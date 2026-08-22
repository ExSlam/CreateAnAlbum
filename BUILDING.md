# Building Create An Album

The project follows the Cosmo Harmony mod layout and targets .NET Framework 4.6 (`net46`). Repository-wide identity, integration, persistence, and directory requirements are in [AGENTS.md](AGENTS.md).

By default, reference assemblies are read from the shared sibling `..\dll` directory. Override that location with `/p:dllDir="X:\path\to\reference-dlls"` when needed. The project does **not** reference BepInEx, Rivals Reborn, IM Data Core, or Save Write Ordering Fix at compile time. IM Data Core remains reflection-only. Create An Album includes its own small caller-level SavedData ordering fallback and detects the standalone Save Write Ordering Fix only by Harmony owner ID so the external mod can take precedence when installed.

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
- `UnityEngine.AssetBundleModule.dll`

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

For manual installation, copy `com.jordanss.createanalbum.dll` and the **contents** of `assets` into that directory. Do not put an `assets` wrapper inside the installed mod. Version 4.2.2 requires the Unity font AssetBundle at `AlbumFonts/createanalbum_fonts`. Build it for `StandaloneWindows64` with Unity 2019.4.23f1 and include Cormorant Garamond, Cinzel, Cyberthrone, and Allura as imported `Font` assets; the recommended addressable names are `cormorant_garamond`, `cinzel`, `cyberthrone`, and `allura`. The raw TTF files may remain in the source tree for reproducible bundle authoring, but CAA does not load them at runtime. Loose `CustomFonts` files and the former Win32 GDI/private-registration path are not supported in 4.2.2. `AlbumBackgrounds` is scanned recursively at runtime for `.png`, `.jpg`, and `.jpeg` images, so additional background files or subfolders can be added without rebuilding the mod.

The complete expected deployment tree is documented in [AGENTS.md](AGENTS.md).
