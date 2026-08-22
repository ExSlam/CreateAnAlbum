# Create An Album changelog

## 4.2.1

- Replaced every `AlbumSaveFile` `JsonUtility` read/write with a CAA-specific explicit JSON codec modeled on IM Data Core's `LightweightSidecarJson` persistence rules. The writer deterministically emits the full schema-v4 envelope, including `VanillaCheckpoint`, `ProductionProject`, `Albums`, track/member ID arrays, historical member snapshots, portrait asset references, chart/sales fields, and cover-recipe fields.
- Made schema-v4 parsing fail closed on missing required structure, duplicate object properties, invalid collection types, invalid integer/float types, and non-finite cover-layout floats. Unknown additive properties remain tolerated. A header-only JSON document can no longer be accepted as a legitimate empty album history.
- Added a narrow migration for the exact five-scalar CAA 4.2.0 `JsonUtility` failure shape observed in real saves. When every available direct/backup/IMDC candidate has only that known unrecoverable shape, CAA preserves diagnostic copies, carries forward scalar chart/migration metadata, restores a writable slot, and rewrites a complete explicit envelope on the next genuine game save. Any unknown malformed candidate still retains the existing fail-closed write block.
- Kept IM Data Core optional/reflection-only and continued sending IMDC and the direct exact-slot mirror the exact same checkpoint-bound serialized string before IMDC prepares/forks the vanilla checkpoint.
- Bumped project and mod metadata version to 4.2.1; persistence schema remains v4.

## 4.2.0

- Fixed the empty-sidecar failure mode: repeated checkpoint-load failure no longer clears the album list and certifies that empty runtime as writable. Existing unresolved CAA data is preserved with `.loadfailed_*` diagnostics and supplemental writes are blocked instead of overwriting it with `Albums: []`.
- Embedded a CAA-local caller-level SavedData write-ordering fallback: SavedData JSON is frozen synchronously, writes are FIFO per physical path, and known SavedData readers wait for pending writes. If Harmony owner `com.cosmo.savewriteorderingfix` is installed, the embedded layer steps aside so Cosmo's standalone SWO remains authoritative.
- Kept CAA's concrete save mutation before `com.cosmo.imdatacore`, ensuring the schema document is present before IM Data Core prepares/forks the same vanilla checkpoint. Same-slot checkpoint mismatches caused by the historical async writer race can be recovered and rebound instead of being interpreted as an empty album history.
- Replaced absolute-OS-path slot identity with a stable path relative to Idol Manager's `persistentDataPath/data` root. CAA now mirrors that tree under `persistentDataPath/CreateAnAlbum` and migrates old `albums_path_<hash>.json` files when possible, including moved-save migration through the embedded checkpoint's `/data/` tail.
- Added schema-v4 historical member snapshots containing idol identity/name/type and exact texture-asset IDs only. Graduated NORMAL idols can be reconstructed as detached portrait-only shells for cover rendering while gameplay-facing album member lists contain only live registry idols. No cover/portrait image blobs are stored. Graduation Details remains optional, with a reflection-only migration bridge that can enrich old CAA ID-only members from its already-loaded historical snapshot.
- Preserved member ordering and center-member identity across graduation/registry timing, and prevented temporarily unresolved members from erasing their saved IDs on the next save.
- Captured the exact string passed into vanilla SavedData loads rather than independently resolving the latest autosave a second time.
- Bumped project and mod metadata version to 4.2.0.

## 4.1.4

- Removed the `System.Drawing` framework reference and the 4.1.3 private-font-collection rasterizer after runtime logs showed Idol Manager Mono could not load `System.Drawing, Version=4.0.0.0`; that missing dependency caused `TypeLoadException`/`ReflectionTypeLoadException` and interrupted Harmony type discovery.
- Replaced direct TTF cover rendering with a Win32 GDI implementation using `gdi32`/`user32` P/Invoke plus Unity `Texture2D`/`Sprite` output. Verified Unity dynamic fonts are used directly when available; GDI is reserved for TTF faces Unity 2019 cannot expose. The renderer verifies the selected GDI face against embedded TTF names and retains the normal game-font fallback if rasterization fails.
- Made `AlbumFontCatalog.RenderTextSprite` fail-safe so any rasterizer error returns to ordinary Unity text instead of removing the album title or aborting Cover Design.
- Made Cover Design create Back/Continue footer buttons before rendering preview content and catch step-render exceptions, so preview/font failures can no longer remove navigation or trap the user on the page.
- Added a dedicated Cover Design refresh path that captures the nested settings scrollbar, outer page scrollbar, and background-strip position, then reapplies them over multiple Unity layout frames. Clicking a layout/theme/font/color/adjustment/background control no longer snaps the left options panel back to the top.
- Hardened the base AlbumPopup song-selection, Continue, and Save paths to use the active Mini/EP/LP minimum and maximum instead of retaining hidden 6–10-song fallback limits.
- Bumped project and mod metadata version to 4.1.4.

## 4.1.3

- Rebuilt the Album Information page around release-aware geometry instead of drawing the old compact 6–10-song page and repositioning it afterward. Changing Mini Album, EP, or Album/LP now preserves the spacing between release controls, Album Name, and song selection.
- Made the song counter release-aware: Mini Album shows 6/6 with minimum 6, EP shows up to 10 with minimum 6, and Album/LP shows up to 15 with minimum 10. The counter only turns green when the selected count satisfies the active release type.
- Added a direct TTF rasterization fallback for cover typography. When Unity 2019 cannot expose packaged/custom fonts through `CreateDynamicFontFromOSFont`, Create An Album reads the actual `.ttf` through a private font collection and renders album-title/group-name sprites directly, so Allura, Cinzel, Cormorant Garamond, and Cyberthrone no longer depend on Unity's cached OS-font list.
- Changed persistence conflict policy so the valid exact physical-slot Create An Album mirror is authoritative. IM Data Core remains synchronized as a secondary/recovery copy, preventing a stale zero-album IMDC branch from overwriting a valid one-album manual-save mirror on load.
- Removed `PopupManager.Close()` from Create An Album reset/cleanup. CAA now retires only its own stale registrations directly and repairs a stranded popup counter only when no live/queued popup exists, eliminating the startup null-reference loop that could permanently suppress automatic 14-day chart reports.
- Bumped project and mod metadata version to 4.1.3.

## 4.1.2

- Fixed the Album Information layout so the Album Name input, song heading/count, song selector, and help text are shifted below the restored Release Type controls instead of overlapping them.
- Reworked packaged/custom TTF loading for Unity 2019 on Windows. The shared font catalog now reads preferred/family/full/PostScript names from each TTF, temporarily registers packaged files before Unity performs its first lookup, and notifies Windows when font resources change. Resolution first uses Unity's installed-font enumeration, then falls back to direct dynamic-font lookup for Unity 2019 builds whose enumeration remains cached, validating the returned `Font` against its own naming metadata before accepting it. Successful and failed registration/resolution attempts are logged explicitly.
- Standardized every Create An Album **Close** control on `AlbumButtonStyle.Destructive`, which uses the vanilla red action style with white text. This includes the Album Production dashboard.
- Removed the redundant Album Detail scroll-rebuild Harmony patch. Collaboration subtitles now render inside the native `TrackListScrollRoot`, so Discography/Album Detail owns exactly one track scrollbar.
- Reworked supplemental save timing around the exact vanilla `SavedData` write. `SaveEvent` now stages the CAA schema-v3 document without rebinding the loaded slot; a caller-level transpiler on all five known vanilla save routes commits that staged document to the concrete physical target **before** IM Data Core prepares/forks its checkpoint and before Save Write Ordering Fix handles the final writer.
- Saving to autosave, Save As, Story save, or another manual target no longer changes Create An Album's in-memory **loaded** identity. Only a real load rebinds the live album/production state, preventing autosave/manual writes from making later loads select the wrong CAA branch.
- Supplemental writes are skipped while CAA is still restoring a load, preventing an early vanilla autosave from turning a transient empty runtime into an authoritative empty album checkpoint.
- Fallback state selected during a vanilla load is no longer pushed into IM Data Core from inside the load boundary; it is marked dirty and committed through the next real concrete save checkpoint instead.
- Hardened Create An Album popup cleanup against stale `PopupManager` registrations whose object or `Popup` component is missing, preventing the startup/reset `NullReferenceException` observed when vanilla `PopupManager.Close()` encountered such an entry.
- Initialized the legacy production DTO's deserialization-only `Project` field explicitly, removing the benign CS0649 build warning without converting the field to a JsonUtility-incompatible property.
- Bumped project and mod metadata version to 4.1.2.

## 4.1.1

- Replaced the duplicated fixed PNG loaders with one shared runtime background catalog used by the Cover Designer and final cover renderer.
- Background discovery is now recursive and accepts `.png`, `.jpg`, and `.jpeg` files from `AlbumBackgrounds`, allowing user-added images and future shipped backgrounds without source changes.
- Added stable persisted `BackgroundKey` identities based on normalized relative paths while retaining `BackgroundIndex` for backward compatibility and migration.
- Captures the legacy 4.1.0 top-level PNG enumeration order before applying the new catalog ordering so existing numeric background selections can migrate as faithfully as possible.
- Added a dedicated horizontal background-thumbnail scroller with a permanent scrollbar, preserved scroll position across Cover Designer refreshes, and extra side padding so selected thumbnail outlines are not clipped at the left edge.
- Background filenames are surfaced as the selected-background label and in the release summary.
- Rival albums now select from the dynamically discovered background catalog instead of a hard-coded six-background range.
- Added live catalog refresh detection with sprite reuse/caching so newly added or replaced images can be picked up during gameplay without rebuilding every chart cover from disk.
- Bumped project and mod metadata version to 4.1.1.

## 4.1.0

- Restored the standalone Group Rules production gameplay inside the unified Harmony mod: Mini Album (6 tracks), EP (6 to 10), and LP (10 to 15).
- Restored the ¥500,000 production cost and 12-day 3/4/3/2 stage schedule for Pre-Production & Writing, Production & Recording, Post-Production, and Release & Distribution.
- Added a PopupManager-owned Album Production dashboard and made F2 / Create Album reopen the active project while production is underway.
- Folded the active production project into unified persistence schema v3 so the production checkpoint and the Idol Manager save that paid for it advance or roll back together.
- Added migration for legacy standalone `album_production_*.json` project files.
- Added persisted release-type metadata to albums and surfaced the format in Discography and Album Detail.
- Restored 15-track LP support and made Album Detail's track list scroll through the full release.
- Restored the legacy once-only debut fan reward: 2% of debut sales with a 100-fan minimum and track-count multipliers of 0.80x / 1.00x / 1.20x. Historical pre-v3 albums migrate with the reward marked consumed to avoid retroactive payouts.
- Replaced unused packaged-font placeholders with a shared font catalog used by the Cover Designer and persisted cover renderer. Allura, Cinzel, Cormorant Garamond, and Cyberthrone are loaded from the shipped TTF files on Windows.
- Added runtime `CustomFonts` support for user-supplied `.ttf` files and stable persisted `FontKey` identities while retaining legacy `FontIndex` migration.
- Restored cover art across all Top 20 chart rows using four-row virtualization, neighboring-batch prefetch, off-screen culling, and a bounded LRU cache instead of eagerly constructing twenty cover object trees.
- Restored optional Rivals Reborn release-news calls through the reflection bridge when `RR.QueueNews(string)` is available; bootstrap rival population remains silent.
- Reworked album persistence around concrete vanilla save/checkpoint identity instead of campaign-folder identity alone. Manual slots and story checkpoints can now diverge safely.
- Added optional reflection-only IM Data Core persistence using namespace `com.jordanss.createanalbum` / key `state_v3`, with the standalone sidecar retained as a migration/fallback mirror.
- Added write timestamps and newer-valid-copy selection when both IM Data Core and the standalone mirror contain state.
- Added caller-level capture for Idol Manager's generated Story `Do_New_Save` path. The transpiler leaves the vanilla SavedData write intact, runs after IM Data Core, and runs before Save Write Ordering Fix. Generic `DataSaver<T>` remains unpatched.
- Preserved atomic standalone writes, empty-save protection, old campaign-sidecar migration, album deduplication, persisted chart timing, and load-generation invalidation.
- Bumped project and mod metadata version to 4.1.0.

## 4.0.0

- Standardized the internal assembly and Harmony identifier as `com.jordanss.createanalbum`.
- Completed the Mod Buttons action labels, tooltips, and localized section title contract.
- Added opt-in packaging for the complete runtime asset tree under `Mods\CreateAnAlbum`.
- Added repository-wide contributor and AI-agent instructions in `AGENTS.md`.
- Fixed Action Hub launches by queueing Create An Album popups through vanilla `PopupManager`.
- Hardened popup registration, animated close, transition, and teardown lifecycles.
- Rebuilt album list scroll views with separate roots, masked viewports, and content containers.
- Stabilized popup panel sizing and kept chart controls attached to the chart panel.
- Corrected MUIP button labels, input placeholders, compact control text sizing, and game-font handling.
- Applied vanilla red/green command styling, dark input text, compact popup bodies, and visible native list sliders across every album UI.
- Restored the automatic 14-day album performance report, including period fan rewards, localized results, and a persisted per-save cycle anchor.
- Preserved Cover Design scroll positions across control changes and styled Back with vanilla purple and white text.
- Clarified chart movement labels and deferred automatic performance reports until the vanilla popup system is stably idle.

## 1.5.0 Harmony overhaul

- Converted runtime bootstrap from BepInEx plugin ownership to HarmonyIntegration patches.
- Removed compile-time Rivals Reborn dependency.
- Added session-latched, Harmony-owner-verified RR reflection bridge.
- Removed Album's per-frame RR portrait pump; RR services its own portrait queue.
- Made RR portrait methods optional reflection capabilities.
- Reworked rival album generation around Album-owned snapshot wrappers rather than RR CLR types.
- Made live vanilla rival-group data optional enrichment instead of a requirement for an RR label.
- Registered Create/Library/Detail/Chart roots with vanilla `PopupManager`.
- Added tiny embedded Album UI resource helper using named MUIP/game Resources directly.
- Removed the second Chart BepInEx plugin and Album self-`PatchAll()` bootstraps.
- Removed redundant Album duplicate-guard and chart self-patch layers.
- Changed sidecar persistence to dirty-state commits on Idol Manager save events.
- Added atomic sidecar replacement fallback.
- Preserved exact vanilla save-folder identity and added one-time legacy sidecar copying.
- Rebuilt negative rival-ID allocation from loaded records.
