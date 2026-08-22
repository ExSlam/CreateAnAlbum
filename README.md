# Create An Album

Read [AGENTS.md](AGENTS.md) before changing code, assets, metadata, build configuration, or documentation. It defines the repository-wide identity, integration, persistence, and directory-shape requirements for contributors and AI coding agents.

Create An Album (`com.jordanss.createanalbum`) is a Harmony mod for Idol Manager using [IM-HarmonyIntegration](https://github.com/ui3TD/IM-HarmonyIntegration). It is deliberately **not** a raw BepInEx plugin. The mod was formerly referred to as AlbumModLite.

## Architecture

- HarmonyIntegration discovers the `[HarmonyPatch]` classes in `com.jordanss.createanalbum.dll` using `info.json` (`HarmonyID`: `com.jordanss.createanalbum`).
- [Cosmo's Mod Localization System](https://github.com/ExSlam/IM-Cosmo-Mod-Library/tree/main/mods/Mod%20Localization%20System) supplies language-aware GUI, notification, and game JSON localization resources.
- [Cosmo's Mod Buttons](https://github.com/ExSlam/IM-Cosmo-Mod-Library/tree/main/mods/Mod%20Buttons) supplies Action Hub buttons that open Create An Album's `PopupManager`-owned UIs.
- `mainScript.Start` attaches the small Album runtime host only in gameplay.
- Create, Discography, Album Detail, Album Chart, Album Production, and automatic chart-performance UI roots are registered with Idol Manager's vanilla `PopupManager` so the game owns queueing, pause/resume, background blur/darken, input blocking, and popup transitions.
- Album UI controls use shipped MUIP/game resources plus the vanilla Settings action buttons and producer-list slider templates. Confirm/destructive commands retain the game's green/red sliced sprites and white labels; light input fields use dark text. Every Create An Album **Close** control is destructive-style red with white text.
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

Version 4.1.4 removes the `System.Drawing` dependency introduced by 4.1.3. Idol Manager's Mono runtime does not ship that framework assembly, so referencing it prevented Harmony from enumerating Create An Album's types and caused Cover Design rendering to abort. Packaged/custom cover typography now uses a Win32 GDI rasterizer implemented through `gdi32`/`user32` P/Invoke plus Unity textures only. The renderer selects the actual privately registered TTF face by its embedded family/full/PostScript names, verifies the selected GDI face, rasterizes title/subtitle coverage into cached RGBA sprites, and falls back to the normal game font if any custom-font step fails. The Unity dynamic-font path is still attempted first for ordinary UI compatibility, but cover titles no longer depend on Unity's stale OS-font enumeration and the mod has no `System.Drawing` load-time dependency.

The mod creates a sibling `CustomFonts` directory in the live mod folder; place additional `.ttf` files there before starting gameplay to make them available in the Cover Designer. Persisted covers use stable font keys rather than depending only on a list index, while old `FontIndex` values remain migration-compatible.

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

Version 4.2.1 uses unified **schema v4** persistence for albums, the 14-day chart anchor, release metadata, once-only debut reward state, active production, and historical member portrait descriptors. The descriptors contain idol metadata and texture-asset IDs only; Create An Album intentionally does **not** store rendered cover images or portrait PNGs.

[Cosmo's IM Data Core](https://github.com/ExSlam/IM-Cosmo-Mod-Library/tree/main/mods/IM%20Data%20Core) remains optional and reflection-only. CAA writes the same checkpoint-bound schema document into IMDC namespace `com.jordanss.createanalbum` / key `state_v3` immediately before IMDC prepares/forks the concrete vanilla checkpoint. The direct CAA mirror remains the authoritative exact-slot copy; IMDC is a secondary/recovery copy. The historical `state_v3` key is intentionally retained so existing IMDC data migrates without creating a disconnected namespace.

Version 4.2.1 no longer uses Unity `JsonUtility` for the CAA save envelope. A CAA-specific explicit JSON codec, modeled on IM Data Core's `LightweightSidecarJson` durability rules, always writes the checkpoint, production-project property, album collection, member snapshots, and nested ID/asset arrays. Schema-v4 readers reject a document that omits any required structural field instead of interpreting a scalar-only header as an empty album history. The exact five-scalar header produced by the broken 4.2.0 runtime is recognized narrowly, preserved for diagnostics, and made writable again without pretending its omitted album/project data can be recovered.

CAA no longer persists an absolute operating-system path as save identity. Vanilla `persistentDataPath/data/<slot>` is mirrored beneath `persistentDataPath/CreateAnAlbum/<slot>`. For example, `data/manual_saves/12/save.json` maps to `CreateAnAlbum/manual_saves/12/save.json`. The checkpoint stores only that stable path relative to Idol Manager's `data` root, so moving a save between Windows users or machines does not change its logical identity. Version 4.2.0 migrates the older `albums_path_<hash>.json` layout when it can identify the same relative slot.

Create An Album still avoids patching generic `DataSaver<T>`. It targets the five known vanilla callers that write `SaveManager.SavedData`: `SaveManager.SaveData(bool, bool)`, `SaveManager.SaveChapter(tasks._chapter)`, `Popup_Save.Save()`, `Popup_Load_Story.Do_Overwrite_Save(save_info)`, and `Popup_Load_Story.Do_New_Save(string)`. CAA captures the exact load argument consumed by `DataSaver.loadData<SavedData>` instead of independently recomputing the latest autosave path.

Version 4.2.0 embeds a CAA-local save-write-ordering fallback derived from the same behavioral contract as Cosmo's Save Write Ordering Fix: freeze `SavedData` JSON synchronously on the caller thread, queue writes FIFO per physical save path, and coordinate known SavedData reads with pending writes. If the standalone `com.cosmo.savewriteorderingfix` Harmony owner is installed, CAA leaves the final writer/reader calls for the standalone mod instead of double-patching them. CAA's custom-JSON mutation still runs before IM Data Core freezes/forks the checkpoint.

The old repeated-load-failure recovery could clear `Albums.AlbumList`, certify that empty runtime as loaded, and let the next genuine game save overwrite the supplemental file with `Albums: []`. Version 4.2.0 removes that destructive recovery. An existing but unreadable or incompatible sidecar is preserved (including a `.loadfailed_*` diagnostic copy) and supplemental writes are blocked for that load context rather than blessing an empty album collection. A truly new vanilla slot with no CAA/backup/IMDC state may still initialize as an empty album collection. Same-logical-slot checkpoint mismatches from the historical asynchronous writer race are migrated/rebound instead of being treated as zero albums.

Track IDs retain their deferred-repair bridge, and album members now have an equivalent historical snapshot layer. Live idols remain the only objects exposed to gameplay reward/fan logic; cover rendering can reconstruct a detached portrait-only NORMAL idol from exact saved texture-asset IDs when a member has graduated or is temporarily absent from `data_girls`. This keeps generated covers stable without storing image blobs and avoids making Graduation Details a hard dependency. For pre-v4 CAA albums whose idol had already graduated before CAA could capture its own asset IDs, an optional reflection-only bridge can import the matching historical portrait descriptor from an installed Graduation Details snapshot. Non-normal unique idols are rendered from live instances when available rather than fabricating gameplay objects.

Album mutations remain in memory until Idol Manager performs a genuine save, at which point CAA commits the staged schema document to the concrete mirrored slot and validates the resulting vanilla checkpoint.

## Building

Run `dotnet build CreateAnAlbum.csproj -c Release`. The project uses the same shared .NET 4.6 and game-reference layout as the Cosmo Harmony mods; see [BUILDING.md](BUILDING.md) for build, packaging, and installation details.

## Planned Features
1. Add a second row to Text Color to add primary colors like red, blue, green, orange with a square with icon for color palette to popup a color picker wheel color selector alongside hex code entering for selecting color in that popup (if possible with MUIP or plain Unity)
2. Add a row similar to text color selection but for adding an opaque background to album title section (with None as an option as well) along with the Group Name background section if it's enabled in the settings row
3. Add quality percentage next to song names in Album Creation UI
