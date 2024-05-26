<p align="center"> <img src="icon.png" alt="MusicCurator icon" width="200"/> </p> 
<h1> <p align="center" > MusicCurator </p> </h1> 

 A Bomb Rush Cyberfunk plugin that adds several new features to enhance the in-game listening experience.\
 For more information or release downloads, [check the Thunderstore page.](https://thunderstore.io/c/bomb-rush-cyberfunk/p/goatgirl/MusicCurator)
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
## Installation
- If installing using r2modman, click "Install with Mod Manager"
- If installing manually, extract the .zip and drop the mod files into your \BepInEx\plugins\ folder
	- Make sure that the "MC-LoopSingle.png" and "MC-PlaylistIcon.png" files (found in the \include\ folder) are in the same location as the MusicCurator.dll file!
## Building from Source
This plugin requires the following .dlls to be placed in the \lib\ folder to be built:
- A [publicized](https://github.com/CabbageCrow/AssemblyPublicizer) version of the game's code, from BRC's Data folder (Assembly-CSharp.dll)
- [CommonAPI.dll by Lazy Duchess](https://github.com/LazyDuchess/BRC-CommonAPI/releases)
- 0Harmony.dll and BepInEx.dll from \BepInEx\core
- [BombRushRadio.dll](https://github.com/Kade-github/BombRushRadio/releases) for compatibility purposes
- Some Unity Engine .dlls from Bomb Rush Cyberfunk's Data folder:
   - UnityEngine.UI.dll
   - Unity.TextMeshPro.dll

With these files, run "dotnet build" in the project's root folder (same directory as MusicCurator.csproj) and the .dll will be in the \bin\ folder. 

## Credits
Huge thanks to:
- **Lazy Duchess**, for the CommonAPI plugin and example code that made this mod possible
- **Kade, ActualMandM, LunaCapra,** and **PrincessMtH** for their work on BombRushRadio, which inspired this mod
