using CommonAPI;
using Reptile;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace MusicCurator
{
    public class PlaylistSaveData : CustomSaveData
    {
        public static PlaylistSaveData Instance { get; private set; }
        
        public static List<List<string>> playlists = new List<List<string>>();
        public static List<string> defaultExclusions {
            get { return MCSettings.alwaysSkipMixtapes.Value ? new List<string> {"tryce-beats for the hideout", "dj cyber-locking up the funk mixtape", "dj cyber-house cats mixtape", "dj cyber-beastmode hip hop mixtape", "dj cyber-breaking machine mixtape", "dj cyber-sunshine popping mixtape"} : new List<string>(); }
        }
        public static List<string> excludedTracksCarryOver = new List<string>();
        
        public static readonly int saveVersion = 0; // current save file version (Write())
        private static int readSaveVersion = 0; // save version from file (Read())

        // save location: %localappdata%\Bomb Rush Cyberfunk Modding\MusicCurator\saves
        public PlaylistSaveData() : base("MusicCurator", "Playlists.Slot{0}.data", SaveLocations.LocalAppData)
        {
            Instance = this;
            AutoSave = false; // only enable autosave once we've loaded everything. done in LoadPlaylists();
            //excludedTracksCarryOver = defaultExclusions;
        }

        // Starting a new save - start from zero.
        public override void Initialize()
        {
            playlists.Clear();
            excludedTracksCarryOver = defaultExclusions;
        }

        public override void Read(BinaryReader reader)
        {
            playlists.Clear();
            excludedTracksCarryOver.Clear();
            MusicCuratorPlugin.playlists.Clear();
            MusicCuratorPlugin.excludedTracks.Clear();

            readSaveVersion = reader.ReadByte(); // save file version
            var howManyPlaylists = reader.ReadInt32(); // playlist count
            for(var i = 0; i < howManyPlaylists; i++) {
                var playlistIndex = reader.ReadInt32(); // playlist index
                var howManyTracksInThisPlaylist = reader.ReadInt32(); // individual playlist count

                List<string> newPlaylist = new List<string>();
                for(var z = 0; z < howManyTracksInThisPlaylist; z++) {
                    var trackIndex = reader.ReadInt32(); // track index
                    var trackID = reader.ReadString(); // songID
                    newPlaylist.Add(trackID); 
                }
                playlists.Add(newPlaylist);
            }
           
            var howManyExclusions = reader.ReadInt32(); // exclusions count
            for(var e = 0; e < howManyExclusions; e++) {
                var trackIndex = reader.ReadInt32(); // track index
                var trackID = reader.ReadString();
                excludedTracksCarryOver.Add(trackID); // songID
                //MusicCuratorPlugin.excludedTracks.Add()
            }
        }

        public override void Write(BinaryWriter writer)
        {
            writer.Write((byte)saveVersion); // save file version
            writer.Write((Int32)playlists.Count); // playlist count

            int i = -1;
            foreach(List<string> playlist in playlists)
            {
                i++;
                int z = -1;
                writer.Write((Int32)i); // playlist index
                writer.Write((Int32)playlist.Count); // individual playlist count
                foreach (string track in playlist) {
                    z++;
                    writer.Write((Int32)z); // track index
                    writer.Write((string)MusicCuratorPlugin.ConformSongID(track)); // songID
                }
            }

            writer.Write((Int32)excludedTracksCarryOver.Count); // exclusions count
            i = -1;
            foreach (string track in excludedTracksCarryOver) {
                i++;
                writer.Write((Int32)i); // track index
                writer.Write((string)MusicCuratorPlugin.ConformSongID(track)); // songID
            }
        }
    }
}