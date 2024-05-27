using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommonAPI;
using CommonAPI.Phone;
using Reptile;
using UnityEngine;

// this code is a MESS. please don't look at it

namespace MusicCurator
{
    public class TracksHeaderApp : CustomApp {
        public UnityEngine.Transform overlayInstance; 
        // Allow us to save overlay so we can delete it later
        // useful for making headers with playlist/track names
        // code nearly entirely copy-pasted from CommonAPI github. thanks lazy duchess!
        // TODO: think about just hiding the overlay and updating the text, rather than deleting and re-making it every time
        public void CreateAndSaveIconlessTitleBar(string title, float fontSize = 80f) {
            var newOverlay = GameObject.Instantiate(MyPhone.GetAppInstance<Reptile.Phone.AppGraffiti>().transform.Find("Overlay"));
            var icons = newOverlay.transform.Find("Icons");
            Destroy(icons.Find("GraffitiIcon").gameObject);
            var header = icons.Find("HeaderLabel");
            header.localPosition = new Vector3(140f, header.localPosition.y, header.localPosition.z);
            Component.Destroy(header.GetComponent<TMProLocalizationAddOn>());
            var tmpro = header.GetComponent<TMPro.TextMeshProUGUI>(); 
            tmpro.text = title; // overlayInstance.transform.Find("Icons").Find("HeaderLabel").GetComponent<TMPro.TextMeshProUGUI>().text = title;
            tmpro.fontSize = fontSize;
            tmpro.fontSizeMax = fontSize;
            //tmpro.fontSizeMin = fontSize;
            tmpro.enableAutoSizing = true;
            newOverlay.SetParent(transform, false);

            overlayInstance = newOverlay;
        }
    }

    public class AppPlaylists : CustomApp
    {
        private static Sprite IconSprite = null;

        // Load the icon for this app and register it with the PhoneAPI, so that it shows up on the homescreen.
        public static void Initialize()
        {
            IconSprite = TextureUtility.LoadSprite(Path.Combine(MusicCuratorPlugin.Instance.Directory, "MC-PlaylistIcon.png")); // same place as plugin dll - keep in mind for thunderstore
            //IconSprite.texture.filterMode = FilterMode.Point;
            PhoneAPI.RegisterApp<AppPlaylists>("playlists", IconSprite);
        }

        public override void OnReleaseLeft() 
        {
            MyPhone.ReturnToHome(); // ignore any previous app history
        }

        public override void OnAppEnable()
        {
            MusicCuratorPlugin.ClearEmptyPlaylists();
            MusicCuratorPlugin.LoadPlaylists(PlaylistSaveData.playlists);
            MusicCuratorPlugin.selectedPlaylist = -1;

            // Create and setup buttons
            var nextButton = PhoneUIUtility.CreateSimpleButton("Create new playlist...");
            nextButton.OnConfirm += () => {
                //MusicCuratorPlugin.selectedPlaylist = MusicCuratorPlugin.CreatePlaylist(); 
                MyPhone.OpenApp(typeof(AppNewPlaylist));
            };
            ScrollView.AddButton(nextButton);

            nextButton = PhoneUIUtility.CreateSimpleButton("Manage queue/blocklist...");
            nextButton.OnConfirm += () => {
                MyPhone.OpenApp(typeof(AppManageQueueAndExclusions));
            };
            ScrollView.AddButton(nextButton);

            foreach (int playlistName in Enumerable.Range(0, MusicCuratorPlugin.playlists.Count).ToArray()) {
                string begin = MusicCuratorPlugin.currentPlaylistIndex == playlistName ? "> " : "";
                string end = MusicCuratorPlugin.PlaylistAnyInvalidTracks(playlistName) ? " (!)" : ""; 
                nextButton = PhoneUIUtility.CreateSimpleButton(begin + MusicCuratorPlugin.GetPlaylistName(playlistName) + " (" + MusicCuratorPlugin.playlists[playlistName].Count + ")" + end);

                nextButton.OnConfirm += () => {
                    MusicCuratorPlugin.selectedPlaylist = playlistName;
                    MyPhone.OpenApp(typeof(AppSelectedPlaylist));
                };
                ScrollView.AddButton(nextButton);
            }

            base.OnAppEnable();
        }

        public override void OnAppDisable()
        {
            ScrollView.RemoveAllButtons();
            base.OnAppDisable();
        }

        public override void OnAppInit()
        {
            base.OnAppInit();
            CreateTitleBar("Playlists", IconSprite);
            ScrollView = PhoneScrollView.Create(this);

            MusicCuratorPlugin.selectedPlaylist = -1;
        }
    }

    public class AppManageQueueAndExclusions : TracksHeaderApp
    {
        //private static Sprite IconSprite = null;
        public override bool Available => false; // don't show in home screen

        // Load the icon for this app and register it with the PhoneAPI, so that it shows up on the homescreen.
        public static void Initialize()
        {
            //IconSprite = TextureUtility.LoadSprite(Path.Combine(MusicCuratorPlugin.Instance.Directory, "playlisticon.png")); // MAKE THIS TEXTURE
            PhoneAPI.RegisterApp<AppManageQueueAndExclusions>("queue/exclusions");
        }

        public override void OnAppEnable()
        {
            base.OnAppEnable();
        }

        public override void OnAppDisable()
        {
            //ScrollView.RemoveAllButtons();
            base.OnAppDisable();
        }

        public override void OnAppInit()
        {
            base.OnAppInit();
            CreateAndSaveIconlessTitleBar("Manage queue/ blocklist");
            ScrollView = PhoneScrollView.Create(this);

            // Create and setup buttons
            var nextButton = PhoneUIUtility.CreateSimpleButton("Clear queue");
            nextButton.OnConfirm += () => {
                MusicCuratorPlugin.playlistTracks.Clear();
                MusicCuratorPlugin.currentPlaylistIndex = -1;
                MusicCuratorPlugin.queuedTracks.Clear();
            };
            ScrollView.AddButton(nextButton);

            nextButton = PhoneUIUtility.CreateSimpleButton("Reset blocklist");
            nextButton.OnConfirm += () => {
                MusicCuratorPlugin.excludedTracks.Clear(); 
                MusicCuratorPlugin.SaveExclusions();
            };
            ScrollView.AddButton(nextButton);

            nextButton = PhoneUIUtility.CreateSimpleButton("Delete all playlists");
            nextButton.Label.faceColor = Color.red;
            nextButton.LabelSelectedColor = Color.red;
            nextButton.LabelUnselectedColor = Color.red;
            nextButton.OnConfirm += () => {
                MyPhone.OpenApp(typeof(AppDeleteAllPlaylists));
            };
            ScrollView.AddButton(nextButton);
        }
    }

    public class AppNewPlaylist : CustomApp
    {
        public override bool Available => false; // don't show in home screen

        //private static Sprite IconSprite = null;

        // Load the icon for this app and register it with the PhoneAPI, so that it shows up on the homescreen.
        public static void Initialize()
        {
            //IconSprite = TextureUtility.LoadSprite(Path.Combine(MusicCuratorPlugin.Instance.Directory, "playlisticon.png")); // MAKE THIS TEXTURE
            PhoneAPI.RegisterApp<AppNewPlaylist>("new playlist");
        }

        public override void OnAppEnable()
        {
            // Create and setup buttons
            if (MusicCuratorPlugin.queuedTracks.Any()) {
                var nextButtonA = PhoneUIUtility.CreateSimpleButton("New playlist from queue");
                nextButtonA.OnConfirm += () => {
                    if (MusicCuratorPlugin.queuedTracks.Any()) {
                        MusicCuratorPlugin.selectedPlaylist = MusicCuratorPlugin.CreatePlaylist(); 
                        foreach (MusicTrack qTracks in MusicCuratorPlugin.queuedTracks) {
                            MusicCuratorPlugin.playlists[MusicCuratorPlugin.selectedPlaylist].Add(qTracks);
                        }
                        MusicCuratorPlugin.SavePlaylists();
                        MusicCuratorPlugin.queuedTracks.Clear();
                        MusicCuratorPlugin.selectedPlaylist = -1;
                    }
                    MyPhone.OpenApp(typeof(AppPlaylists));
                };
                ScrollView.AddButton(nextButtonA);
            }
            
            var nextButton = PhoneUIUtility.CreateSimpleButton("Select playlist tracks");
            nextButton.OnConfirm += () => {
                MusicCuratorPlugin.selectedPlaylist = MusicCuratorPlugin.CreatePlaylist(); 
                AppPlaylistTracklist.creatingPlaylist = true;
                MyPhone.OpenApp(typeof(AppPlaylistTracklist));
            };
            ScrollView.AddButton(nextButton);

            nextButton = PhoneUIUtility.CreateSimpleButton("Cancel");
            nextButton.OnConfirm += () => {
                MyPhone.OpenApp(typeof(AppPlaylists));
            };
            ScrollView.AddButton(nextButton);

            base.OnAppEnable();
        }

        public override void OnAppDisable()
        {
            ScrollView.RemoveAllButtons();
            base.OnAppDisable();
        }

        public override void OnAppInit()
        {
            base.OnAppInit();
            CreateIconlessTitleBar("New Playlist");
            ScrollView = PhoneScrollView.Create(this);
        }
    }

    public class AppSelectedPlaylist : TracksHeaderApp
    {
        public override bool Available => false; // don't show in home screen

        //private static Sprite IconSprite = null;

        // Load the icon for this app and register it with the PhoneAPI, so that it shows up on the homescreen.
        public static void Initialize()
        {
            //IconSprite = TextureUtility.LoadSprite(Path.Combine(MusicCuratorPlugin.Instance.Directory, "playlisticon.png")); // MAKE THIS TEXTURE
            PhoneAPI.RegisterApp<AppSelectedPlaylist>("selected playlist");
        }

        public override void OnAppEnable()
        {
            CreateAndSaveIconlessTitleBar(MusicCuratorPlugin.GetPlaylistName(MusicCuratorPlugin.selectedPlaylist));

            bool playlistIsInvalid = MusicCuratorPlugin.PlaylistAllInvalidTracks(MusicCuratorPlugin.selectedPlaylist) || (!MCSettings.playlistTracksNoExclude.Value && MusicCuratorPlugin.PlaylistAllExcludedTracks(MusicCuratorPlugin.selectedPlaylist));

            // Create and setup buttons
            var nextButton = PhoneUIUtility.CreateSimpleButton("Shuffle and loop playlist");
            if (playlistIsInvalid) {
                nextButton.Label.faceColor = Color.red;
                nextButton.LabelSelectedColor = Color.red;
                nextButton.LabelUnselectedColor = Color.red;
            } else {
                nextButton.OnConfirm += () => {
                    MusicCuratorPlugin.SetAppShuffle(true);
                    MusicCuratorPlugin.shufflingPlaylist = true;
                    MusicCuratorPlugin.queuedTracks.Clear();
                    MusicCuratorPlugin.LoadPlaylistIntoQueue(MusicCuratorPlugin.selectedPlaylist, true);
                    
                    MusicCuratorPlugin.SkipCurrentTrack();
                    MyPhone.OpenApp(typeof(AppPlaylists));
                };
            }

            ScrollView.AddButton(nextButton);

            nextButton = PhoneUIUtility.CreateSimpleButton("Play and loop playlist");
            if (playlistIsInvalid) {
                nextButton.Label.faceColor = Color.red;
                nextButton.LabelSelectedColor = Color.red;
                nextButton.LabelUnselectedColor = Color.red;
            } else {
            nextButton.OnConfirm += () => {
                MusicCuratorPlugin.SetAppShuffle(false);
                MusicCuratorPlugin.shufflingPlaylist = false;
                MusicCuratorPlugin.queuedTracks.Clear();
                MusicCuratorPlugin.LoadPlaylistIntoQueue(MusicCuratorPlugin.selectedPlaylist, false);
                
                MusicCuratorPlugin.SkipCurrentTrack();
                MyPhone.OpenApp(typeof(AppPlaylists));
            };
            }
            ScrollView.AddButton(nextButton);

            nextButton = PhoneUIUtility.CreateSimpleButton("Edit playlist");
            nextButton.OnConfirm += () => {
                MyPhone.OpenApp(typeof(AppEditPlaylist));
            };
            ScrollView.AddButton(nextButton);

            // if playlist in queue, remove instead
            if (!MusicCuratorPlugin.ListAInB(MusicCuratorPlugin.playlists[MusicCuratorPlugin.selectedPlaylist], MusicCuratorPlugin.queuedTracks)) {
                nextButton = PhoneUIUtility.CreateSimpleButton("Add playlist to queue");
                if (playlistIsInvalid) {
                    nextButton.Label.faceColor = Color.red;
                    nextButton.LabelSelectedColor = Color.red;
                    nextButton.LabelUnselectedColor = Color.red;
                } else {
                nextButton.OnConfirm += () => {
                    MusicCuratorPlugin.queuedTracks.AddRange(MusicCuratorPlugin.playlists[MusicCuratorPlugin.selectedPlaylist]); 
                    MyPhone.OpenApp(typeof(AppPlaylists));
                }; 
                }
            } else {
                nextButton = PhoneUIUtility.CreateSimpleButton("Remove playlist from queue");
                if (playlistIsInvalid) {
                    nextButton.Label.faceColor = Color.red;
                    nextButton.LabelSelectedColor = Color.red;
                    nextButton.LabelUnselectedColor = Color.red;
                } else {
                nextButton.OnConfirm += () => {
                    MusicCuratorPlugin.queuedTracks = MusicCuratorPlugin.queuedTracks.Except(MusicCuratorPlugin.playlists[MusicCuratorPlugin.selectedPlaylist]).ToList(); 
                    MyPhone.OpenApp(typeof(AppPlaylists));
                }; 
                }
            }
            ScrollView.AddButton(nextButton);

            // if playlist in exclusions, remove instead
            if (!MusicCuratorPlugin.ListAInB(MusicCuratorPlugin.playlists[MusicCuratorPlugin.selectedPlaylist], MusicCuratorPlugin.excludedTracks)) {
                nextButton = PhoneUIUtility.CreateSimpleButton("Add playlist to blocklist");
                nextButton.OnConfirm += () => {
                    MusicCuratorPlugin.excludedTracks.AddRange(MusicCuratorPlugin.playlists[MusicCuratorPlugin.selectedPlaylist]); 
                    if (MusicCuratorPlugin.currentPlaylistIndex == MusicCuratorPlugin.selectedPlaylist) {
                        MusicCuratorPlugin.currentPlaylistIndex = -1;
                        MusicCuratorPlugin.playlistTracks.Clear();
                        MusicCuratorPlugin.SkipCurrentTrack();
                    }
                    MusicCuratorPlugin.SaveExclusions();
                    MyPhone.OpenApp(typeof(AppPlaylists));
                };
            } else {
                nextButton = PhoneUIUtility.CreateSimpleButton("Remove playlist from blocklist");
                nextButton.OnConfirm += () => {
                    MusicCuratorPlugin.excludedTracks = MusicCuratorPlugin.excludedTracks.Except(MusicCuratorPlugin.playlists[MusicCuratorPlugin.selectedPlaylist]).ToList(); 
                    MusicCuratorPlugin.SaveExclusions();
                    MyPhone.OpenApp(typeof(AppPlaylists));
                };
            }
            ScrollView.AddButton(nextButton);

            nextButton = PhoneUIUtility.CreateSimpleButton("Delete playlist");
            nextButton.OnConfirm += () => {
                MyPhone.OpenApp(typeof(AppDeletePlaylist));
            };
            ScrollView.AddButton(nextButton);

            nextButton = PhoneUIUtility.CreateSimpleButton("Cancel");
            nextButton.OnConfirm += () => {
                MyPhone.OpenApp(typeof(AppPlaylists));
            };
            ScrollView.AddButton(nextButton);

            base.OnAppEnable();
        }

        public override void OnAppDisable()
        {
            ScrollView.RemoveAllButtons();
            base.OnAppDisable();
            Destroy(overlayInstance.gameObject);
        }

        public override void OnAppInit()
        {
            base.OnAppInit();
            ScrollView = PhoneScrollView.Create(this);
        }
    }

    public class AppEditPlaylist : TracksHeaderApp
    {
        public override bool Available => false; // don't show in home screen

        //private static Sprite IconSprite = null;

        // Load the icon for this app and register it with the PhoneAPI, so that it shows up on the homescreen.
        public static void Initialize()
        {
            //IconSprite = TextureUtility.LoadSprite(Path.Combine(MusicCuratorPlugin.Instance.Directory, "playlisticon.png")); // MAKE THIS TEXTURE
            PhoneAPI.RegisterApp<AppEditPlaylist>("edit playlist");
        }

        public override void OnAppEnable()
        {
            CreateAndSaveIconlessTitleBar("Edit " + MusicCuratorPlugin.GetPlaylistName(MusicCuratorPlugin.selectedPlaylist));

            MusicCuratorPlugin.ReorderPlaylistInQueue(MusicCuratorPlugin.musicPlayer.shuffle);

            // Create and setup buttons
            var nextButton = PhoneUIUtility.CreateSimpleButton("Add tracks...");
            nextButton.OnConfirm += () => {
                MyPhone.OpenApp(typeof(AppPlaylistTracklist));
            };
            ScrollView.AddButton(nextButton);

            nextButton = PhoneUIUtility.CreateSimpleButton("Reorder tracks...");
            nextButton.OnConfirm += () => {
                MyPhone.OpenApp(typeof(AppReorderPlaylist));
            };
            ScrollView.AddButton(nextButton);

            foreach (MusicTrack mpTrack in MusicCuratorPlugin.playlists[MusicCuratorPlugin.selectedPlaylist]) {
                string IDDisplay = mpTrack.Artist + " - " + mpTrack.Title;
                //string trueID = MusicCuratorPlugin.TrackToSongID(mpTrack);
                nextButton = PhoneUIUtility.CreateSimpleButton(IDDisplay);

                if (MusicCuratorPlugin.IsInvalidTrack(mpTrack)) {
                    nextButton.Label.faceColor = Color.red;
                    nextButton.LabelSelectedColor = Color.red;
                    nextButton.LabelUnselectedColor = Color.red;
                }
                
                nextButton.OnConfirm += () => {
                    //if (MusicCuratorPlugin.IsInvalidTrack(MusicCuratorPlugin.FindTrackBySongID(trueID, false))) { return; }
                    //MusicCuratorPlugin.appSelectedTrack = MusicCuratorPlugin.FindTrackBySongID(trueID);
                    MusicCuratorPlugin.appSelectedTrack = MusicCuratorPlugin.playlists[MusicCuratorPlugin.selectedPlaylist].IndexOf(mpTrack);
                    MyPhone.OpenApp(typeof(AppRemoveTrackFromPlaylist));
                };
                ScrollView.AddButton(nextButton);
            }

            base.OnAppEnable();
            ScrollView.ScrollUp();
        }

        public override void OnAppDisable()
        {
            ScrollView.RemoveAllButtons();
            Destroy(overlayInstance.gameObject);

            if (!MusicCuratorPlugin.playlists[MusicCuratorPlugin.selectedPlaylist].Any()) {
                MusicCuratorPlugin.selectedPlaylist = -1;
            }
        }
        
        public override void OnReleaseLeft()
        {
            if (!MusicCuratorPlugin.playlists[MusicCuratorPlugin.selectedPlaylist].Any()) {
                //MusicCuratorPlugin.playlists[MusicCuratorPlugin.selectedPlaylist]
                MusicCuratorPlugin.selectedPlaylist = -1;
                MyPhone.OpenApp(typeof(AppPlaylists));
            } else {
                base.OnReleaseLeft();
            }
        }

        public override void OnAppInit()
        {
            base.OnAppInit();
            ScrollView = PhoneScrollView.Create(this);
            
        }
    }

    public class AppRemoveTrackFromPlaylist : TracksHeaderApp
    {
        public override bool Available => false; // don't show in home screen

        //private static Sprite IconSprite = null;

        // Load the icon for this app and register it with the PhoneAPI, so that it shows up on the homescreen.
        public static void Initialize()
        {
            //IconSprite = TextureUtility.LoadSprite(Path.Combine(MusicCuratorPlugin.Instance.Directory, "playlisticon.png")); // MAKE THIS TEXTURE
            PhoneAPI.RegisterApp<AppRemoveTrackFromPlaylist>("remove from playlist");
        }

        public override void OnAppEnable()
        {
            CreateAndSaveIconlessTitleBar("Edit " + MusicCuratorPlugin.GetPlaylistName(MusicCuratorPlugin.selectedPlaylist));
            MusicTrack selectedMusicTrack = MusicCuratorPlugin.playlists[MusicCuratorPlugin.selectedPlaylist][MusicCuratorPlugin.appSelectedTrack];

            // Create and setup buttons
            var nextButton = PhoneUIUtility.CreateSimpleButton("Remove track " + selectedMusicTrack.Title + " from playlist");
            nextButton.OnConfirm += () => {
                MusicCuratorPlugin.playlists[MusicCuratorPlugin.selectedPlaylist].RemoveAt(MusicCuratorPlugin.appSelectedTrack);
                MusicCuratorPlugin.appSelectedTrack = -1;

                if (!MusicCuratorPlugin.playlists[MusicCuratorPlugin.selectedPlaylist].Any()) {
                    MusicCuratorPlugin.selectedPlaylist = -1;
                    MyPhone.OpenApp(typeof(AppPlaylists));
                } else {
                    MusicCuratorPlugin.ReorderPlaylistInQueue(MusicCuratorPlugin.musicPlayer.shuffle);
                    MyPhone.OpenApp(typeof(AppEditPlaylist));
                    MyPhone.m_PreviousApps.Pop();
                    MyPhone.m_PreviousApps.Pop();
                }

                MusicCuratorPlugin.SavePlaylists(); 
            };
            ScrollView.AddButton(nextButton);

            nextButton = PhoneUIUtility.CreateSimpleButton("Cancel");
            nextButton.OnConfirm += () => {
                MusicCuratorPlugin.appSelectedTrack = -1;
                MyPhone.OpenApp(typeof(AppEditPlaylist));
                MyPhone.m_PreviousApps.Pop();
                MyPhone.m_PreviousApps.Pop();
            };
            ScrollView.AddButton(nextButton);

            base.OnAppEnable();
            //MyPhone.m_PreviousApps.Pop();
        }

        public override void OnAppDisable()
        {
            ScrollView.RemoveAllButtons();
            Destroy(overlayInstance.gameObject);
            base.OnAppDisable();
            //MyPhone.m_PreviousApps.Pop();
        }

        public override void OnAppInit()
        {
            base.OnAppInit();
            ScrollView = PhoneScrollView.Create(this);
            
        }
    }

    public class AppPlaylistTracklist : CustomApp
    {
        public override bool Available => false; // don't show in home screen

        //private static Sprite IconSprite = null;

        public static List<MusicTrack> selectedTracksToAddToPlaylist = new List<MusicTrack>();

        public static Color LabelSelectedColorDefault = new Color32(49, 90, 165, 255);
        public static Color LabelUnselectedColorDefault = Color.white;

        public static bool creatingPlaylist = false;

        // Load the icon for this app and register it with the PhoneAPI, so that it shows up on the homescreen.
        public static void Initialize()
        {
            //IconSprite = TextureUtility.LoadSprite(Path.Combine(MusicCuratorPlugin.Instance.Directory, "playlisticon.png")); // MAKE THIS TEXTURE
            PhoneAPI.RegisterApp<AppPlaylistTracklist>("playlist tracklist");
        }

        public override void OnAppEnable()
        {
            // Create and setup buttons
            foreach (MusicTrack mpTrack in MusicCuratorPlugin.GetAllMusic()) {
                if (MusicCuratorPlugin.playlists[MusicCuratorPlugin.selectedPlaylist].Contains(mpTrack)) { continue; }
                
                MusicCuratorPlugin.Log.LogInfo("Tracklist App: Adding " + mpTrack.Artist + " - " + mpTrack.Title);
                var nextButton = PhoneUIUtility.CreateSimpleButton(mpTrack.Artist + " - " + mpTrack.Title);
                nextButton.OnConfirm += () => {
                    if (selectedTracksToAddToPlaylist.Contains(mpTrack)) {
                        selectedTracksToAddToPlaylist.Remove(mpTrack);
                        nextButton.Label.faceColor = LabelSelectedColorDefault;
                        nextButton.LabelSelectedColor = LabelSelectedColorDefault;
                        nextButton.LabelUnselectedColor = LabelUnselectedColorDefault;
                    } else {
                        selectedTracksToAddToPlaylist.Add(mpTrack);
                        nextButton.Label.faceColor = Color.green;
                        nextButton.LabelSelectedColor = Color.green;
                        nextButton.LabelUnselectedColor = Color.green;
                    }
                };
                ScrollView.AddButton(nextButton);
            }
            selectedTracksToAddToPlaylist.Clear();
            base.OnAppEnable();
            ScrollView.ScrollUp();
        }

        public override void OnAppDisable()
        {
            creatingPlaylist = false;
            ScrollView.RemoveAllButtons();
            selectedTracksToAddToPlaylist.Clear();
            base.OnAppDisable();
        }

        public override void OnReleaseLeft() 
        {
            //if (MusicCuratorPlugin.selectedPlaylist > MusicCuratorPlugin.playlists.Count - 1) {
            //    MusicCuratorPlugin.selectedPlaylist = MusicCuratorPlugin.CreatePlaylist();
            //}
            foreach (MusicTrack trackToAddToPlaylist in selectedTracksToAddToPlaylist) {
                MusicCuratorPlugin.playlists[MusicCuratorPlugin.selectedPlaylist].Add(trackToAddToPlaylist);
            }
            
            selectedTracksToAddToPlaylist.Clear();
            MusicCuratorPlugin.SavePlaylists(); // add this method
            MusicCuratorPlugin.ReorderPlaylistInQueue(MusicCuratorPlugin.musicPlayer.shuffle);
            
            if (creatingPlaylist) {
                creatingPlaylist = false;
                MyPhone.OpenApp(typeof(AppPlaylists));
            } else {
                base.OnReleaseLeft(); 
            }
        }

        public override void OnAppInit()
        {
            base.OnAppInit();
            CreateIconlessTitleBar("Tracklist");
            ScrollView = PhoneScrollView.Create(this);
            
        }
    }

    public class AppDeletePlaylist : TracksHeaderApp
    {
        public override bool Available => false; // don't show in home screen

        //private static Sprite IconSprite = null;

        // Load the icon for this app and register it with the PhoneAPI, so that it shows up on the homescreen.
        public static void Initialize()
        {
            //IconSprite = TextureUtility.LoadSprite(Path.Combine(MusicCuratorPlugin.Instance.Directory, "playlisticon.png")); // MAKE THIS TEXTURE
            PhoneAPI.RegisterApp<AppDeletePlaylist>("delete playlist");
        }

        public override void OnAppEnable()
        {
            CreateAndSaveIconlessTitleBar("Delete " + MusicCuratorPlugin.GetPlaylistName(MusicCuratorPlugin.selectedPlaylist));

            // Create and setup buttons
            var nextButton = PhoneUIUtility.CreateSimpleButton("Cancel");
            nextButton.OnConfirm += () => {
                MyPhone.OpenApp(typeof(AppSelectedPlaylist));
                MyPhone.m_PreviousApps.Pop();
                MyPhone.m_PreviousApps.Pop();
            };
            ScrollView.AddButton(nextButton);

            nextButton = PhoneUIUtility.CreateSimpleButton("Delete playlist (permanent!)");
            nextButton.Label.faceColor = Color.red;
            nextButton.LabelSelectedColor = Color.red;
            nextButton.LabelUnselectedColor = Color.red;
            
            nextButton.OnConfirm += () => {
                if (MusicCuratorPlugin.currentPlaylistIndex == MusicCuratorPlugin.selectedPlaylist) {
                    MusicCuratorPlugin.playlistTracks.Clear();
                    MusicCuratorPlugin.currentPlaylistIndex = -1;
                }
                MusicCuratorPlugin.playlists.RemoveAt(MusicCuratorPlugin.selectedPlaylist);
                MusicCuratorPlugin.selectedPlaylist = -1;
                MusicCuratorPlugin.appSelectedTrack = -1;
                
                MusicCuratorPlugin.SavePlaylists(); // add this method
                MyPhone.OpenApp(typeof(AppPlaylists));
                MyPhone.m_PreviousApps.Pop();
                MyPhone.m_PreviousApps.Pop();
            };
            ScrollView.AddButton(nextButton);

            base.OnAppEnable();
        }

        public override void OnAppDisable()
        {
            ScrollView.RemoveAllButtons();
            Destroy(overlayInstance.gameObject);
            base.OnAppDisable();
            //MyPhone.m_PreviousApps.Pop();
        }

        public override void OnAppInit()
        {
            base.OnAppInit();
            ScrollView = PhoneScrollView.Create(this);
            
        }
    }

    public class AppDeleteAllPlaylists : TracksHeaderApp
    {
        public override bool Available => false; // don't show in home screen

        //private static Sprite IconSprite = null;

        // Load the icon for this app and register it with the PhoneAPI, so that it shows up on the homescreen.
        public static void Initialize()
        {
            //IconSprite = TextureUtility.LoadSprite(Path.Combine(MusicCuratorPlugin.Instance.Directory, "playlisticon.png")); // MAKE THIS TEXTURE
            PhoneAPI.RegisterApp<AppDeleteAllPlaylists>("delete all playlist");
        }

        public override void OnAppEnable()
        {
            CreateAndSaveIconlessTitleBar("Are you sure?");

            var nextButton = PhoneUIUtility.CreateSimpleButton("Cancel");
            nextButton.OnConfirm += () => {
                MyPhone.OpenApp(typeof(AppManageQueueAndExclusions));
                MyPhone.m_PreviousApps.Pop();
                MyPhone.m_PreviousApps.Pop();
            };
            ScrollView.AddButton(nextButton);

            nextButton = PhoneUIUtility.CreateSimpleButton("Delete all playlists (permanent!)");
            nextButton.Label.faceColor = Color.red;
            nextButton.LabelSelectedColor = Color.red;
            nextButton.LabelUnselectedColor = Color.red;
            nextButton.OnConfirm += () => {
                MusicCuratorPlugin.playlists.Clear();
                PlaylistSaveData.playlists.Clear();
                MusicCuratorPlugin.currentPlaylistIndex = -1;
                MusicCuratorPlugin.SavePlaylists(); // add this method
                MyPhone.OpenApp(typeof(AppPlaylists));
            };
            ScrollView.AddButton(nextButton);

            base.OnAppEnable();
            //MyPhone.m_PreviousApps.Pop();
        }

        public override void OnAppDisable()
        {
            ScrollView.RemoveAllButtons();
            Destroy(overlayInstance.gameObject);
            base.OnAppDisable();
            //MyPhone.m_PreviousApps.Pop();
        }

        public override void OnAppInit()
        {
            base.OnAppInit();
            ScrollView = PhoneScrollView.Create(this);
            
        }
    }

    public class AppReorderPlaylist : TracksHeaderApp
    {
        public override bool Available => false; // don't show in home screen
        
        public static bool draggingTrack = false;  
        private static SimplePhoneButton selectedButton = null;

        //private static Sprite IconSprite = null;

        // Load the icon for this app and register it with the PhoneAPI, so that it shows up on the homescreen.
        public static void Initialize()
        {
            //IconSprite = TextureUtility.LoadSprite(Path.Combine(MusicCuratorPlugin.Instance.Directory, "playlisticon.png")); // MAKE THIS TEXTURE
            PhoneAPI.RegisterApp<AppReorderPlaylist>("reorder playlist");
        }

        public override void OnAppEnable()
        {
            CreateAndSaveIconlessTitleBar("Edit " + MusicCuratorPlugin.GetPlaylistName(MusicCuratorPlugin.selectedPlaylist));

            //MusicCuratorPlugin.ReorderPlaylistInQueue(MusicCuratorPlugin.musicPlayer.shuffle);

            // Create and setup buttons
            foreach (MusicTrack mpTrack in MusicCuratorPlugin.playlists[MusicCuratorPlugin.selectedPlaylist]) {
                string IDDisplay = mpTrack.Artist + " - " + mpTrack.Title;
                //string trueID = MusicCuratorPlugin.TrackToSongID(mpTrack);
                var nextButton = PhoneUIUtility.CreateSimpleButton(IDDisplay);

                if (MusicCuratorPlugin.IsInvalidTrack(mpTrack)) {
                    nextButton.Label.faceColor = Color.red;
                    nextButton.LabelSelectedColor = Color.red;
                    nextButton.LabelUnselectedColor = Color.red;
                }
                
                nextButton.OnConfirm += () => {
                    if (AppReorderPlaylist.draggingTrack) {
                        AppReorderPlaylist.draggingTrack = false; 
                        
                        int selfIndex = ScrollView.Buttons.IndexOf(nextButton);
                        int selectIndex = ScrollView.Buttons.IndexOf(AppReorderPlaylist.selectedButton);

                        AppReorderPlaylist.selectedButton.Label.faceColor = AppPlaylistTracklist.LabelSelectedColorDefault;
                        AppReorderPlaylist.selectedButton.LabelSelectedColor = AppPlaylistTracklist.LabelSelectedColorDefault;
                        AppReorderPlaylist.selectedButton.LabelUnselectedColor = AppPlaylistTracklist.LabelUnselectedColorDefault;

                        if (selfIndex != selectIndex) {
                            ScrollView.Buttons.RemoveAt(selectIndex);
                            ScrollView.InsertButton(selfIndex, AppReorderPlaylist.selectedButton);
                            ScrollView.UpdateButtons();
                            nextButton.PlayDeselectAnimation();

                            MusicTrack selectedTrack = MusicCuratorPlugin.playlists[MusicCuratorPlugin.selectedPlaylist][selectIndex];
                            MusicCuratorPlugin.playlists[MusicCuratorPlugin.selectedPlaylist].RemoveAt(selectIndex);
                            MusicCuratorPlugin.playlists[MusicCuratorPlugin.selectedPlaylist].Insert(selfIndex, selectedTrack);
                        }
                        //AppReorderPlaylist.selectedButton = null; 
                    } else { 
                        AppReorderPlaylist.draggingTrack = true; 
                        AppReorderPlaylist.selectedButton = nextButton;

                        nextButton.Label.faceColor = Color.green;
                        nextButton.LabelSelectedColor = Color.green;
                        nextButton.LabelUnselectedColor = Color.green;
                    }
                    
                };
                ScrollView.AddButton(nextButton);
            }

            base.OnAppEnable();
            ScrollView.ScrollUp();
        }

        public override void OnAppDisable()
        {
            ScrollView.RemoveAllButtons();
            Destroy(overlayInstance.gameObject);
            AppReorderPlaylist.draggingTrack = false; 

            if (!MusicCuratorPlugin.playlists[MusicCuratorPlugin.selectedPlaylist].Any()) {
                MusicCuratorPlugin.selectedPlaylist = -1;
            }
        }
        
        public override void OnReleaseLeft()
        {
            if (!MusicCuratorPlugin.playlists[MusicCuratorPlugin.selectedPlaylist].Any()) {
                //MusicCuratorPlugin.playlists[MusicCuratorPlugin.selectedPlaylist]
                MusicCuratorPlugin.selectedPlaylist = -1;
                MyPhone.OpenApp(typeof(AppPlaylists));
            } else {
                base.OnReleaseLeft();
            }
        }

        public override void OnAppInit()
        {
            base.OnAppInit();
            ScrollView = PhoneScrollView.Create(this);
            
        }
    }
}