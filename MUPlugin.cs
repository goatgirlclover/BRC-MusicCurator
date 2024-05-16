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
        public static bool shufflingPlaylist = false;
        public static MusicTrack playlistStartingTrack; // first played track in playlist

        public static bool skipping = false;
        //public static bool disableSkipping = false;
        public static bool ContinuingStageTrack = false; // is this used for anything? 
        public static bool hasInstantShuffledAlready = false; // shuffle on game startup

        // for apps
        public static int selectedPlaylist = -1;
        public static MusicTrack appSelectedTrack = null;

        // loop single track 
        public static Sprite loopingSingleTrackSprite = null; // for music app
        public static int loopingSingleTrackIndex = -1; // index in musicplayer of looped track

        public static MusicPlayer musicPlayer { get { return Core.Instance.AudioManager.MusicPlayer as MusicPlayer; } }
        public static Player player { get { return WorldHandler.instance?.GetCurrentPlayer(); }}

        public static bool hasShuffleify = false;
        public static bool hasBRR = false;

        public static MusicTrack missingStageTrack = null;

        public static int pausePlaybackSamples = 0;
        public static int pausedTrack; 

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
            appSelectedTrack = null;

            loopingSingleTrackIndex = -1; // index in musicplayer of looped track

            missingStageTrack = null;

            pausePlaybackSamples = 0;
            pausedTrack = 0; 
        }

        private void OnDestroy() {
            Harmony.UnpatchSelf(); 
        }

        private void Update() {
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

            if (PlayerUsingMusicApp()) {
                MusicPlayerTrackButton mptb = (player.phone.AppInstances["AppMusicPlayer"] as AppMusicPlayer).m_TrackList.SelectedButtton as MusicPlayerTrackButton;
                MusicTrack selectedTrack = mptb.AssignedContent as MusicTrack;

                if (pressedAnyButtonIn(MCSettings.keybindsShuffle)) {
                    SetAppShuffle(!musicPlayer.shuffle);
                }

                if (pressedAnyButtonIn(MCSettings.keybindsSkip)) {
                    if (!excludedTracks.Contains(selectedTrack)) { 
                        excludedTracks.Add(selectedTrack);
                        if (ListAInB(GetAllMusic(), excludedTracks)) { excludedTracks.Clear(); } 
                        else if (mptb.IsMyTrackPlaying()) { SkipCurrentTrack(); }
                    } else { 
                        excludedTracks.Remove(selectedTrack); 
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
        }

        public static void SkipCurrentTrack() {
            skipping = true;
            LoadExclusions();
            musicPlayer.ForcePaused();
            //if (excludedTracks.Count >= musicPlayer.musicTrackQueue.AmountOfTracks) { return; }
            musicPlayer.PlayNext();
            skipping = false;
        }

        public static void UpdateButtonColor(MusicPlayerTrackButton button) {
            MusicTrack assignedTrack = button.AssignedContent as MusicTrack;

            bool currentPlaylistExists = false;
            if (currentPlaylistIndex != -1) { 
                if (MusicCuratorPlugin.playlists[currentPlaylistIndex].Any()) {
                    currentPlaylistExists = true; 
                }
            }

            TextMeshProUGUI queuePosLabel = button.m_TitleLabel.GetComponentsInChildren<TextMeshProUGUI>().LastOrDefault();
            if (queuePosLabel != null && queuePosLabel != button.m_TitleLabel) {
                if (queuePosLabel.text == "") { // initial setup - can't add an outline on label creation or game hard crashes
                    queuePosLabel.outlineWidth = 0.3f;
                    queuePosLabel.outlineColor = button.m_ArtistColorNormal;
                }
                if (MusicCuratorPlugin.playlistTracks.Contains(assignedTrack)) {
                    // queued tracks take priority over playlist tracks
                    queuePosLabel.text = (playlistTracks.IndexOf(assignedTrack) + 1 + queuedTracks.Count).ToString();
                } else {
                    queuePosLabel.text = (queuedTracks.IndexOf(assignedTrack) + 1).ToString();
                    // if playing a playlist, add an asterisk to queued tracks to show they aren't part of the playlist
                    if (playlistTracks.Any() && queuePosLabel.text != "0") { 
                        queuePosLabel.text = queuePosLabel.text + "*"; 
                    }
                }
                // hide label if track isn't anywhere
                if (queuePosLabel.text == "0") { queuePosLabel.text = "   "; }
            }
            
            if (assignedTrack.Title != button.m_TitleLabel.text || button.IsHidden) { return; } 

            bool inPlaylist = currentPlaylistExists ? playlists[currentPlaylistIndex].Contains(assignedTrack) : false;
            // TODO: finalize colors (can we make them nicer?)
            // it'd also be nice to have actual icons instead of color coding everything
            if (inPlaylist) {
                    button.m_TitleLabel.color = Color.cyan;
                    button.m_ArtistLabel.color = Color.cyan;
            } else if (excludedTracks.Contains(assignedTrack)) {
                    button.m_TitleLabel.color = Color.red;
                    button.m_ArtistLabel.color = Color.red;
            } else if (queuedTracks.Contains(assignedTrack)) {
                    button.m_TitleLabel.color = Color.magenta;
                    button.m_ArtistLabel.color = Color.magenta;
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
            if (indexOfTarget == -1) {
                Log.LogError("(MusicCuratorPlugin.PlayTrack) Target track invalid!");
                //SkipCurrentTrack();
                return;
            }
            musicPlayer.ForcePaused();
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
            if (currentPlaylistIndex == -1 || currentPlaylistIndex > playlists.Count) {
                return;
            }
            
            MusicTrack currentTrack = (Core.Instance.AudioManager.MusicPlayer as MusicPlayer).musicTrackQueue.CurrentMusicTrack;
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
                if (!playlists[playlistIndex].Any()) {
                    playlists.RemoveAt(playlistIndex);
                    if (currentPlaylistIndex == playlistIndex) {
                        playlistTracks.Clear();
                        MusicCuratorPlugin.currentPlaylistIndex = -1;
                    }
                }
            }
            if (save) { SavePlaylists(true); }
        }

        public static List<MusicTrack> GetAllMusic() { // is there a built in method for this?
            return musicPlayer.musicTrackQueue.currentMusicTracks; // yes, yes there is!
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
            if (playlistIndex < 0 || playlistIndex > MCSettings.customPlaylistNames.Count - 1) {
                return "New Playlist " + (playlistIndex + 1).ToString();
            } else {
                return MCSettings.customPlaylistNames[playlistIndex].Substring(0, Math.Min(MCSettings.customPlaylistNames[playlistIndex].Length, 32));
            }
        }

        public static bool ListAInB<T>(List<T> listA, List<T> listB) {
            return !listA.Except(listB).Any();
            // TODO: try and use this instead of the PlaylistAll methods
        }

        public static bool IsInvalidTrack(MusicTrack willItBlend) {
            return ((willItBlend.Title == string.Empty && willItBlend.Artist == string.Empty) || willItBlend.AudioClip == null);
        }

        public static bool pressedAnyButtonIn(List<KeyCode> keybinds) {
            foreach (KeyCode key in keybinds) { if (UnityEngine.Input.GetKeyUp(key)) { return true; } } 
            return false;
        }

        public static MusicTrack FindTrackBySongID(string songID, bool inclusive = true) {
            List<MusicTrack> foundTracksMatchID = new List<MusicTrack>();
            foreach (MusicTrack trackAtI in GetAllMusic()) {
                if (MusicCuratorPlugin.TrackToSongID(trackAtI).Contains(ConformSongID(songID))) {
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

        public static MusicTrack CreateDummyTrack(string songID) {
            // make an invalid track just to make the rest of the code happy
            MusicTrack dummyTrack = ScriptableObject.CreateInstance<MusicTrack>(); //new MusicTrack();
            string[] splitString = songID.Split(new [] { "-" }, StringSplitOptions.None);
            dummyTrack.AudioClip = null;
            dummyTrack.Artist = splitString[0].Trim();
            dummyTrack.Title = splitString[1].Trim();
            return dummyTrack;
        }

        public static string TrackToSongID(MusicTrack startingTrack) {
            return ConformSongID(startingTrack.Artist + "-" + startingTrack.Title);
        }

        public static string ConformSongID(string ogID) {
            return ogID.Replace(" - ", "-").ToLower().Trim();
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
            PlaylistSaveData.Instance.AutoSave = true;
        }

        public static void LoadExclusions() {
            excludedTracks.Clear();
            foreach (string individualID in PlaylistSaveData.excludedTracksCarryOver) {
                if (!IsInvalidTrack(FindTrackBySongID(individualID))) {
                    excludedTracks.Add(FindTrackBySongID(individualID));
                }
            }
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
            PlaylistSaveData.excludedTracksCarryOver = PlaylistSaveData.defaultExclusions;

            foreach (MusicTrack blocklisted in excludedTracks) {
                if (!PlaylistSaveData.excludedTracksCarryOver.Contains(TrackToSongID(blocklisted))) {
                    PlaylistSaveData.excludedTracksCarryOver.Add(TrackToSongID(blocklisted));
                }
            }   

            ClearDupes_E();
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
            if (playlistIndex < 0 || playlistIndex > playlists.Count) {
                Log.LogError("Playlist " + playlistIndex + " outside range!");
                return false;
            }
            List<MusicTrack> playlist = playlists[playlistIndex];
            foreach (MusicTrack track in playlist) {
                if (!excludedTracks.Contains(track)) { return false; }
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
    }
}
