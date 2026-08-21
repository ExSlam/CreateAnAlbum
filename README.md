# Create An Album

Read [AGENTS.md](AGENTS.md) before changing code, assets, metadata, build configuration, or documentation. It defines the repository-wide identity, integration, persistence, and directory-shape requirements for contributors and AI coding agents.

Create An Album (`com.jordanss.createanalbum`) is a Harmony mod for Idol Manager using [IM-HarmonyIntegration](https://github.com/ui3TD/IM-HarmonyIntegration). It is deliberately **not** a raw BepInEx plugin. The mod was formerly referred to as AlbumModLite.

## Architecture

- HarmonyIntegration discovers the `[HarmonyPatch]` classes in `com.jordanss.createanalbum.dll` using `info.json` (`HarmonyID`: `com.jordanss.createanalbum`).
- [Cosmo's Mod Localization System](https://github.com/ExSlam/IM-Cosmo-Mod-Library/tree/main/mods/Mod%20Localization%20System) supplies language-aware GUI, notification, and game JSON localization resources.
- [Cosmo's Mod Buttons](https://github.com/ExSlam/IM-Cosmo-Mod-Library/tree/main/mods/Mod%20Buttons) supplies Action Hub buttons that open Create An Album's `PopupManager`-owned UIs.
- `mainScript.Start` attaches the small Album runtime host only in gameplay.
- Create, Discography, Album Detail, Album Chart, Album Production, and automatic chart-performance UI roots are registered with Idol Manager's vanilla `PopupManager` so the game owns queueing, pause/resume, background blur/darken, input blocking, and popup transitions.
- Album UI controls use shipped MUIP/game resources plus the vanilla Settings action buttons and producer-list slider templates. Confirm/destructive commands retain the game's green/red sliced sprites and white labels; light input fields use dark text.
- Popup panels use compact centered dimensions, fixed headers and footers, masked scrolling bodies or lists, and the game-selected legacy/TMP font where available.
- The small `AlbumUiResources` helper is embedded in this assembly. There is no dependency on the standalone IM UI Framework mod.

## Album production and release formats

Version 4.1.0 restores the production gameplay formerly supplied by the standalone Group Rules addon and ports it into the current Harmony/PopupManager architecture.

- **Mini Album:** exactly 6 released singles.
- **EP:** 6 to 10 released singles.
- **LP / Album:** 10 to 15 released singles.
- Production costs **¥500,000** when the project starts.
- One album project can be in production at a time for the current save.
- Production lasts **12 in-game days** over four stages: Pre-Production & Writing (3 days), Production & Recording (4 days), Post-Production (3 days), and Release & Distribution (2 days).
- F2 / Create Album opens the active production dashboard while a project is underway.
- The project snapshots its title, group, release type, songs, members, center, and cover configuration when production begins.
- At the end of production, the album is released, receives debut sales and its initial chart position, and awards the restored once-only debut fan reward.
- Debut fans use the legacy formula: 2% of debut sales, minimum 100 fans, with a 0.80x multiplier for 6-track releases, 1.00x for 7 to 10 tracks, and 1.20x for releases above 10 tracks.

## Covers, backgrounds, and fonts

Create An Album ships Allura, Cinzel, Cormorant Garamond, and Cyberthrone and now uses the packaged font files in both the live Cover Designer and persisted cover renderer.

Version 4.1.1 makes album backgrounds directory-driven. The shared background catalog scans `AlbumBackgrounds` recursively for `.png`, `.jpg`, and `.jpeg` files, so users can add images and future releases can ship more backgrounds without code changes. The Cover Designer exposes every discovered image through a padded horizontal thumbnail scroller, and persisted albums use stable relative-path background keys so adding files later does not remap existing covers. Legacy numeric background indexes remain migration-compatible.

On Windows, packaged and custom `.ttf` files are registered privately for the game process rather than installed system-wide. The mod creates a sibling `CustomFonts` directory in the live mod folder; place additional `.ttf` files there before starting gameplay to make them available in the Cover Designer. Persisted covers use stable font keys rather than depending only on a list index, while old `FontIndex` values remain migration-compatible.

## Album chart and Rivals Reborn

- The Album Chart remains a Top 20 and advances on the persisted 14-day chart cycle.
- Every completed 14-day period opens the localized performance report and awards the period fan gain based on player-album sales.
- Cover art is available across all Top 20 rows. The chart virtualizes covers in four-row batches, keeps only a small LRU cache of built cover object trees, prefetches a neighboring batch, and deactivates off-screen cover graphics instead of eagerly rendering all twenty at popup open.
- [Rivals Reborn](https://steamcommunity.com/sharedfiles/filedetails/?id=3768235126) integration is optional. There is no `rivalsreborn.dll` project reference and no RR CLR type in Album's core API.
- RR is discovered once per gameplay session from the loaded `rivalsreborn` assembly plus Harmony owner `rivalsreborn`, then accessed by reflection.
- A missing/disabled/failed RR bridge is latched for that gameplay session and reset only after returning to the main menu.
- Album never calls `RivalsReborn.Portraits.Pump()`. Current RR owns its portrait pump itself.
- When RR exposes `RR.QueueNews(string)`, newly generated runtime rival album releases are queued into RR's existing news system. Initial rival bootstrap albums do not generate release-news spam.

## Saving

Version 4.1.0 uses a unified **schema v3** save document for albums, the 14-day chart anchor, release metadata, once-only debut reward state, and the active production project. Production is no longer committed independently from the Idol Manager save that paid its cost.

[Cosmo's IM Data Core](https://github.com/ExSlam/IM-Cosmo-Mod-Library/tree/main/mods/IM%20Data%20Core) is an optional, preferred persistence backend when its `com.cosmo.imdatacore` Harmony owner is active. Create An Album integrates with it by reflection, so there is no compile-time IM Data Core dependency. The standalone fallback remains available when IM Data Core is absent.

The fallback identity tracks Idol Manager's concrete vanilla save/checkpoint path rather than only the campaign folder name. Create An Album patches the known concrete save callers and deliberately does **not** patch generic `DataSaver<T>`. Story `Do_New_Save` receives an additional caller-level path capture immediately before its final `SavedData` write because Idol Manager generates that random physical save directory after `SaveEvent` has already fired.

This ordering is designed to compose with Cosmo's Save Write Ordering Fix: IM Data Core can prepare its checkpoint first, Create An Album captures the final generated path without replacing the vanilla writer, and Save Write Ordering Fix can run last and replace only that writer.

Standalone mirrors use atomic temporary-file replacement and include a write timestamp. When both IM Data Core and a standalone mirror are available, the newer valid copy wins and can reseed the other backend. Legacy v4 campaign-level sidecars and old standalone `album_production_*.json` projects are migrated forward when possible.

Album mutations remain dirty in memory until Idol Manager performs a save. Quitting gameplay without an Idol Manager save therefore does not intentionally commit new album or production-only state.

## Building

Run `dotnet build CreateAnAlbum.csproj -c Release`. The project uses the same shared .NET 4.6 and game-reference layout as the Cosmo Harmony mods; see [BUILDING.md](BUILDING.md) for build, packaging, and installation details.

## Planned Features
1. Add a second row to Text Color to add primary colors like red, blue, green, orange with a square with icon for color palette to popup a color picker wheel color selector alongside hex code entering for selecting color in that popup (if possible with MUIP or plain Unity)
2. Add a row similar to text color selection but for adding an opaque background to album title section (with None as an option as well) along with the Group Name background section if it's enabled in the settings row
3. Add quality percentage next to song names in Album Creation UI
