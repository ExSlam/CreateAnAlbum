# Create An Album Repository Instructions

These instructions apply to the entire repository. Read them before changing code, metadata, assets, build configuration, or documentation.

## Identity

- The canonical internal mod identifier is `com.jordanss.createanalbum`.
- The player-facing display name is `Create An Album`.
- The mod was formerly referred to as `AlbumModLite`.
- Keep the project `AssemblyName`, `assets/info.json` `HarmonyID`, and every Mod Buttons `assembly` value synchronized as `com.jordanss.createanalbum`.
- Keep the project/package version and `assets/info.json` version synchronized.
- The installed folder name is `CreateAnAlbum` (without spaces).
- `Albummodelite` is a legacy CLR namespace still used by the current source and reflection targets. Do not treat it as the canonical mod identifier or rename it opportunistically.

## Runtime Model

Create An Album is a Harmony **mod** for Idol Manager using [IM-HarmonyIntegration](https://github.com/ui3TD/IM-HarmonyIntegration). It is not a raw BepInEx **plugin**.

- HarmonyIntegration discovers and applies this assembly's Harmony patches.
- Do not add a `BaseUnityPlugin`, a self-owned `Harmony.PatchAll()` bootstrap, or a BepInEx `Paths` dependency.
- Game UI must continue to use Idol Manager's `PopupManager` lifecycle for opening and closing Create, Discography, Album Detail, Album Chart, Album Production, and automatic chart-report popups.
- The integrated release rules are Mini Album = 6 songs, EP = 6 to 10 songs, and LP = 10 to 15 songs.
- Album production costs ¥500,000 and lasts 12 in-game days over the 3/4/3/2-day stage sequence. Do not reintroduce immediate release in the normal Create Album path.
- Production state belongs to the same per-save schema as albums and chart state. Do not reintroduce an independently committed production sidecar.

## Integrations

Create An Album uses Cosmo's [Mod Localization System](https://github.com/ExSlam/IM-Cosmo-Mod-Library/tree/main/mods/Mod%20Localization%20System) for GUI and notification strings and for localized game JSON selected from the game's active language.

- Put string keys in `assets/Localization/<language-code>/strings.txt`.
- Put localized game JSON below `assets/Localization/<language-code>/JSON/...`.
- English is the fallback language. When adding a player-facing key, add the English value and update every current translation where practical.

Create An Album uses Cosmo's [Mod Buttons](https://github.com/ExSlam/IM-Cosmo-Mod-Library/tree/main/mods/Mod%20Buttons) to display actions that trigger `PopupManager`-owned Create An Album UIs.

- The source configuration belongs at `assets/ModButtons/buttons.json`.
- The deployed configuration must be at `ModButtons/buttons.json` beside the mod DLL, without an `assets` wrapper.
- Action targets must be public static methods. Their assembly value must be `com.jordanss.createanalbum`, and class/method names and argument types must exactly match the compiled code.
- Use `mod.title` for the localized Mod Buttons section title and `createanalbum.button.*` / `createanalbum.tooltip.*` for action text.

Create An Album has optional integration with [Rivals Reborn](https://steamcommunity.com/sharedfiles/filedetails/?id=3768235126).

- Rivals Reborn must remain optional and reflection-only.
- Do not add a compile-time Rivals Reborn reference or expose Rivals Reborn CLR types from Create An Album APIs.
- Verify both the loaded `rivalsreborn` assembly and Harmony owner `rivalsreborn` before using the bridge.
- `RR.QueueNews(string)` is optional. Runtime rival album releases may queue news when it is available, but bootstrap rival albums must not generate news spam.
- A missing, disabled, or incompatible Rivals Reborn installation must not stop the base mod from loading.

Create An Album also has optional persistence integration with Cosmo's IM Data Core.

- Keep IM Data Core optional and reflection-only. Do not add a compile-time IM Data Core reference.
- Verify Harmony owner `com.cosmo.imdatacore` before using its reflection-friendly interop API.
- Use Create An Album namespace `com.jordanss.createanalbum` and custom JSON key `state_v3` for the unified schema v3 document.
- Album save identity must follow the concrete vanilla save/checkpoint path. Do not collapse distinct manual saves or story checkpoints back to a campaign-folder-only identity.
- The standalone mirror is a fallback and migration source, not a second independently advancing timeline.

Create An Album is designed to coexist with Cosmo's Save Write Ordering Fix.

- Never Harmony-patch generic `DataSaver<T>`.
- Save interception belongs on the known concrete Idol Manager SavedData callers: `SaveManager.SaveData(bool, bool)`, `SaveManager.SaveChapter(tasks._chapter)`, `Popup_Save.Save()`, `Popup_Load_Story.Do_Overwrite_Save(save_info)`, and `Popup_Load_Story.Do_New_Save(string)`.
- The `Do_New_Save` concrete-path transpiler must leave the final SavedData writer instruction in place, run after `com.cosmo.imdatacore`, and run before `com.cosmo.savewriteorderingfix`.
- Save Write Ordering Fix must remain free to run last and replace only the final SavedData writer.

## Cover assets and chart rendering

- Album backgrounds belong in `assets/AlbumBackgrounds` and are discovered dynamically and recursively at runtime. Supported file types are `.png`, `.jpg`, and `.jpeg`; do not hard-code a background count or filename list into gameplay/UI code.
- Persisted background identity uses stable relative-path `BackgroundKey` values. Keep `BackgroundIndex` only for migration/backward compatibility, and keep the Cover Designer and final renderer on the same shared background catalog.
- The background picker must remain horizontally scrollable and retain enough side padding that thumbnail outlines are not clipped by its mask.
- Packaged fonts belong in `assets/AlbumFonts` and are resolved through the shared album font catalog for both live previews and persisted cover rendering.
- Persisted font identity uses stable `FontKey` values. Keep `FontIndex` only for migration/backward compatibility.
- `CustomFonts` is a runtime-created, user-managed sibling of `AlbumFonts` and is not a source asset directory.
- The Album Chart supports cover art for all Top 20 entries through four-row virtualization. Do not regress to an eager twenty-cover build or a Top-4-only render gate.
- Off-screen chart covers should be culled/deactivated and retained only within the bounded cache rather than destroyed/rebuilt on every scroll delta.

## Development Directory

The expected development shape is shown below. It may gain new files and asset or localization entries over time. Do not enumerate individual `src` filenames in documentation because they change frequently.

```text
CreateAnAlbum_Overhauled/
|-- .gitignore
|-- AGENTS.md
|-- BUILDING.md
|-- CHANGELOG.md
|-- CreateAnAlbum.csproj
|-- Directory.Build.props
|-- README.md
|-- assets/
|   |-- info.json
|   |-- thumb.png
|   |-- AlbumBackgrounds/
|   |   `-- <background images and optional subfolders; .png/.jpg/.jpeg>
|   |-- AlbumFonts/
|   |   |-- Allura-Regular.ttf
|   |   |-- Cinzel-VariableFont_wght.ttf
|   |   |-- CormorantGaramond-VariableFont_wght.ttf
|   |   `-- Cyberthrone.ttf
|   |-- Localization/
|   |   `-- <language-code>/
|   |       |-- strings.txt
|   |       `-- JSON/... (when that language supplies localized game JSON)
|   `-- ModButtons/
|       `-- buttons.json
|-- src/
|   `-- *.cs
|-- bin/ (generated and ignored)
|-- dll/ (ignored local reference cache; the default reference path is ../dll)
`-- obj/ (generated and ignored)
```

## Deployment Directory

Deploy the contents of `assets`, not the `assets` directory itself. The expected live installation is:

```text
%USERPROFILE%\AppData\LocalLow\Glitch Pitch\Idol Manager\Mods\CreateAnAlbum\
|-- com.jordanss.createanalbum.dll
|-- info.json
|-- thumb.png
|-- AlbumBackgrounds/
|   `-- <background images and optional subfolders; .png/.jpg/.jpeg>
|-- AlbumFonts/
|   |-- Allura-Regular.ttf
|   |-- Cinzel-VariableFont_wght.ttf
|   |-- CormorantGaramond-VariableFont_wght.ttf
|   `-- Cyberthrone.ttf
|-- CustomFonts/ (runtime-created; user .ttf files)
|-- Localization/
|   `-- <language-code>/
|       |-- strings.txt
|       `-- JSON/... (when supplied)
`-- ModButtons/
    `-- buttons.json
```

The live installation does not require the PDB, source files, project files, `bin`, `obj`, `dll`, or an `assets` wrapper directory.

## Build and handoff expectations

- Target .NET Framework 4.6 (`net46`) to match Idol Manager and the working Cosmo Harmony projects.
- Treat game, Unity, Harmony, and other runtime-provided assemblies as compile-time-only references (`Private="false"`).
- A normal build must not deploy implicitly. Deployment is opt-in through `ModOutputDir` and must preserve the deployment shape above.
