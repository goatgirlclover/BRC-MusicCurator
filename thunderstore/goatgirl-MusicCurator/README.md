## MusicCurator
MusicCurator adds several new features to enhance your in-game listening experience, including a playlist app, a track blocklist, and a skip button. Fully compatible with BombRushRadio!

**This mod is currently unfinished - beware of bugs!**

## Features
- **Instant shuffle**: If you're getting tired of the "Locking Up the Funk" mixtape, this config option chooses a random song to play when loading a save. Also switches your music player app to shuffle mode automatically for maximum convienence. 
- **Skip tracks**: Hit R3 (Semicolon on keyboard) to skip the currently playing song at any time. No more scrolling through the music app in the middle of a combo!
- **Track blocklist**: Sick of *that one song* killing your groove? Select it in the music app and press the skip button, and it'll never play again! (...except when you play it manually or in a playlist)
    - By default, all looped mixtapes are blocklisted to prevent them from playing automatically.
- **Track queueing**: Press L3 (Quote) while hovering over a track in the music app to queue it up. Once the current track ends, the earliest queued track will play next, regardless of shuffle/looping settings.
- **Playlists**: The Playlists app in your phone allows you to create and edit playlists, as well as manage your blocklist and queue. Keep your favorite tracks on loop, or create playlists for different stages and vibes. Perfect for organizing your custom tracks!
- **Single loop**: A new looping option within the music app to keep one track constantly playing. Note that skipping a track in single loop mode will simply restart it instead. 
- **Quick shuffle toggle**: Press Select (Left Bracket) while in the music app to quickly toggle looping, shuffling, or looping a single track.
- **All mixtapes everywhere**: The Hideout mixtape is now avaliable to play in every stage, and the "Locking Up the Funk" mixtape is avaliable in the Hideout. 
- Rebindable controls and plenty of configuration options!

## Incompatible Mods
- **Shuffleify by Dragsun**: This mod patches some of the same code MusicCurator patches, leading to unexpected behavior from both mods. Additionally, most of the features of Shuffleify (instant shuffle, looping single tracks, etc.) are also included in MusicCurator, so having both mods becomes redundant. While the plugins will both still load, it's better to choose one over the other.
- **TrackRemix by Glomzubuk**: Untested due to TrackRemix being deprecated. Highly recommended to use BombRushRadio instead.

## Installation
- If installing using r2modman, click "Install with Mod Manager"
- If installing manually, extract the .zip and drop the mod files into your \BepInEx\plugins\ folder
	- Make sure that the "MC-LoopSingle.png" and "MC-PlaylistIcon.png" files are in the same location as the MusicCurator.dll file!

## Configuration
This mod can be configured with r2modman's config editor, configured in-game using the [BepInEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager), or configured manually by editing \BepInEx\config\goatgirl.MusicCurator.cfg. Instructions for configuring the different values can be found in each config option's description.

## Q&A
### Where is the save data stored?
Windows: %localappdata%\Bomb Rush Cyberfunk Modding\MusicCurator\saves

There are separate files for each save slot, and each contains that slot's blocklist and playlists. Playlists can't currently be shared between saves, but you can copy-paste and rename these files to copy over your playlists and blocklist. Note that songs you haven't unlocked yet won't play in playlists.   

### How do I skip/blocklist/queue tracks?
By default, you can skip with R3/Semicolon and queue with L3/Quote. These controls are rebindable via the config editor. To blocklist/queue tracks, use R3/L3 while hovering over the track in the music app.  

### What does all this track color coding mean?
- **Red**: Track is blocklisted (or invalid when within a playlist)
- **Cyan**: Track is in current playlist
- **Purple**: Track is in queue
- **Green** (in certain Playlist app pages): Track is selected 

### How do I make a playlist?
- Open the Playlists app and select "Create new playlists", then "Select playlist tracks"
    - Alternatively, you can add all the tracks you want to your queue through the Music app, then select "Create playlist from queue"
- Press Right on the D-Pad for each track you want included in the playlist
- Once all the tracks you want are selected, press Left to create the playlist   

All created playlists can later be edited, rearranged, or deleted.

### How do I rename my playlists?
Check the config file - there's a setting for custom playlist names. Note that they'll be limited to 32 characters max in-game. 

### Why don't my playlists / queued tracks continue between stages?
This is due to a bug in the current version of BombRushRadio that prevents all custom tracks from continuing between stages. Rather than implementing a fix for this within this mod (or creating a separate fix mod like NoMixtapeAutoplay once was), I've submitted a pull request on BombRushRadio's GitHub page that should fix the issue.   

My fix will hopefully be included in an update to BombRushRadio, but in the meantime, you can use [this modified BombRushRadio.dll](https://github.com/scoopds/BombRushRadio/releases/tag/1.7) as a replacement.   

### Why are some of my playlist tracks/buttons red?
Any music tracks marked with red text couldn't be found - to the mod, they don't exist and are marked as invalid. They shouldn't get deleted from the playlist, but they won't be played. These tracks are usually BombRushRadio custom tracks that were renamed or removed after being added to a playlist. They might also be stage-exclusive mixtapes (ex. "Beats from the Hideout") if the "All Mixtapes" config option is disabled.

Some of the playlist buttons ("Shuffle and loop", "Play and loop", and "Add to queue") will also be highlighted red to signal that the playlist cannot be played. This is for one of two reasons:
- All of the playlist's tracks are **invalid**; the tracks don't exist, so they can't be played
- All of the playlist's tracks are **blocklisted**, and the "Playlists ignore blocklist" config option is disabled

### My controller keybinds don't work / work inconsistently!
In my experience, sometimes KeyCodes (the Unity input system this mod uses) just don't get picked up for controllers on startup. Try disconnecting and reconnecting your controller while in-game. If that doesn't work, you could potentially work around it by using Steam Input and reconfiguring certain inputs to match the keyboard keybinds.   

### I'm getting crashes/errors/bugs from this plugin!
Send a message in the #modding channel of the official Reptile Hideout Discord server and I'll try to sort it out when I can. Crash logs greatly appreciated! You can ping me (@goatgirl_) to make sure I see it, or just make sure the mod's name "MusicCurator" is somewhere in the message so I can search for it. 

## Credits
Huge thanks to:
- **Lazy Duchess**, for the CommonAPI plugin and example code that made this mod possible
- **Kade, ActualMandM, LunaCapra,** and **PrincessMtH** for their work on BombRushRadio, which inspired this mod
