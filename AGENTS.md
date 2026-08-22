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
- Every button whose action is explicitly **Close** in a Create An Album UI must use `AlbumButtonStyle.Destructive`: red action styling with white text. Do not introduce neutral/outline Close buttons.
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
- Use Create An Album namespace `com.jordanss.createanalbum` and custom JSON key `state_v3` for the unified schema-v4 document. The historical key name remains stable for migration.
- Album save identity must follow the concrete vanilla save/checkpoint path **relative to** `Application.persistentDataPath/data`. Never persist or hash an OS/user-specific absolute path as the logical identity, and do not collapse distinct manual saves or story checkpoints back to a campaign-folder-only identity. Mirror that relative path below `Application.persistentDataPath/CreateAnAlbum`.
- Keep **loaded identity** separate from **save destination**. Autosave, Save As, chapter save, and manual-save writes may create/fork checkpoints, but they must never rebind the in-memory loaded album/production state; only a real load may do that.
- `SaveEvent` may stage the unified schema document, but the IM Data Core mutation must be committed at the exact concrete `SavedData` write target before IMDC prepares/forks that checkpoint. On load, a valid exact logical-slot CAA mirror is authoritative; IMDC is secondary/recovery and must not replace that mirror solely because its wall-clock timestamp is newer.
- Do not authoritatively write an empty supplemental checkpoint while CAA is still restoring a vanilla load.
- The exact logical-slot standalone mirror is the authoritative CAA load source when valid and also serves as the migration anchor; IM Data Core is the secondary/recovery mirror. They must receive the same staged schema document at save boundaries rather than advancing independently. Existing-but-unusable supplemental data must never be converted into an authoritative empty album document.

Create An Album embeds a minimal SavedData write-ordering fallback and is designed to coexist with Cosmo's standalone Save Write Ordering Fix.

- Never Harmony-patch generic `DataSaver<T>`.
- Save interception belongs on the known concrete Idol Manager SavedData callers: `SaveManager.SaveData(bool, bool)`, `SaveManager.SaveChapter(tasks._chapter)`, `Popup_Save.Save()`, `Popup_Load_Story.Do_Overwrite_Save(save_info)`, and `Popup_Load_Story.Do_New_Save(string)`.
- The CAA persistence transpiler must leave the final SavedData writer instruction in place and run **before** `com.cosmo.imdatacore` so CAA's staged custom JSON is present when IMDC prepares/forks the target checkpoint.
- The embedded ordering transpiler runs last, freezes SavedData synchronously, and queues FIFO per physical path. It must disable itself when Harmony owner `com.cosmo.savewriteorderingfix` is present so the standalone implementation can own the final writer/read coordination instead of being double-patched.
- Known caller-level `DataSaver.loadData<SavedData>` reads must wait for CAA's embedded pending write for the same path when the standalone SWO owner is absent.

## Cover assets and chart rendering

- Album backgrounds belong in `assets/AlbumBackgrounds` and are discovered dynamically and recursively at runtime. Supported file types are `.png`, `.jpg`, and `.jpeg`; do not hard-code a background count or filename list into gameplay/UI code.
- Persisted background identity uses stable relative-path `BackgroundKey` values. Keep `BackgroundIndex` only for migration/backward compatibility, and keep the Cover Designer and final renderer on the same shared background catalog.
- The background picker must remain horizontally scrollable and retain enough side padding that thumbnail outlines are not clipped by its mask.
- Cover Design control changes must preserve both the outer page scroll and the nested left-options scroll across rebuilt layouts; do not call the generic refresh path from cover-control callbacks without the delayed multi-frame position restore.
- Cover Design footer navigation (Back and Continue/Release progression) must be created independently of preview rendering so a cover/font exception can never trap the user on the page.
- Packaged fonts belong in `assets/AlbumFonts/createanalbum_fonts` and are resolved through the shared album font catalog for both live previews and persisted cover rendering. The bundle contains Unity-imported `Font` assets built for `StandaloneWindows64` with Unity 2019.4.23f1. Keep explicit success/failure diagnostics and preserve the stable packaged key order: Cormorant Garamond, Cinzel, Cyberthrone, Allura.
- Do not reintroduce private/session Windows font registration, Win32 GDI font rasterization, or `System.Drawing` for CAA packaged fonts. Cover typography uses ordinary Unity UI `Text` with native `UnityEngine.Font` objects.
- The historical Windows choices may use `Font.CreateDynamicFontFromOSFont` only when the family is already installed and Unity can resolve it.
- Persisted font identity uses stable `FontKey` values. Keep `FontIndex` only for migration/backward compatibility.
- Loose `CustomFonts/*.ttf` / `.otf` files are not a supported runtime font source in 4.2.2. Existing directories may be detected for diagnostics but must not be privately registered or rasterized.
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
|   |   |-- createanalbum_fonts (required runtime AssetBundle; supplied for release packaging)
|   |   |-- Allura-Regular.ttf (bundle source)
|   |   |-- Cinzel-VariableFont_wght.ttf (bundle source)
|   |   |-- CormorantGaramond-VariableFont_wght.ttf (bundle source)
|   |   `-- Cyberthrone.ttf (bundle source)
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
|   |-- createanalbum_fonts
|   |-- Allura-Regular.ttf (optional bundle source; not loaded at runtime)
|   |-- Cinzel-VariableFont_wght.ttf (optional bundle source; not loaded at runtime)
|   |-- CormorantGaramond-VariableFont_wght.ttf (optional bundle source; not loaded at runtime)
|   `-- Cyberthrone.ttf (optional bundle source; not loaded at runtime)
|-- CustomFonts/ (legacy user directory; ignored except for diagnostics)
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
- Validate `assets/info.json`, `assets/ModButtons/buttons.json`, and edited localization files before handoff.
- When compilation is part of the available workflow, run clean Debug and Release builds. When a handoff is explicitly source-only, perform delimiter/syntax-oriented static checks and leave compilation and in-game verification to the receiving developer.
