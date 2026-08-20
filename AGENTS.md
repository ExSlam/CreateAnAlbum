# Create An Album Repository Instructions

These instructions apply to the entire repository. Read them before changing code, metadata, assets, build configuration, or documentation.

## Identity

- The canonical internal mod identifier is `com.jordanss.createanalbum`.
- The player-facing display name is `Create An Album`.
- The mod was formerly referred to as `AlbumModLite`.
- Keep the project `AssemblyName`, `assets/info.json` `HarmonyID`, and every Mod Buttons `assembly` value synchronized as `com.jordanss.createanalbum`.
- The installed folder name is `CreateAnAlbum` (without spaces).
- `Albummodelite` is a legacy CLR namespace still used by the current source and reflection targets. Do not treat it as the canonical mod identifier or rename it opportunistically.

## Runtime Model

Create An Album is a Harmony **mod** for Idol Manager using [IM-HarmonyIntegration](https://github.com/ui3TD/IM-HarmonyIntegration). It is not a raw BepInEx **plugin**.

- HarmonyIntegration discovers and applies this assembly's Harmony patches.
- Do not add a `BaseUnityPlugin`, a self-owned `Harmony.PatchAll()` bootstrap, or a BepInEx `Paths` dependency.
- Game UI must continue to use Idol Manager's `PopupManager` lifecycle for opening and closing Create An Album popups.

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
- A missing, disabled, or incompatible Rivals Reborn installation must not stop the base mod from loading.

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
|   |   |-- aurora.png
|   |   |-- desert.png
|   |   |-- Dreamy.png
|   |   |-- eclipse.png
|   |   |-- floral art-deco.png
|   |   |-- floral.png
|   |   |-- Geometric.png
|   |   |-- neon.png
|   |   `-- Winter.png
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
|   |-- aurora.png
|   |-- desert.png
|   |-- Dreamy.png
|   |-- eclipse.png
|   |-- floral art-deco.png
|   |-- floral.png
|   |-- Geometric.png
|   |-- neon.png
|   `-- Winter.png
|-- AlbumFonts/
|   |-- Allura-Regular.ttf
|   |-- Cinzel-VariableFont_wght.ttf
|   |-- CormorantGaramond-VariableFont_wght.ttf
|   `-- Cyberthrone.ttf
|-- Localization/
|   `-- <language-code>/
|       |-- strings.txt
|       `-- JSON/... (when supplied)
`-- ModButtons/
    `-- buttons.json
```

The live installation does not require the PDB, source files, project files, `bin`, `obj`, `dll`, or an `assets` wrapper directory.

## Build Expectations

- Target .NET Framework 4.6 (`net46`) to match Idol Manager and the working Cosmo Harmony projects.
- Treat game, Unity, Harmony, and other runtime-provided assemblies as compile-time-only references (`Private="false"`).
- A normal build must not deploy implicitly. Deployment is opt-in through `ModOutputDir` and must preserve the deployment shape above.
- Before handing off code or configuration changes, run clean Debug and Release builds and validate `assets/ModButtons/buttons.json` plus every edited localization file.
