using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Reptile;
using Reptile.Phone;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;

namespace MusicCurator {
    [HarmonyPatch(typeof(Reptile.Phone.Phone))]
    internal class PhonePatches {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(Reptile.Phone.Phone.TurnOff))]
        public static void TurnOffPostfix_ResetAppOptions() {
            MusicCuratorPlugin.selectedPlaylist = -1;
            MusicCuratorPlugin.appSelectedTrack = -1;
        } 
    }

    [HarmonyPatch(typeof(Reptile.GameplayUI))]
    internal class GameplayUIPatches {
        public static TextMeshProUGUI trackLabel;
        public static RectTransform trackLogo;
        public static Image trackLogoImage;
        
        [HarmonyPostfix]
        [HarmonyPatch(nameof(Reptile.GameplayUI.Init))]
        public static void SetupMusicLabel(GameplayUI __instance) { 
            if (MCSettings.enableTrackDisplay.Value && trackLogoImage == null) {
                MusicCuratorPlugin.gameplayUI = __instance;
                trackLabel = UnityEngine.Object.Instantiate(__instance.tricksInComboLabel, __instance.tricksInComboLabel.transform.parent);
                trackLabel.transform.localPosition = __instance.tricksInComboLabel.transform.localPosition;
                trackLabel.transform.localPosition -= new Vector3(0, 20.0f*((float)Screen.height/1600.0f), 0f);
                trackLabel.alignment = TextAlignmentOptions.Left;

                GameObject imgObject = new GameObject("Track Icon");
                RectTransform trans = imgObject.AddComponent<RectTransform>();
                trans.transform.SetParent(trackLabel.transform); // setting parent
                trans.localScale = Vector3.one;
                trans.anchoredPosition = new Vector2(0f, 0f); // setting position, will be on center
                trans.sizeDelta = new Vector2(32f, 32f); // custom size
                trackLogo = trans;

                Image image = imgObject.AddComponent<Image>();
                image.sprite = CommonAPI.TextureUtility.LoadSprite(Path.Combine(MusicCuratorPlugin.Instance.Directory, "MC-Note.png"));
                imgObject.transform.SetParent(trackLabel.transform);
                trackLogoImage = image;
            }
            
        }
    }

    [HarmonyPatch(typeof(MusicPlayer))]
    internal class MusicPlayerPatches {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(MusicPlayer.EvaluateRepeatingMusicTrack))]
        public static void EVRPostfix(MusicPlayer __instance, ref bool __result) {
            if (MusicCuratorPlugin.skipping || (MCSettings.skipRepeatInPlaylists.Value && MusicCuratorPlugin.playlistTracks.Contains(__instance.musicTrackQueue.CurrentMusicTrack))) { __result = false; }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(MusicPlayer.PlayNext))]
        public static void PlayNextPostfix_OverrideNextTrack(MusicPlayer __instance) {
            // all tracks excluded 
            if (MusicCuratorPlugin.AllUnlockedTracksExcluded()) {
                if (!(MusicCuratorPlugin.playlistTracks.Any() && MCSettings.playlistTracksNoExclude.Value)) {
                    __instance.ForcePaused();
                }
            }

            // looping single track
            else if (MusicCuratorPlugin.loopingSingleTrackIndex >= 0) {
                MusicTrack nextTrack = __instance.GetMusicTrack(MusicCuratorPlugin.loopingSingleTrackIndex);
                if (!MusicCuratorPlugin.IsInvalidTrack(nextTrack)) { MusicCuratorPlugin.PlayTrack(nextTrack); }
                else { // stop looping single track
                    MusicCuratorPlugin.loopingSingleTrackIndex = -1;
                    MusicCuratorPlugin.SetAppShuffle(true);
                }
            }

            // has next tracks queued
            else if (MusicCuratorPlugin.queuedTracks.Any()) { 
                MusicTrack nextTrack = MusicCuratorPlugin.queuedTracks[0];
                MusicCuratorPlugin.PlayTrack(nextTrack);

                if (MusicCuratorPlugin.queuedTracks.Count > 1) {
                    MusicCuratorPlugin.queuedTracks.RemoveAt(0);
                } else { MusicCuratorPlugin.queuedTracks.Clear(); }
            } 

            // has playlist tracks
            else if (MusicCuratorPlugin.playlistTracks.Any()) {
                MusicTrack nextTrack = MusicCuratorPlugin.playlistTracks[0];
                
                // if can't play this track...
                bool cantPlay = MusicCuratorPlugin.IsInvalidTrack(nextTrack) || !__instance.musicTrackQueue.currentMusicTracks.Contains(nextTrack) || (!MCSettings.playlistTracksNoExclude.Value && MusicCuratorPlugin.TrackIsExcluded(nextTrack));
                if (cantPlay) {
                    // try to find valid track in playlist
                    var i = 0;
                    while (cantPlay && i <= MusicCuratorPlugin.playlistTracks.Count) {
                        i++;
                        MusicCuratorPlugin.playlistTracks.RemoveAt(0);
                        MusicCuratorPlugin.playlistTracks.Add(nextTrack);
                        nextTrack = MusicCuratorPlugin.playlistTracks[0];
                        cantPlay = MusicCuratorPlugin.IsInvalidTrack(nextTrack) || !__instance.musicTrackQueue.currentMusicTracks.Contains(nextTrack) || (!MCSettings.playlistTracksNoExclude.Value && MusicCuratorPlugin.TrackIsExcluded(nextTrack));
                    }
                    
                    // if failed to find valid track, stop playing the playlist and give up
                    if (i > MusicCuratorPlugin.playlistTracks.Count) {
                        MusicCuratorPlugin.playlistTracks.Clear();
                        MusicCuratorPlugin.currentPlaylistIndex = -1;
                        return;
                    }
                }

                MusicCuratorPlugin.PlayTrack(nextTrack);   
                MusicCuratorPlugin.playlistTracks.RemoveAt(0);
                MusicCuratorPlugin.playlistTracks.Add(nextTrack);
                // re-shuffle music tracks if need be
                if (nextTrack == MusicCuratorPlugin.playlistStartingTrack && MCSettings.reshuffleOnLoop.Value && MusicCuratorPlugin.musicPlayer.shuffle) {
                    MusicCuratorPlugin.LoadPlaylistIntoQueue(MusicCuratorPlugin.currentPlaylistIndex, MusicCuratorPlugin.shufflingPlaylist);
                    if (MusicCuratorPlugin.playlistTracks[0] == nextTrack) {
                        MusicCuratorPlugin.playlistTracks.RemoveAt(0);
                        MusicCuratorPlugin.playlistTracks.Add(nextTrack);
                    }
                }
            }

            // looping event song
            else if (MusicCuratorPlugin.skipping && MusicCuratorPlugin.previousTrack == __instance.GetMusicTrack(__instance.CurrentTrackIndex) &&
            MCSettings.unlockEncounterMusic.Value) { 
                MusicCuratorPlugin.player.phone.GetAppInstance<AppMusicPlayer>().OnAppInit();
                MusicCuratorPlugin.player.phone.GetAppInstance<AppMusicPlayer>().OnAppEnable();
                MusicCuratorPlugin.player.phone.GetAppInstance<AppMusicPlayer>().PlaySong(1);
                MusicCuratorPlugin.SkipCurrentTrack();
                MusicCuratorPlugin.Log.LogInfo("Skipped encounter track (hopefully)");
            }

            // strict blocklisting mode
            /*else if (MusicCuratorPlugin.TrackIsExcluded(__instance.musicTrackQueue.CurrentMusicTrack)) {
                __instance.ForcePaused();
            }*/

            // handle excluded tracks (moved to trackqueue)
            //else if (MusicCuratorPlugin.TrackIsExcluded(__instance.musicTrackQueue.CurrentMusicTrack) && !MusicCuratorPlugin.ContinuingStageTrack) {
            //    MusicCuratorPlugin.SkipCurrentTrack();
            //} 
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(MusicPlayer.PlayFrom))]
        public static void PlayFromPostfix_PlaylistHandling(int index, int playbackSamples, MusicPlayer __instance) {
            // clear the queue if the player chose this track manually
            if (!MusicCuratorPlugin.ContinuingStageTrack && MusicCuratorPlugin.PlayerUsingMusicApp()) { 
                MusicCuratorPlugin.queuedTracks.Clear();
                MusicCuratorPlugin.playlistTracks.Clear();
                MusicCuratorPlugin.currentPlaylistIndex = -1;
            //} else {
                //MusicCuratorPlugin.ContinuingStageTrack = false;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(MusicPlayer.SetShuffle))]
        public static void SetShufflePostfix_PlaylistShuffle(bool value, MusicPlayer __instance) {
            if (value != MusicCuratorPlugin.shufflingPlaylist) {
                MusicCuratorPlugin.shufflingPlaylist = value;
                MusicCuratorPlugin.ReorderPlaylistInQueue(value);
            }
        }
    }

    [HarmonyPatch(typeof(MusicTrackQueue))]
    internal class MusicQueuePatches {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(MusicTrackQueue.ClearTracks))] // find out missing stage mixtape, and add it if using brr
        public static void ClearTracksPostfix_AllMixtapes(MusicTrackQueue __instance) {
            if (!MCSettings.allMixtapes.Value) { return; }
            MusicTrack hideoutTrack = Core.Instance.AudioManager.MusicLibraryPlayer.GetMusicTrackByID(MusicTrackID.Hideout_Mixtape);
            MusicTrack chapterTrack = Core.Instance.chapterMusic.GetChapterMusic(Story.GetCurrentObjectiveInfo().chapter);
            MusicCuratorPlugin.missingStageTrack = Reptile.Utility.GetCurrentStage() == Stage.hideout ? chapterTrack : hideoutTrack;
            if (PlaylistSaveData.excludedTracksCarryOver.Contains(MusicCuratorPlugin.TrackToSongID(MusicCuratorPlugin.missingStageTrack))) { 
                MusicCuratorPlugin.excludedTracks.Add(MusicCuratorPlugin.missingStageTrack); 
                //MusicCuratorPlugin.SaveExclusions();
            }
            
            if (MusicCuratorPlugin.hasBRR && MusicCuratorPlugin.missingStageTrack != null) { 
                BRRHelper.AddMissingTrackToAudios(MusicCuratorPlugin.missingStageTrack); 
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(MusicTrackQueue.UpdateMusicQueueForStage))] // adding missing stage mixtake (non-brr version)
        public static bool RefreshingPrefix_AllMixtapes(MusicTrack trackToPlay, MusicTrackQueue __instance) {
            if (MusicCuratorPlugin.missingStageTrack == null || MusicCuratorPlugin.hasBRR || __instance.currentMusicTracks.Contains(MusicCuratorPlugin.missingStageTrack) || !MCSettings.allMixtapes.Value) { return true; }

            __instance.currentMusicTracks.Add(MusicCuratorPlugin.missingStageTrack);
            if (PlaylistSaveData.excludedTracksCarryOver.Contains(MusicCuratorPlugin.TrackToSongID(MusicCuratorPlugin.missingStageTrack))) { 
                MusicCuratorPlugin.excludedTracks.Add(MusicCuratorPlugin.missingStageTrack); 
                //MusicCuratorPlugin.SaveExclusions();
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(MusicTrackQueue.SelectNextTrack))]
        public static bool NTPrefix_SkipBlocklisted(MusicTrackQueue __instance) {
            if (__instance.AmountOfTracks <= 0) { return false; }
            int nextInQueue = 0;
            int i = 0;
            int runs = 0;
            bool solved = false;

            while (runs < __instance.indexQueue.QueueCount)
            {
                runs++;
                i++;
                if (i + __instance.indexQueue.indexQueue.IndexOf(__instance.currentTrackIndex) >= __instance.indexQueue.indexQueue.Count) {
                    i = 0 - __instance.indexQueue.indexQueue.IndexOf(__instance.currentTrackIndex);
                }
                nextInQueue = __instance.indexQueue.GetNextInQueue(__instance.currentTrackIndex, i);
                
                bool dontSkipPlaylistTrack = MusicCuratorPlugin.currentPlaylistIndex != -1 && MusicCuratorPlugin.playlistTracks.Contains(__instance.GetMusicTrack(nextInQueue)) && MCSettings.playlistTracksNoExclude.Value; 
                if (!MusicCuratorPlugin.TrackIsExcluded(__instance.GetMusicTrack(nextInQueue)) && !dontSkipPlaylistTrack)
                {
                    solved = true;
                    break;
                }
            }
            
            if (!solved) { nextInQueue = __instance.indexQueue.GetNextInQueue(__instance.currentTrackIndex, 1); }
            int previousInQueue = __instance.indexQueue.GetPreviousInQueue(__instance.currentTrackIndex, (int)__instance.nPreviousTracksBuffered);
            __instance.EvaluateNextTrack(nextInQueue);
            __instance.EvaluateTrackToUnload(previousInQueue);
            return false;
        }
    }

    [HarmonyPatch(typeof(MusicPlayerTrackButton))]
    internal class TrackButtonPatches {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(MusicPlayerTrackButton.ConstantUpdate))]
        public static void ColorUpdatePostfix(MusicPlayerTrackButton __instance) {
            if (!__instance.IsHidden) { 
                MusicCuratorPlugin.UpdateButtonColor(__instance); 
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(MusicPlayerTrackButton.SetMusicApp))]
        public static void SetupPostfix_CreateLabel(MusicPlayerTrackButton __instance) {
            if (MCSettings.enableQueueVisual.Value) {
                TextMeshProUGUI queuePosLabel = __instance.m_TitleLabel.GetComponentsInChildren<TextMeshProUGUI>().LastOrDefault();
                if (queuePosLabel == null || queuePosLabel == __instance.m_TitleLabel) {
                    // Create queue number label
                    TextMeshProUGUI newLabel = UnityEngine.Object.Instantiate(__instance.m_TitleLabel, __instance.m_TitleLabel.transform);
                    newLabel.margin = new Vector4 (0,0,0,0);
                    newLabel.alignment = TextAlignmentOptions.Center;
                    newLabel.text = "";
                    newLabel.fontSize *= 2f;
                    newLabel.transform.position = __instance.m_Disc.transform.position;
                }
            }
        }
    }

    [HarmonyPatch(typeof(AppMusicPlayer))]
    internal class MusicAppPatches {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(AppMusicPlayer.OnAppDisable))]
        public static void OnMusicAppDisable_SaveExclusions() {
            MusicCuratorPlugin.SaveExclusions();
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(AppMusicPlayer.ToggleShuffle))]
        public static bool ToggleShuffle_LoopSingleTrack(ref bool value, AppMusicPlayer __instance) {
            // if player is changing value manually (allow for regular toggling in other apps, or automatically, without butting in)...
            if (value != __instance.GameMusicPlayer.GetShuffle() && MusicCuratorPlugin.PlayerUsingMusicApp()) {
                // if looping track and player hits right, stop looping track and go back to regualr loop
                if (MusicCuratorPlugin.loopingSingleTrackIndex >= 0 && value == true) {
                    MusicCuratorPlugin.loopingSingleTrackIndex = -1;
                    value = false;
                } 
                // if shuffling and player hits right, start looping track
                else if (value == false && __instance.GameMusicPlayer.GetShuffle() == true) {
                    MusicCuratorPlugin.loopingSingleTrackIndex = __instance.GameMusicPlayer.CurrentTrackIndex;
                }
            }
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(AppMusicPlayer.SetShuffleIconImage))]
        public static void SetShuffleIconImagePostfix(AppMusicPlayer __instance) {
            if (MusicCuratorPlugin.loopingSingleTrackIndex >= 0) {
                __instance.shuffleIconImageSelected.sprite = (__instance.shuffleIconImageUnselected.sprite = (MusicCuratorPlugin.loopingSingleTrackSprite));
            }
        }
    }

    [HarmonyPatch(typeof(StageManager))]
    internal class SMPatches {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(StageManager), "StartMusicForStage", new Type[] { typeof(Stage), typeof(int) })]
        public static bool SMFSPrefix(ref Stage stage, int playbackSamples, StageManager __instance) {
            MusicCuratorPlugin.ContinuingStageTrack = true;
            //MusicAppPatches.stageManager = __instance;
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StageManager), "StartMusicForStage", new Type[] { typeof(Stage), typeof(int) })]
        [HarmonyPriority(Priority.Low)] 
        public static void SMFSPostfix(Stage stage, int playbackSamples, StageManager __instance) {
            MusicCuratorPlugin.ContinuingStageTrack = false;
            MusicCuratorPlugin.LoadPlaylists(PlaylistSaveData.playlists);

            bool skip = MusicCuratorPlugin.TrackIsExcluded((__instance.musicPlayer as MusicPlayer).musicTrackQueue.CurrentMusicTrack);
            bool resetAppShuffle = false;
            bool ogAppShuffle = (__instance.musicPlayer as MusicPlayer).shuffle;

            if (MCSettings.instantShuffle.Value && (!MusicCuratorPlugin.hasInstantShuffledAlready || MCSettings.alwaysInstantShuffle.Value)) {
                skip = true;
                if (MusicCuratorPlugin.hasInstantShuffledAlready) { 
                    resetAppShuffle = true; 
                }
                MusicCuratorPlugin.SetAppShuffle(true);
                MusicCuratorPlugin.hasInstantShuffledAlready = true;
            }
            
            if (skip) { 
                MusicCuratorPlugin.SkipCurrentTrack(); 
                (__instance.musicPlayer as MusicPlayer).playbackSamples = 0;
            }
            MusicCuratorPlugin.LoadExclusions();
            if (resetAppShuffle) { MusicCuratorPlugin.SetAppShuffle(ogAppShuffle); }
        }
    }

    [HarmonyPatch(typeof(BaseModule))]
    internal class ReloadMainMenu {
        [HarmonyPatch(nameof(BaseModule.LoadMainMenuScene))]
        [HarmonyPostfix]
        static void LoadingMenu_ResetVars() {
            MusicCuratorPlugin.resetVariables(); 
        }
    }

}