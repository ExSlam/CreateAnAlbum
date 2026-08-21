# Create An Album changelog

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
