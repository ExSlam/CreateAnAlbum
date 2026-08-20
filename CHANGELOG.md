# Create An Album changelog

## 4.0.0

- Standardized the internal assembly and Harmony identifier as `com.jordanss.createanalbum`.
- Completed the Mod Buttons action labels, tooltips, and localized section title contract.
- Added opt-in packaging for the complete runtime asset tree under `Mods\CreateAnAlbum`.
- Added repository-wide contributor and AI-agent instructions in `AGENTS.md`.

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
