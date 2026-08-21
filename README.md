# Create An Album

Read [AGENTS.md](AGENTS.md) before changing code, assets, metadata, build configuration, or documentation. It defines the repository-wide identity, integration, and directory-shape requirements for contributors and AI coding agents.

Create An Album (`com.jordanss.createanalbum`) is a Harmony mod for Idol Manager using [IM-HarmonyIntegration](https://github.com/ui3TD/IM-HarmonyIntegration). It is deliberately **not** a raw BepInEx plugin. The mod was formerly referred to as AlbumModLite.

## Architecture

- HarmonyIntegration discovers the `[HarmonyPatch]` classes in `com.jordanss.createanalbum.dll` using `assets/info.json` (`HarmonyID`: `com.jordanss.createanalbum`).
- [Cosmo's Mod Localization System](https://github.com/ExSlam/IM-Cosmo-Mod-Library/tree/main/mods/Mod%20Localization%20System) supplies language-aware GUI, notification, and game JSON localization resources.
- [Cosmo's Mod Buttons](https://github.com/ExSlam/IM-Cosmo-Mod-Library/tree/main/mods/Mod%20Buttons) supplies Action Hub buttons that open Create An Album's `PopupManager`-owned UIs.
- `mainScript.Start` attaches the small Album runtime host only in gameplay.
- [Rivals Reborn](https://steamcommunity.com/sharedfiles/filedetails/?id=3768235126) integration is optional. There is no `rivalsreborn.dll` project reference and no RR CLR type in Album's core API.
- RR is discovered once per gameplay session from the loaded `rivalsreborn` assembly plus Harmony owner `rivalsreborn`, then accessed by reflection.
- A missing/disabled/failed RR bridge is latched for that gameplay session. It is reset only after returning to the main menu, where the player can actually change enabled mods.
- Album never calls `RivalsReborn.Portraits.Pump()`. Current RR owns its portrait pump itself.
- Create, Library, Detail, Chart and automatic chart-performance UI roots are registered with Idol Manager's vanilla `PopupManager` so the game owns queueing, pause/resume, background blur/darken and input blocking.
- Album UI controls use shipped MUIP/game resources plus the vanilla Settings action buttons and producer-list slider templates. Confirm/destructive commands retain the game's green/red sliced sprites and white labels; light input fields use dark text.
- Popup panels use compact centered dimensions, fixed headers and footers, masked scrolling bodies or lists, and the game-selected legacy/TMP font where available.
- Every completed 14-day album chart period opens a localized performance report and awards the restored fan gain based on that period's player-album sales.
- The small `AlbumUiResources` helper is embedded in this assembly. There is no dependency on the standalone IM UI Framework mod.

## Saving

Album mutations mark the Album sidecar **dirty** in memory. `SaveManager.SaveEvent` performs the actual sidecar write. Quitting gameplay without an Idol Manager save therefore does not intentionally commit new Album-only state.

Sidecar writes use a temporary file and replacement/copy fallback. Save IDs preserve Idol Manager's complete save-folder identity. A legacy suffix-stripped Album sidecar is copied into the exact save-specific filename on first load when possible.

The per-save album chart cycle anchor is stored in the same sidecar so restarting or reloading cannot postpone the next performance report.

## Building

Run `dotnet build CreateAnAlbum.csproj -c Release`. The project uses the same shared .NET 4.6 and game-reference layout as the Cosmo Harmony mods; see [BUILDING.md](BUILDING.md) for build, packaging, and installation details.

## Known Issues
- Saving and loading sets album data to none

## Planned Features
1. Add a second row to Text Color to add primary colors like red, blue, green, orange with a square with icon for color palette to popup a color picker wheel color selector alongside hex code entering for selecting color in that popup (if possible with MUIP or plain Unity)
2. Add a row similar to text color selection but for adding an opaque background to album title section (with None as an option as well) along with the Group Name background section if it's enabled in the settings row
3. Add quality percentage next to song names in Album Creation UI
