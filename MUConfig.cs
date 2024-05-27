using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace MusicCurator
{
    internal class MCSettings {
        public static ConfigEntry<bool> instantShuffle;
        public static ConfigEntry<bool> alwaysSkipMixtapes;
        public static ConfigEntry<bool> reshuffleOnLoop;
        public static ConfigEntry<string> customPlaylistNamesUnsplit;
        //public static ConfigEntry<string> autoExclusionsUnsplit;
        public static ConfigEntry<string> keybindsSkipUnsplit;
        public static ConfigEntry<string> keybindsQueueUnsplit;
        
        public static List<string> customPlaylistNames;
        public static List<KeyCode> keybindsQueue;
        public static List<KeyCode> keybindsSkip;
        
        public static ConfigEntry<string> keybindsShuffleUnsplit;
        public static List<KeyCode> keybindsShuffle;

        public static ConfigEntry<string> keybindsPauseUnsplit;
        public static List<KeyCode> keybindsPause;

        public static ConfigEntry<bool> allMixtapes;

        public static ConfigEntry<bool> playlistTracksNoExclude; 
        public static ConfigEntry<bool> skipRepeatInPlaylists; 


        public static void BindSettings(ConfigFile Config) {
            customPlaylistNamesUnsplit = Config.Bind(
                "1. Settings",          // The section under which the option is shown
                "Custom Playlist Names",     // The key of the configuration option in the configuration file
                "",    // The default value
                "List of replacement playlist names, seperated by commas. Spaces are allowed. Names are sorted from top to bottom in the playlist app (first name replaces New Playlist 1, second replaces New Playlist 2, etc.)"); // Description of the option 

            instantShuffle = Config.Bind(
                "1. Settings", "Instant Shuffle", true, "Instantly set the music app to shuffle all tracks on startup."
            );
            alwaysSkipMixtapes = Config.Bind(
                "1. Settings", "Skip Mixtapes By Default", true, "Automatically blocklist all mixtapes when starting a new save or resetting the blocklist."
            );
            allMixtapes = Config.Bind("1. Settings", "Always Load All Mixtapes", true, "Whether or not to add the Hideout mixtape in stages outside of the Hideout (or the Chapter 6 mixtape in the Hideout). Ensures playlists can include and play either track on all stages.");
            reshuffleOnLoop = Config.Bind(
                "1. Settings", "Reshuffle Playlist on Loop", true, "If true, \"Shuffle and play\" re-shuffles a shuffled playlist every time it loops. If false, \"Shuffle and play\" shuffles the playlist once before looping the shuffled queue."
            );

            playlistTracksNoExclude = Config.Bind("1. Settings", "Playlists Ignore Blocklist", true, "If true, playlists can play blocklisted tracks. If false, blocklisted tracks are always skipped, including those in a playlist.");
            skipRepeatInPlaylists = Config.Bind("1. Settings", "Playlists Ignore Repeatable Tracks", true, "If true, repeatable tracks (the stage mixtapes) don't loop if they're played within a playlist. If false, repeatable tracks will loop forever, even in a playlist. They must be manually skipped to advance in the playlist.");

            //autoExclusionsUnsplit = Config.Bind(
            //    "1. Settings",          // The section under which the option is shown
            //    "Automatic Exclusions",     // The key of the configuration option in the configuration file
            //    "DJ Cyber-, Tryce-",    // The default value
            //    "hi"); // Description of the option 
            
            keybindsSkipUnsplit = Config.Bind(
                "2. Keybinds",          // The section under which the option is shown
                "Skip Keybinds",     // The key of the configuration option in the configuration file
                "Semicolon, JoystickButton9",    // The default value
                "List of KeyCodes that can be pressed to skip/blocklist tracks, separated by commas."); // Description of the option 
            keybindsQueueUnsplit = Config.Bind(
                "2. Keybinds",          // The section under which the option is shown
                "Queue Keybinds",     // The key of the configuration option in the configuration file
                "Quote, JoystickButton8",    // The default value
                "List of KeyCodes that can be pressed to queue up tracks, separated by commas."); // Description of the option 

            keybindsShuffleUnsplit = Config.Bind("2. Keybinds", "Quick Toggle Shuffle Keybinds", "JoystickButton6, LeftBracket", "List of KeyCodes that can be pressed to quickly toggle shuffling, separated by commas. Only works if the music app is open.");
            keybindsPauseUnsplit = Config.Bind("2. Keybinds", "Pause Music Keybinds", "F13, Pause", "List of KeyCodes that can be pressed to pause the current track, separated by commas.");

            customPlaylistNamesUnsplit.SettingChanged += UpdateSettingsEvent;
            keybindsSkipUnsplit.SettingChanged += UpdateSettingsEvent;
            keybindsQueueUnsplit.SettingChanged += UpdateSettingsEvent;
            keybindsShuffleUnsplit.SettingChanged += UpdateSettingsEvent;
            keybindsPauseUnsplit.SettingChanged += UpdateSettingsEvent;
        }

        public static void UpdateSettingsEvent(object sender, EventArgs args) {
            UpdateSettings();
        }

        public static void UpdateSettings() {
            customPlaylistNames = SplitStringByCommas(customPlaylistNamesUnsplit.Value);
            keybindsQueue = KeyCodeListFromList(SplitStringByCommas(keybindsQueueUnsplit.Value));
            keybindsSkip = KeyCodeListFromList(SplitStringByCommas(keybindsSkipUnsplit.Value));
            keybindsShuffle = KeyCodeListFromList(SplitStringByCommas(keybindsShuffleUnsplit.Value));
            keybindsPause = KeyCodeListFromList(SplitStringByCommas(keybindsPauseUnsplit.Value));
        }

        public static KeyCode StringToKeyCode(string input) {
            KeyCode returnValue;
            if (KeyCode.TryParse(input, true, out returnValue)) { 
                return (KeyCode)returnValue;
            }
            return (KeyCode)0;
        }

        public static List<string> SplitStringByCommas(string input) {
            return input.Replace(", ", ",").Split(new [] { "," }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        public static List<KeyCode> KeyCodeListFromList(List<string> input) {
            List<KeyCode> returnList = new List<KeyCode>();
            foreach (string keycodeAsString in input) {
                returnList.Add(StringToKeyCode(keycodeAsString)); 
            }
            return returnList;
        }
    }
}