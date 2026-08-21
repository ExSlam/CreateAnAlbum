# Create An Album changelog

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
