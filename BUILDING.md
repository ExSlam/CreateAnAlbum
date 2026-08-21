# Building Create An Album

The project follows the Cosmo Harmony mod layout and targets .NET Framework 4.6 (`net46`). Repository-wide identity, integration, persistence, and directory requirements are in [AGENTS.md](AGENTS.md).

By default, reference assemblies are read from the shared sibling `..\dll` directory. Override that location with `/p:dllDir="X:\path\to\reference-dlls"` when needed. The project does **not** reference BepInEx, Rivals Reborn, IM Data Core, or Save Write Ordering Fix at compile time. Optional runtime integrations are reflection-only or Harmony-ordering-only.

Build a release DLL with:

`dotnet build CreateAnAlbum.csproj -c Release`

Required reference DLLs named by the project:

- `Assembly-CSharp.dll`
- `Assembly-CSharp-firstpass.dll`
- `0Harmony.dll`
- `UnityEngine.CoreModule.dll`
- `UnityEngine.UIModule.dll`
- `UnityEngine.UI.dll`
- `UnityEngine.TextRenderingModule.dll`
- `UnityEngine.TextCoreModule.dll`
- `UnityEngine.JSONSerializeModule.dll`
- `UnityEngine.InputLegacyModule.dll`
- `Unity.TextMeshPro.dll`
- `UnityEngine.ImageConversionModule.dll`

All game, Unity, and Harmony references are compile-time only (`Private="false"`) and are not copied into the output directory.

The compiled assembly is written to `bin\Release\net46\com.jordanss.createanalbum.dll`.

## Packaging and deployment

A normal build does not deploy the mod. Supply `ModOutputDir` to copy the DLL and the contents of `assets` into a `CreateAnAlbum` child directory:

```powershell
$modsRoot = Join-Path $env:USERPROFILE 'AppData\LocalLow\Glitch Pitch\Idol Manager\Mods'
dotnet build CreateAnAlbum.csproj -c Release -p:ModOutputDir="$modsRoot"
```

That produces or updates:

`%USERPROFILE%\AppData\LocalLow\Glitch Pitch\Idol Manager\Mods\CreateAnAlbum`

For manual installation, copy `com.jordanss.createanalbum.dll` and the **contents** of `assets` into that directory. Do not put an `assets` wrapper inside the installed mod. The four packaged `.ttf` files must remain in `AlbumFonts`. A `CustomFonts` sibling directory is created at runtime for user-supplied `.ttf` files and is not part of the source asset tree. On Windows, 4.1.2 resolves the TTF name table, temporarily registers those font resources at runtime, and verifies them against Unity's installed-font enumeration; test logs should contain `[AlbumFonts] Loaded ... as Unity family ...` entries for successful packaged-font resolution. `AlbumBackgrounds` is scanned recursively at runtime for `.png`, `.jpg`, and `.jpeg` images, so additional background files or subfolders can be added without rebuilding the mod.

The complete expected deployment tree is documented in [AGENTS.md](AGENTS.md).
