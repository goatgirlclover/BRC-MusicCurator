using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Reptile;
using Reptile.Phone;
using UnityEngine;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using CommonAPI;

namespace MusicCurator
{
    [BepInPlugin("goatgirl.MusicCurator", "MusicCurator", "1.0.0")]
    [BepInProcess("Bomb Rush Cyberfunk.exe")]
    [BepInDependency("CommonAPI", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("kade.bombrushradio", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("fr.glomzubuk.plugins.brc.trackremix", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.dragsun.Shufleify", BepInDependency.DependencyFlags.SoftDependency)]
    //[BepInDependency("com.dragsun.Shuffleify", BepInDependency.DependencyFlags.SoftDependency)]
    public class MusicCuratorPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static Harmony Harmony = new Harmony("goatgirl.MusicCurator");
        public static MusicCuratorPlugin Instance { get; private set; }
        public string Directory => Path.GetDirectoryName(Info.Location);
        public static System.Random rando = new System.Random();
        
        public static List<MusicTrack> excludedTracks = new List<MusicTrack>();
        public static List<MusicTrack> queuedTracks = new List<MusicTrack>();

        // playlists from save data, but converted to music tracks
        public static List<List<MusicTrack>> playlists = new List<List<MusicTrack>>();
        // queued up tracks from playlist. similar to playlists[currentPlaylistIndex], but can be rearranged freely
        public static List<MusicTrack> playlistTracks = new List<MusicTrack>();
        
        public static int currentPlaylistIndex = -1; 
        public static bool shufflingPlaylist = false; // idk if this is still useful
        public static MusicTrack playlistStartingTrack; // first played track in playlist

        public static bool skipping = false;
        //public static bool disableSkipping = false;
        public static bool ContinuingStageTrack = false; // if true, don't reset queue on next track lpay
        public static bool hasInstantShuffledAlready = false; // shuffle on game startup
        
        public static MusicTrack previousTrack; 
        //public static int numberOfConsecutiveSkips = 0; 

        // for apps
        public static int selectedPlaylist = -1;
        public static int appSelectedTrack = -1; // index of track in selectedPlaylist

        // loop single track 
        public static Sprite loopingSingleTrackSprite = null; // for music app
        public static int loopingSingleTrackIndex = -1; // index in musicplayer of looped track

        public static MusicPlayer musicPlayer { get { return Core.Instance.AudioManager.MusicPlayer as MusicPlayer; } }
        public static Player player { get { return WorldHandler.instance?.GetCurrentPlayer(); }}

        public static bool hasShuffleify = false;
        public static bool hasBRR = false;

        public static MusicTrack missingStageTrack = null; // for AllMixtapes

        public static int pausePlaybackSamples = 0;
        public static int pausedTrack; 

        public const string songIDSymbol = "♫"; // v0.1.0 = "-", v0.1.1 = "♫"

        public static GameplayUI gameplayUI;
        public static float timeOnSameTrack = 0f;
        private int trackInstanceId = -1;

        private void Awake()
        {
            Instance = this;
            MusicCuratorPlugin.Log = base.Logger; // i don't remember why we do this this way
            Harmony.PatchAll(); 
            Logger.LogInfo($"Plugin MusicCurator is loaded!");
            //Logger.LogInfo($"Keep in mind MusicCurator has not been extensively tested just yet. Watch out for bugs!");

            MCSettings.BindSettings(Config);
            MCSettings.UpdateSettings();      

            new PlaylistSaveData();
            AppPlaylists.Initialize();
                AppNewPlaylist.Initialize();
                    AppPlaylistTracklist.Initialize();
                AppSelectedPlaylist.Initialize();
                    AppEditPlaylist.Initialize();
                        AppReorderPlaylist.Initialize();
                        AppRemoveTrackFromPlaylist.Initialize(); // TODO: condense into AppConfirm
                    AppDeletePlaylist.Initialize();
                AppManageQueueAndExclusions.Initialize();
                    AppDeleteAllPlaylists.Initialize(); // TODO: condense into AppConfirm
            // SO MANY APPS!!!!!

            loopingSingleTrackSprite = CommonAPI.TextureUtility.LoadSprite(Path.Combine(MusicCuratorPlugin.Instance.Directory, "MC-LoopSingle.png")); // same place as plugin dll - keep in mind for thunderstore
            loopingSingleTrackSprite.texture.filterMode = FilterMode.Point;

            foreach (var plugin in BepInEx.Bootstrap.Chainloader.PluginInfos)
            {
                if (plugin.Value.Metadata.GUID.Contains("BombRushRadio"))
                { 
                    hasBRR = true; 
                    Log.LogInfo($"BombRushRadio install detected!");
                }

                if (plugin.Value.Metadata.GUID.Equals("com.dragsun.Shuffleify") || plugin.Value.Metadata.GUID.Equals("com.dragsun.Shufleify"))
                { 
                    // should mark as incompat in bepinex and thunderstore too
                    hasShuffleify = true;
                    MusicCuratorPlugin.Log.LogError($"Shuffleify install detected!");
                    MusicCuratorPlugin.Log.LogError($"Shuffleify patches much of the same code that MusicCurator patches, and implements many of the same features (startup shuffle, looping single songs, etc.)");
                    MusicCuratorPlugin.Log.LogError($"Running both Shuffleify and MusicCurator can and likely will lead to issues with both plugins!");
                    MusicCuratorPlugin.Log.LogError($"Considering the significant amount of feature overlap, I highly recommend uninstalling either Shuffleify or MusicCurator.");
                }
            }       
        }

        public static void resetVariables() {
            excludedTracks = new List<MusicTrack>();
            queuedTracks = new List<MusicTrack>();
            playlists = new List<List<MusicTrack>>();
            playlistTracks = new List<MusicTrack>();
            
            currentPlaylistIndex = -1; 
            shufflingPlaylist = false;
            playlistStartingTrack = null; // first played track in playlist

            skipping = false;
            ContinuingStageTrack = false; // is this used for anything? 
            hasInstantShuffledAlready = false; // shuffle on game startup

            selectedPlaylist = -1;
            appSelectedTrack = -1;

            loopingSingleTrackIndex = -1; // index in musicplayer of looped track

            missingStageTrack = null;

            pausePlaybackSamples = 0;
            pausedTrack = 0; 
        }

        private void OnDestroy() {
            Harmony.UnpatchSelf(); 
        }

        private void Update() {
            if (Core.Instance == null || musicPlayer == null) { return; }

            if (musicPlayer.IsPlaying) {
                if (MusicCuratorPlugin.AllUnlockedTracksExcluded()) {
                    if (!(MusicCuratorPlugin.playlistTracks.Any() && MCSettings.playlistTracksNoExclude.Value)) {
                        musicPlayer.ForcePaused();
                    }
                }

                if (player != null && musicPlayer.CurrentTrackTime > 0.2f && musicPlayer.GetMusicTrack(musicPlayer.CurrentTrackIndex).GetInstanceID() != trackInstanceId) { 
                    SetTrackPopupText(musicPlayer.musicTrackQueue.CurrentMusicTrack.Artist + " - " + musicPlayer.musicTrackQueue.CurrentMusicTrack.Title);
                    trackInstanceId = musicPlayer.GetMusicTrack(musicPlayer.CurrentTrackIndex).GetInstanceID();
                }
            }

            if (MCSettings.strictBlocklist.Value && player != null) {
                if (musicPlayer.IsPlaying && GetAllUnlockedMusic().Count > 1 && !MusicCuratorPlugin.AllUnlockedTracksExcluded() && MusicCuratorPlugin.TrackIsExcluded(musicPlayer.GetMusicTrack(musicPlayer.CurrentTrackIndex))) {
                    MusicCuratorPlugin.SkipCurrentTrack();
                } /*else if (MusicCuratorPlugin.AllUnlockedTracksExcluded() && !musicPlayer.isPlaying) { musicPlayer.ForcePaused(); }*/

                List<AudioClip> excludedAudioClips = new List<AudioClip>(); 
                foreach (MusicTrack gameTrack in GetAllMusicIncludingLocked()) {
                    if (gameTrack != null && gameTrack.AudioClip != null) { 
                        if (TrackIsExcluded(gameTrack)) { 
                            excludedAudioClips.Add(gameTrack.AudioClip); 
                        }
                    }
                }

                List<AudioSource> audioSources = new List<AudioSource> {Core.Instance.musicBlendingAudioSource, Core.Instance.musicAudioSource};
                foreach (CutsceneAudioTrack meow in GameObject.FindObjectsOfType<CutsceneAudioTrack>()) { audioSources.Add(meow.source); }
                audioSources = audioSources.Distinct().ToList();
                
                foreach (AudioSource src in audioSources) { if (src != null && src.clip != null) { 
                    src.mute = excludedAudioClips.Contains(src.clip); 
                } }
                
            } else if (!MCSettings.unlockEncounterMusic.Value && !MCSettings.strictBlocklist.Value) {
                if (musicPlayer.IsPlaying) {
                    if (MusicCuratorPlugin.AllAvailableTracksExcluded()) {
                        if (!(MusicCuratorPlugin.playlistTracks.Any() && MCSettings.playlistTracksNoExclude.Value)) {
                            musicPlayer.ForcePaused();
                        }
                    }
                }
            }

            if (player != null && player.phone != null && MCSettings.unlockPhone.Value) {
                if (WorldHandler.instance != null && WorldHandler.instance.currentEncounter != null) {
                    if (!WorldHandler.instance.currentEncounter.allowPhone) { 
                        WorldHandler.instance.currentEncounter.allowPhone = true; 
                    }
                }
                
                if (player.phoneLocked) { player.LockPhone(false); }
                if (!player.phone.m_PhoneAllowed) { player.phone.AllowPhone(true); }
            }
             
            if (pressedAnyButtonIn(MCSettings.keybindsPause)) {
                if (musicPlayer.IsPlaying) {
                    pausePlaybackSamples = musicPlayer.CurrentTrackSamples;
                    pausedTrack = musicPlayer.CurrentTrackIndex; 
                    musicPlayer.Pause();
                } else { 
                    musicPlayer.Play(); 
                    if (musicPlayer.CurrentTrackIndex == pausedTrack) { musicPlayer.playbackSamples = pausePlaybackSamples; }
                }
            }

            if (pressedAnyButtonIn(MCSettings.keybindsShuffle)) {
                SetAppShuffle(!musicPlayer.shuffle);
            }

            if (PlayerUsingMusicApp()) {
                MusicPlayerTrackButton mptb = (player.phone.AppInstances["AppMusicPlayer"] as AppMusicPlayer).m_TrackList.SelectedButtton as MusicPlayerTrackButton;
                MusicTrack selectedTrack = mptb.AssignedContent as MusicTrack;

                if (pressedAnyButtonIn(MCSettings.keybindsSkip)) {
                    if (!TrackIsExcluded(selectedTrack)) { 
                        excludedTracks.Add(selectedTrack);
                        if (mptb.IsMyTrackPlaying()) { SkipCurrentTrack(); }
                        //CheckIfAllExcluded();
                        SaveExclusions();
                    } else { 
                        excludedTracks.Remove(selectedTrack); 
                        SaveExclusions();
                    }
                } 
                
                else if (pressedAnyButtonIn(MCSettings.keybindsQueue)) {
                    if (!queuedTracks.Contains(selectedTrack)) { queuedTracks.Add(selectedTrack); }
                    else { queuedTracks.Remove(selectedTrack); }
                }
            } else {
                if (pressedAnyButtonIn(MCSettings.keybindsSkip)) {
                    SkipCurrentTrack();
                } //else if (pressedAnyButtonIn(MCSettings.keybindsQueue)) {
                    //return;
                //}
            }

            if (player != null && player.phone != null) {
                UpdateTrackPopup();
            }
            
        }

        public static void SkipCurrentTrack() {
            //LoadExclusions();
            previousTrack = musicPlayer.GetMusicTrack(musicPlayer.CurrentTrackIndex);
            skipping = true;
            musicPlayer.ForcePaused();
            //if (excludedTracks.Count >= musicPlayer.musicTrackQueue.AmountOfTracks) { return; }
            if (!MusicCuratorPlugin.AllUnlockedTracksExcluded() || (MusicCuratorPlugin.playlistTracks.Any() && MCSettings.playlistTracksNoExclude.Value)) {
                musicPlayer.PlayNext();
            }
            skipping = false;
            previousTrack = null;
        }

        public static void UpdateTrackPopup() {
            if (CommonAPI.Phone.AppUtility.GetAppFont() != GameplayUIPatches.trackLabel.font) {
                GameplayUIPatches.trackLabel.font = CommonAPI.Phone.AppUtility.GetAppFont();

                var _outlineMaterial = GameplayUIPatches.trackLabel.fontMaterial;
                _outlineMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
                _outlineMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
                GameplayUIPatches.trackLabel.fontMaterial = _outlineMaterial;
            }

            timeOnSameTrack += Time.deltaTime; 

            float screenRatio = (float)Screen.height/1600.0f;
            float posX = MCSettings.musicPosX.Value*screenRatio;
            float posY = MCSettings.musicPosY.Value*screenRatio;
            float sX = MCSettings.musicSX.Value;
            float sY = MCSettings.musicSY.Value;
            float imgPosX = MCSettings.imgPosX.Value;
            float imgPosY = MCSettings.imgPosY.Value;
            float outlineWidth = MCSettings.outlineWidth.Value;

            GameplayUIPatches.trackLabel.transform.position = new Vector3(gameplayUI.wanted1.position.x - posX, gameplayUI.trickNameLabel.transform.position.y - posY, 0f);
            GameplayUIPatches.trackLabel.GetComponent<RectTransform>().sizeDelta = new Vector3(sX, sY);
            GameplayUIPatches.trackLogo.transform.localPosition = new Vector3 (imgPosX, imgPosY, 0f);
            GameplayUIPatches.trackLabel.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);

            float timeOpacity = player.phone.IsOn ? 0.0f : (timeOnSameTrack < 4f ? 1.0f : 0.0f); 
            float speed = 0.2f*(Time.deltaTime*60f);

            GameplayUIPatches.trackLabel.faceColor = new Color(1f, 1f, 1f, Mathf.Lerp(GameplayUIPatches.trackLogoImage.color.a, timeOpacity, speed));
            GameplayUIPatches.trackLabel.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, Mathf.Lerp(GameplayUIPatches.trackLogoImage.color.a, timeOpacity, speed)));
            GameplayUIPatches.trackLogoImage.color = new Color(1f, 1f, 1f, Mathf.Lerp(GameplayUIPatches.trackLogoImage.color.a, timeOpacity, speed));
        }

        public static void SetTrackPopupText(string text) {
            GameplayUIPatches.trackLabel.SetText(text);
            timeOnSameTrack = 0f;
        }

        public static void UpdateButtonColor(MusicPlayerTrackButton button) {
            MusicTrack assignedTrack = button.AssignedContent as MusicTrack;

            //bool currentPlaylistExists = false;
            //if (currentPlaylistIndex != -1) { 
            //    if (MusicCuratorPlugin.playlists[currentPlaylistIndex].Any()) {
            //        currentPlaylistExists = true; 
            //    }
            //}

            TextMeshProUGUI queuePosLabel = button.m_TitleLabel.GetComponentsInChildren<TextMeshProUGUI>().LastOrDefault();
            if (queuePosLabel != null && queuePosLabel != button.m_TitleLabel) {
                if (queuePosLabel.text == "") { // initial setup - can't add an outline on label creation or game hard crashes
                    queuePosLabel.outlineWidth = 0.4f;
                    queuePosLabel.outlineColor = button.m_ArtistColorNormal;
                    queuePosLabel.fontSharedMaterial.SetFloat(ShaderUtilities.ID_FaceDilate,0.4f);
                }
                if (MusicCuratorPlugin.playlistTracks.Contains(assignedTrack) && !button.IsMyTrackPlaying()) {
                    // queued tracks take priority over playlist tracks, so add the queued tracks count first
                    queuePosLabel.text = (playlistTracks.IndexOf(assignedTrack) + 1 + queuedTracks.Count).ToString();
                } else {
                    queuePosLabel.text = (queuedTracks.IndexOf(assignedTrack) + 1).ToString();
                    // if playing a playlist, add an asterisk to queued tracks to show they aren't part of the playlist
                    if (playlistTracks.Any() && currentPlaylistIndex >= 0 && queuePosLabel.text != "0") { 
                        queuePosLabel.text = queuePosLabel.text + "*"; 
                    }
                }
                // hide label if track isn't anywhere
                if (queuePosLabel.text == "0") { queuePosLabel.text = "   "; }
            }
            
            if (assignedTrack.Title != button.m_TitleLabel.text || button.IsHidden) { return; } 

            //bool inPlaylist = currentPlaylistExists ? playlists[currentPlaylistIndex].Contains(assignedTrack) : false;
            // TODO: finalize colors (can we make them nicer?)
            // it'd also be nice to have actual icons instead of color coding everything
        //    if (inPlaylist) {
        //            button.m_TitleLabel.color = Color.cyan;
        //            button.m_ArtistLabel.color = Color.cyan;
            if (TrackIsExcluded(assignedTrack)) {
                    button.m_TitleLabel.color = Color.red;
                    button.m_ArtistLabel.color = Color.red;
        //    } else if (queuedTracks.Contains(assignedTrack)) {
        //            button.m_TitleLabel.color = Color.magenta;
        //            button.m_ArtistLabel.color = Color.magenta;
            } else if (button.IsSelected) {
                    button.m_TitleLabel.color = button.m_TitleColorSelected;
                    button.m_ArtistLabel.color = button.m_ArtistColorSelected;
            } else {
                    button.m_TitleLabel.color = button.m_TitleColorNormal;
                    button.m_ArtistLabel.color = button.m_ArtistColorNormal;  
            }
        }

        public static void PlayTrack(MusicTrack targetTrack, int playbackSamples = 0) {
            int indexOfTarget = musicPlayer.musicTrackQueue.currentMusicTracks.IndexOf(targetTrack);
            if (indexOfTarget == -1 || indexOfTarget >= GetAllMusic().Count) {
                Log.LogError("(MusicCuratorPlugin.PlayTrack) Target track outside range!");
                //SkipCurrentTrack();
                return;
            }

            musicPlayer.ForcePaused();

            if (MusicCuratorPlugin.AllUnlockedTracksExcluded()) { 
                if (!(MusicCuratorPlugin.playlistTracks.Any() && MCSettings.playlistTracksNoExclude.Value)) {
                    return;
                } 
            }

            ContinuingStageTrack = true; // workaround - bypass our prefix so we don't clear the queue
            musicPlayer.PlayFrom(indexOfTarget, playbackSamples);
            ContinuingStageTrack = false;
        }

        public static void SetAppShuffle(bool shuffling) {
            if (player == null || player.phone == null)  { return; }
            player.phone.GetAppInstance<AppMusicPlayer>().ToggleShuffle(shuffling);
        }

        public static void LoadPlaylistIntoQueue(int playlistIndex, bool randomize = false) {
            if (playlistIndex < 0 || playlistIndex > playlists.Count) {
                Log.LogError("Playlist " + playlistIndex + " outside range!");
                return;
            }
            
            playlistTracks.Clear();
            foreach (MusicTrack playTrack in playlists[playlistIndex]) {
                if (!IsInvalidTrack(playTrack)) { playlistTracks.Add(playTrack); }
            }

            if (!playlistTracks.Any()) {
                Log.LogError("Playlist " + playlistIndex + " entirely invalid/blank! Not playing it!");
                return; 
            }

            currentPlaylistIndex = playlistIndex;
            //Log.LogInfo(playlistTracks.Count());
            if (randomize) { playlistTracks = RandomizeList(playlistTracks); }
            playlistStartingTrack = playlistTracks.LastOrDefault();
        }

        public static void ReorderPlaylistInQueue(bool randomize, bool seekToCurrentTrack = true) {
            if (currentPlaylistIndex == -1 || currentPlaylistIndex >= playlists.Count) {
                return;
            }
            
            MusicTrack currentTrack = musicPlayer.musicTrackQueue.CurrentMusicTrack;
            List<MusicTrack> reorderedPlaylistTracks = new List<MusicTrack>();
            foreach (MusicTrack playlistTrack in playlists[currentPlaylistIndex].ToList()) {
                if (!IsInvalidTrack(playlistTrack)) { //if (playlistTracks.Contains(playlistTrack)) {
                    reorderedPlaylistTracks.Add(playlistTrack);
                }
            }

            if (randomize) { reorderedPlaylistTracks = RandomizeList(reorderedPlaylistTracks);  }
            playlistTracks = reorderedPlaylistTracks;
            
            // loop through playlist and push current track to end, just like how the playnext patch does over time
            if (seekToCurrentTrack) { 
                foreach (MusicTrack newPlaylistTrack in reorderedPlaylistTracks.ToList()) {
                    playlistTracks.RemoveAt(0);
                    playlistTracks.Add(newPlaylistTrack);
                    if (newPlaylistTrack == currentTrack) { break; }
                }
            }
        }

        public static void ClearEmptyPlaylists(bool save = true)
        {
            foreach (int playlistIndex in Enumerable.Range(0, playlists.Count).ToArray()) {
                if (playlists.ElementAtOrDefault(playlistIndex) != null) {
                    if (!playlists[playlistIndex].Any()) {
                        playlists.RemoveAt(playlistIndex);
                        if (currentPlaylistIndex == playlistIndex) {
                            playlistTracks.Clear();
                            MusicCuratorPlugin.currentPlaylistIndex = -1;
                        }
                    }
                }
            }
            if (save) { SavePlaylists(true); }
        }

        /* avaliable music in musicplayer */
        public static List<MusicTrack> GetAllMusic() { return musicPlayer.musicTrackQueue.currentMusicTracks; }

        /* should be avaliable in musicplayer - may not be due to forced music during story events */
        public static List<MusicTrack> GetAllUnlockedMusic() { 
            if (player == null) { return GetAllMusic(); }
            List<MusicTrack> allTracks = new List<MusicTrack>(); 
			
            MusicTrack musicTrackByID = Core.Instance.AudioManager.MusicLibraryPlayer.GetMusicTrackByID(MusicTrackID.Hideout_Mixtape);
            MusicTrack chapterMusic2 = Core.Instance.baseModule.StageManager.chapterMusic.GetChapterMusic(Story.Chapter.CHAPTER_6);
            if (MCSettings.allMixtapes.Value) { allTracks.Add(chapterMusic2); }
			if (MCSettings.allMixtapes.Value || Core.Instance.BaseModule.CurrentStage == Stage.hideout) { allTracks.Add(musicTrackByID); }
            
            MusicTrack chapterMusic3 = Core.Instance.baseModule.StageManager.chapterMusic.GetChapterMusic(Story.GetCurrentObjectiveInfo().chapter);
            if (!allTracks.Contains(chapterMusic3)) { allTracks.Add(chapterMusic3); }

			AUnlockable[] unlockables = player.phone.GetAppInstance<AppMusicPlayer>().Unlockables;
			for (int i = 0; i < unlockables.Length; i++)
			{
				MusicTrack musicTrack = unlockables[i] as MusicTrack;
                if (Core.Instance.Platform.User.GetUnlockableSaveDataFor(musicTrack).IsUnlocked) {
                    musicTrack.isRepeatable = false;
                    if (!allTracks.Contains(musicTrack)) { allTracks.Add(musicTrack); }
                }
			}

            foreach (MusicTrack additionalTrack in GetAllMusic()) {
                if (!allTracks.Contains(additionalTrack)) { allTracks.Add(additionalTrack); }
            }

            if (hasBRR) { 
                foreach (MusicTrack customTrack in BRRHelper.BRRAudios) {
                    if (!allTracks.Contains(customTrack)) { allTracks.Add(customTrack); }
                }
            }

            return allTracks;
        }
        
        /* all music tracks in the game */
        public static List<MusicTrack> GetAllMusicIncludingLocked(bool getInvalid = false) {
            List<MusicTrack> allTracks = new List<MusicTrack>(); 
			
            MusicTrack musicTrackByID = Core.Instance.AudioManager.MusicLibraryPlayer.GetMusicTrackByID(MusicTrackID.Hideout_Mixtape);
			allTracks.Add(musicTrackByID);

			AUnlockable[] unlockables = player.phone.GetAppInstance<AppMusicPlayer>().Unlockables;
			for (int i = 0; i < unlockables.Length; i++)
			{
				MusicTrack musicTrack = unlockables[i] as MusicTrack;
                musicTrack.isRepeatable = false;
                allTracks.Add(musicTrack);
			}

            MusicTrack chapterMusic2 = Core.Instance.baseModule.StageManager.chapterMusic.GetChapterMusic(Story.Chapter.CHAPTER_6);
            allTracks.Add(chapterMusic2);

            foreach (MusicTrack additionalTrack in GetAllMusic()) {
                if (!allTracks.Contains(additionalTrack)) { allTracks.Add(additionalTrack); }
            }

            if (hasBRR) { 
                foreach (MusicTrack customTrack in BRRHelper.BRRAudios) {
                    if (!allTracks.Contains(customTrack)) { allTracks.Add(customTrack); }
                }
            }

            if (getInvalid) {
                List<string> allTrackID = new List<string>(); 

                foreach (MusicTrack additionalTrack in excludedTracks) {
                    if (!allTracks.Contains(additionalTrack) && !allTrackID.Contains(TrackToSongID(additionalTrack))) { 
                        allTracks.Add(additionalTrack); 
                        allTrackID.Add(TrackToSongID(additionalTrack));
                    }
                }

                foreach (MusicTrack additionalTrack in queuedTracks) {
                    if (!allTracks.Contains(additionalTrack) && !allTrackID.Contains(TrackToSongID(additionalTrack))) { 
                        allTracks.Add(additionalTrack); 
                        allTrackID.Add(TrackToSongID(additionalTrack));
                    }
                }

                foreach (List<MusicTrack> playlist in playlists) {
                    foreach (MusicTrack additionalTrack in playlist) {
                        if (!allTracks.Contains(additionalTrack) && !allTrackID.Contains(TrackToSongID(additionalTrack))) { 
                            allTracks.Add(additionalTrack); 
                            allTrackID.Add(TrackToSongID(additionalTrack));
                        }
                    }
                }
            }

            return allTracks;
        }

        public static int CreatePlaylist() {
            playlists.Add(new List<MusicTrack>());
            SavePlaylists(true);
            return playlists.Count - 1; // index of new playlist
        }

        public static List<T> RandomizeList<T>(List<T> listToRandomize) {
            if (listToRandomize.Count <= 1) { return listToRandomize; }
            List<T> randomizedList = listToRandomize;
            int i = randomizedList.Count;  
            
            while (i > 1) {  
                i--;  
                int k = rando.Next(i + 1);  
                var randomvalue = randomizedList[k];  
                randomizedList[k] = randomizedList[i];  
                randomizedList[i] = randomvalue;  
            }  
            return randomizedList;
        }

        public static bool PlayerUsingMusicApp() {
            if (player == null || player.phone == null)  { return false; }
            return (player.phone.m_CurrentApp is AppMusicPlayer && player.phone.IsOn && player.phoneLayerWeight >= 1f);
        }

        public static string GetPlaylistName(int playlistIndex) {
            if (playlistIndex < 0 || playlistIndex >= MCSettings.customPlaylistNames.Count) {
                return "New Playlist " + (playlistIndex + 1).ToString();
            } else {
                return MCSettings.customPlaylistNames[playlistIndex].Substring(0, Math.Min(MCSettings.customPlaylistNames[playlistIndex].Length, 32));
            }
        }

        public static bool ListAInB<T>(List<T> listA, List<T> listB) {
            return !listA.Except(listB).Any();
            // TODO: try and use this instead of the PlaylistAll methods
        }

        public static bool IsInvalidTrack(MusicTrack checkTrack) {
            return ((checkTrack.Title == string.Empty && checkTrack.Artist == string.Empty) || checkTrack.AudioClip == null);
        }

        public static bool pressedAnyButtonIn(List<KeyCode> keybinds) {
            foreach (KeyCode key in keybinds) { if (UnityEngine.Input.GetKeyUp(key)) { return true; } } 
            return false;
        }

        public static MusicTrack FindTrackBySongID(string songID) {
            List<MusicTrack> foundTracksMatchID = new List<MusicTrack>();
            foreach (MusicTrack trackAtI in GetAllMusic()) {
                if (MusicCuratorPlugin.TrackToSongID(trackAtI).Equals(ConformSongID(songID))) {
                    foundTracksMatchID.Add(trackAtI);
                }
            }

            if (foundTracksMatchID.Count < 1) { 
                Log.LogWarning(String.Format("FindTrackBySongID: Track {0} not found. Creating empty track to prevent crashes", songID));
                return CreateDummyTrack(songID); 
            }
            else if (foundTracksMatchID.Count > 1) {
                Log.LogError(String.Format("FindTrackBySongID: Multiple tracks are sharing songID {0} (same name and artist)! Please make sure your custom songs all have unique names/artists!!! First track found using songID will be returned - this may not be what you want!", songID));
            } 
            return foundTracksMatchID[0];
        }

        public static MusicTrack LooseFindTrackBySongID(string songID) { // intended for playlist repair
            List<MusicTrack> foundTracksMatchID = new List<MusicTrack>();
            string[] splitString = SplitSongID(songID);
            //string songIDArtist = splitString[0].Trim();
            string songIDTitle = splitString[1].Trim();

            int closestCharDifference = 999;

            foreach (MusicTrack trackAtI in GetAllMusic()) {
                string trackAtID = MusicCuratorPlugin.TrackToSongID(trackAtI);
                if (trackAtID.Contains(ConformSongID(songID))) {
                    foundTracksMatchID.Add(trackAtI);
                    int charDifference = Mathf.Abs(trackAtID.Length - songID.Length); 
                    if (charDifference < closestCharDifference) { closestCharDifference = charDifference; }
                }
            }

            foreach (MusicTrack foundTrack in foundTracksMatchID) {
                if (Mathf.Abs(MusicCuratorPlugin.TrackToSongID(foundTrack).Length - songID.Length) == closestCharDifference) {
                    return foundTrack;
                }
            }

            // last resort - match by only title
            foundTracksMatchID.Clear();
            closestCharDifference = 999;
            foreach (MusicTrack trackAtI in GetAllMusic()) {
                string trackAtID = MusicCuratorPlugin.TrackToSongID(trackAtI);
                if (trackAtID.Contains(songIDTitle)) { 
                    foundTracksMatchID.Add(trackAtI);
                    int charDifference = Mathf.Abs(trackAtI.Title.Length - songIDTitle.Length); 
                    if (charDifference < closestCharDifference) { closestCharDifference = charDifference; }
                }
            }

            foreach (MusicTrack foundTrack in foundTracksMatchID) {
                if (Mathf.Abs(foundTrack.Title.Length - songIDTitle.Length) == closestCharDifference) {
                    return foundTrack;
                }
            }

            return CreateDummyTrack(songID);
        }

        public static MusicTrack CreateDummyTrack(string songID) {
            // make an invalid track just to make the rest of the code happy
            MusicTrack dummyTrack = ScriptableObject.CreateInstance<MusicTrack>(); //new MusicTrack();
            string[] splitString = SplitSongID(songID);
            dummyTrack.AudioClip = null;
            dummyTrack.Artist = splitString[0].Trim();
            dummyTrack.Title = splitString[1].Trim();
            return dummyTrack;
        }

        public static string TrackToSongID(MusicTrack startingTrack) {
            return ConformSongID(startingTrack.Artist + songIDSymbol + startingTrack.Title);
        }

        public static string ConformSongID(string ogID) {
            return ogID.Replace(" " + songIDSymbol + " ", songIDSymbol).ToLower().Trim();
        }

        public static string[] SplitSongID(string songID, string splitSymbol = songIDSymbol) {
            string[] returnValue = songID.Split(new [] { songIDSymbol }, StringSplitOptions.None);
            if (returnValue.Length > 2) { 
                Log.LogError("SplitSongID: SongID \"" + songID + "\" using illegal symbol \"" + splitSymbol + "\" in either title or artist! Please remove this symbol from this track's title/artist. This track cannot be safely added to playlists or blocklists.");
            }
            return returnValue;
        }

        public static void LoadPlaylists(List<List<string>> playlistsBySongID) {
            playlists.Clear();
            foreach (List<string> individualPlaylist in playlistsBySongID) {
                List<MusicTrack> newPlaylist = new List<MusicTrack>();
                foreach (string individualID in individualPlaylist) {
                    newPlaylist.Add(FindTrackBySongID(individualID));
                }
                playlists.Add(newPlaylist); // playlists[i] = newPlaylist;
            }
            LoadExclusions();
            PlaylistSaveData.Instance.AutoSave = true; // ensures PlaylistSaveData doesn't save over with empty playlists
        }

        public static void LoadExclusions() {
            excludedTracks.Clear();
            foreach (string individualID in PlaylistSaveData.excludedTracksCarryOver) {
                //if (!IsInvalidTrack(FindTrackBySongID(individualID))) {
                    excludedTracks.Add(FindTrackBySongID(individualID));
                //}
            }
            //if (!skipCheck) { CheckIfAllExcluded(); }
        }

        public static void SavePlaylists(bool skipClear = false) {
            if (!skipClear) { 
                ClearEmptyPlaylists(false); 
                ClearDupes_P();
            }

            PlaylistSaveData.playlists.Clear();
            foreach (List<MusicTrack> individualPlaylist in playlists) {
                List<string> newPlaylist = new List<string>();
                foreach (MusicTrack individualTrack in individualPlaylist) {
                    newPlaylist.Add(TrackToSongID(individualTrack));
                }
                PlaylistSaveData.playlists.Add(newPlaylist);
            }

            SaveExclusions();
        }

        public static void SaveExclusions() {
            //PlaylistSaveData.excludedTracksCarryOver = PlaylistSaveData.defaultExclusions;
            PlaylistSaveData.excludedTracksCarryOver.Clear();

            foreach (MusicTrack blocklisted in excludedTracks) {
                if (!PlaylistSaveData.excludedTracksCarryOver.Contains(TrackToSongID(blocklisted))) {
                    PlaylistSaveData.excludedTracksCarryOver.Add(TrackToSongID(blocklisted));
                }
            }   

            ClearDupes_E();
            //CheckIfAllExcluded();
        }

        public static void ClearDupes_P() {
            int i = -1;
            foreach (List<MusicTrack> playlist in playlists.ToList()) { 
                i++;
                playlists[i] = playlist.Distinct().ToList();
                PlaylistSaveData.playlists[i] = PlaylistSaveData.playlists[i].Distinct().ToList();
            }
        }

        public static void ClearDupes_E() {
            excludedTracks = excludedTracks.Distinct().ToList();
            PlaylistSaveData.excludedTracksCarryOver = PlaylistSaveData.excludedTracksCarryOver.Distinct().ToList();
        }

        public static bool PlaylistAllInvalidTracks(int playlistIndex) {
            if (playlistIndex < 0 || playlistIndex > playlists.Count) {
                Log.LogError("Playlist " + playlistIndex + " outside range!");
                return false;
            }
            List<MusicTrack> playlist = playlists[playlistIndex];
            foreach (MusicTrack track in playlist) {
                if (!IsInvalidTrack(track)) { return false; }
            } return true;
        }

        public static bool PlaylistAllExcludedTracks(int playlistIndex) {
            if (AllUnlockedTracksExcluded()) { return true; }

            if (playlistIndex < 0 || playlistIndex > playlists.Count) {
                Log.LogError("Playlist " + playlistIndex + " outside range!");
                return false;
            }

            List<MusicTrack> playlist = playlists[playlistIndex];
            foreach (MusicTrack track in playlist) {
                if (!IsInvalidTrack(track)) {
                    if (!TrackIsExcluded(track)) { return false; }
                }
            } return true;
        }

        public static bool PlaylistAnyInvalidTracks(int playlistIndex) {
            if (playlistIndex < 0 || playlistIndex > playlists.Count) {
                Log.LogError("Playlist " + playlistIndex + " outside range!");
                return false;
            }
            List<MusicTrack> playlist = playlists[playlistIndex];
            foreach (MusicTrack track in playlist) {
                if (IsInvalidTrack(track)) { return true; }
            } return false;
        }

        public static bool AllUnlockedTracksExcluded() {
            if (!GetAllUnlockedMusic().Any()) { return true; }
            else if (!excludedTracks.Any()) { return false; }
            foreach (MusicTrack music in GetAllUnlockedMusic()) { if (!TrackIsExcluded(music)) { return false; } }
            return true;
        }

        public static bool AllAvailableTracksExcluded() {
            if (!GetAllMusic().Any()) { return true; }
            else if (!excludedTracks.Any()) { return false; }
            foreach (MusicTrack music in GetAllMusic()) { if (!TrackIsExcluded(music)) { return false; } }
            return true;
        }

        public static bool TrackIsExcluded(MusicTrack checkTrack) {
            return excludedTracks.Contains(checkTrack) || PlaylistSaveData.excludedTracksCarryOver.Contains(TrackToSongID(checkTrack)); 
        }
    }
}
