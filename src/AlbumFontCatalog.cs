using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Albummodelite
{
    internal sealed class AlbumFontOption
    {
        internal string Key;
        internal string DisplayName;
        internal Font Font;
        internal bool Packaged;
    }

    /// <summary>
    /// Shared native-Unity font catalog for the live cover designer and persisted cover renderer.
    ///
    /// CAA's four packaged faces are imported by Unity 2019.4.23f1 ahead of time and shipped in
    /// AlbumFonts/createanalbum_fonts. Loading them from an AssetBundle produces the same kind of
    /// UnityEngine.Font object used by Idol Manager's own imported FontFiles. Installed Windows
    /// faces use Unity's supported CreateDynamicFontFromOSFont path. Raw loose TTF/OTF files are
    /// intentionally not treated as supported runtime fonts because this Unity player does not
    /// expose its Editor TrueTypeFontImporter at runtime.
    /// </summary>
    internal static class AlbumFontCatalog
    {
        private static readonly List<AlbumFontOption> options = new List<AlbumFontOption>();
        // Keep AssetBundle late-bound. Idol Manager's shipped reference set exposes a reduced
        // compile-time UnityEngine.AssetBundle surface even though the player runtime provides the
        // full AssetBundleModule API. Reflection lets CAA invoke the real runtime API without
        // compiling against methods missing from the game's reference assemblies.
        private static object packagedFontBundle;
        private static Type assetBundleType;
        private static MethodInfo assetBundleLoadFromFile;
        private static MethodInfo assetBundleGetAllAssetNames;
        private static MethodInfo assetBundleLoadAssetByType;
        private static MethodInfo assetBundleLoadAssetGeneric;
        private static MethodInfo assetBundleUnload;
        private static bool loaded;

        private sealed class PackagedFont
        {
            internal string Key;
            internal string DisplayName;
            internal string BundleAssetName;
            internal string SourceFileName;
        }

        private sealed class SystemFont
        {
            internal string Key;
            internal string DisplayName;
            internal string[] FamilyCandidates;
        }

        // Keep the first four slots aligned with the legacy v2.7 enhancement and the
        // v4 Elegant/Classic/Bold/Script semantic slots so old FontIndex values migrate
        // predictably: Cormorant, Cinzel, Cyberthrone, Allura.
        private static readonly PackagedFont[] PackagedFonts =
        {
            new PackagedFont
            {
                Key = "packaged.cormorant-garamond",
                DisplayName = "Cormorant Garamond",
                BundleAssetName = "cormorant_garamond",
                SourceFileName = "CormorantGaramond-VariableFont_wght.ttf"
            },
            new PackagedFont
            {
                Key = "packaged.cinzel",
                DisplayName = "Cinzel",
                BundleAssetName = "cinzel",
                SourceFileName = "Cinzel-VariableFont_wght.ttf"
            },
            new PackagedFont
            {
                Key = "packaged.cyberthrone",
                DisplayName = "Cyberthrone",
                BundleAssetName = "cyberthrone",
                SourceFileName = "Cyberthrone.ttf"
            },
            new PackagedFont
            {
                Key = "packaged.allura",
                DisplayName = "Allura",
                BundleAssetName = "allura",
                SourceFileName = "Allura-Regular.ttf"
            }
        };

        // These were the four cover fonts used by pre-4.1 Create An Album releases. They are
        // already-installed operating-system fonts, so Unity can load them through its native
        // OS-font API without registering or rasterizing loose font files ourselves.
        private const int LegacySystemFontCount = 4;

        private static readonly SystemFont[] WindowsSystemFonts =
        {
            new SystemFont
            {
                Key = "system.georgia",
                DisplayName = "Georgia",
                FamilyCandidates = new[] { "Georgia" }
            },
            new SystemFont
            {
                Key = "system.times-new-roman",
                DisplayName = "Times New Roman",
                FamilyCandidates = new[] { "Times New Roman" }
            },
            new SystemFont
            {
                Key = "system.arial-black",
                DisplayName = "Arial Black",
                FamilyCandidates = new[] { "Arial Black" }
            },
            new SystemFont
            {
                Key = "system.segoe-script",
                DisplayName = "Segoe Script",
                FamilyCandidates = new[] { "Segoe Script" }
            }
        };

        internal static int Count
        {
            get
            {
                EnsureLoaded();
                return options.Count;
            }
        }

        internal static void EnsureLoaded()
        {
            if (loaded)
                return;

            loaded = true;
            options.Clear();

            LoadPackagedFontBundle();
            LoadWindowsSystemFonts();
            WarnAboutUnsupportedLooseFonts();

            if (options.Count == 0)
                AddGameFontFallback();
        }

        internal static string[] GetDisplayNames()
        {
            EnsureLoaded();
            return options.Select(o => o.DisplayName).ToArray();
        }

        internal static Font[] GetFonts()
        {
            EnsureLoaded();
            Font fallback = AlbumUiResources.GetGameFont();
            return options.Select(o => o.Font ?? fallback).ToArray();
        }

        internal static string GetKey(int index)
        {
            EnsureLoaded();
            if (options.Count == 0)
                return "game.default";

            index = Mathf.Clamp(index, 0, options.Count - 1);
            return options[index].Key;
        }

        internal static string GetLegacySystemKey(int legacyIndex)
        {
            // Save schema v1/v2 came from the pre-4.1 Cover Designer, whose four numeric slots
            // were Georgia, Times New Roman, Arial Black and Segoe Script. Return the stable key
            // even when that OS font is unavailable; keyed lookup then falls back to the matching
            // packaged semantic slot instead of drifting onto an unrelated font.
            if (legacyIndex >= 0 &&
                legacyIndex < LegacySystemFontCount &&
                legacyIndex < WindowsSystemFonts.Length)
                return WindowsSystemFonts[legacyIndex].Key;

            return GetKey(legacyIndex);
        }

        internal static int GetIndex(string key, int legacyIndex)
        {
            EnsureLoaded();
            if (!string.IsNullOrEmpty(key))
            {
                int match = options.FindIndex(
                    o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
                if (match >= 0)
                    return match;

                int systemFallback = GetSystemSemanticFallbackIndex(key);
                return systemFallback >= 0 ? systemFallback : 0;
            }

            return options.Count == 0
                ? 0
                : Mathf.Clamp(legacyIndex, 0, options.Count - 1);
        }

        internal static Font Resolve(string key, int legacyIndex)
        {
            EnsureLoaded();
            Font fallback = AlbumUiResources.GetGameFont();
            if (options.Count == 0)
                return fallback;

            int index = GetIndex(key, legacyIndex);
            return options[index].Font ?? fallback;
        }

        internal static void Shutdown()
        {
            options.Clear();

            if (packagedFontBundle != null)
            {
                try
                {
                    // Shutdown happens after returning to the main menu, where CAA cover objects
                    // from the gameplay scene are no longer needed. Destroy the imported bundle
                    // assets as well so repeated game sessions do not accumulate duplicate Fonts.
                    InvokeAssetBundleUnload(packagedFontBundle, true);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        "[AlbumFonts] Could not unload packaged font AssetBundle cleanly: " +
                        ex.Message);
                }

                packagedFontBundle = null;
            }

            loaded = false;
        }

        private static void LoadPackagedFontBundle()
        {
            string bundlePath = AlbumPaths.FontBundlePath;
            if (string.IsNullOrEmpty(bundlePath) || !File.Exists(bundlePath))
            {
                Debug.LogError(
                    "[AlbumFonts] Required native font AssetBundle is missing: " + bundlePath +
                    ". Expected the Unity 2019.4.23f1 Windows bundle at " +
                    "AlbumFonts/createanalbum_fonts.");
                AddMissingPackagedFallbacks();
                return;
            }

            try
            {
                EnsureAssetBundleApi();
                packagedFontBundle = assetBundleLoadFromFile.Invoke(null, new object[] { bundlePath });
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[AlbumFonts] Failed to load native font AssetBundle '" + bundlePath +
                    "': " + ex.Message);
                packagedFontBundle = null;
            }

            if (packagedFontBundle == null)
            {
                Debug.LogError(
                    "[AlbumFonts] Unity returned null for font AssetBundle: " + bundlePath +
                    ". Check that it was built for StandaloneWindows64 with Unity 2019.4.23f1.");
                AddMissingPackagedFallbacks();
                return;
            }

            string[] bundleAssetNames;
            try
            {
                object result = assetBundleGetAllAssetNames.Invoke(packagedFontBundle, null);
                bundleAssetNames = result as string[] ?? new string[0];
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumFonts] Could not enumerate font AssetBundle contents: " + ex.Message);
                bundleAssetNames = new string[0];
            }

            int loadedCount = 0;
            for (int i = 0; i < PackagedFonts.Length; i++)
            {
                PackagedFont packaged = PackagedFonts[i];
                Font font = TryLoadPackagedFont(packaged, bundleAssetNames);
                if (font == null)
                {
                    Debug.LogError(
                        "[AlbumFonts] AssetBundle is missing native Unity Font '" +
                        packaged.DisplayName + "' (address '" + packaged.BundleAssetName + "').");
                    AddUnavailablePackagedOption(packaged);
                    continue;
                }

                loadedCount++;
                options.Add(new AlbumFontOption
                {
                    Key = packaged.Key,
                    DisplayName = packaged.DisplayName,
                    Font = font,
                    Packaged = true
                });

                Debug.Log(
                    "[AlbumFonts] Loaded packaged Unity Font '" + packaged.DisplayName +
                    "' from AssetBundle as '" + font.name + "'.");
            }

            Debug.Log(
                "[AlbumFonts] Native font AssetBundle ready: " + loadedCount + "/" +
                PackagedFonts.Length + " packaged fonts loaded.");
        }

        private static Font TryLoadPackagedFont(
            PackagedFont packaged,
            string[] bundleAssetNames)
        {
            if (packagedFontBundle == null || packaged == null)
                return null;

            Font font = null;
            try
            {
                // The recommended CAA bundle builder assigns these short addressable names.
                font = InvokeAssetBundleLoadFont(packagedFontBundle, packaged.BundleAssetName);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumFonts] AssetBundle address '" + packaged.BundleAssetName +
                    "' could not be loaded: " + ex.Message);
            }

            if (font != null)
                return font;

            // Be tolerant of a bundle built without addressableNames. In that case Unity stores
            // the project asset path, so locate the imported font by its original filename.
            if (bundleAssetNames == null || bundleAssetNames.Length == 0)
                return null;

            string fileName = packaged.SourceFileName ?? string.Empty;
            for (int i = 0; i < bundleAssetNames.Length; i++)
            {
                string assetName = bundleAssetNames[i];
                if (string.IsNullOrEmpty(assetName))
                    continue;

                string candidateFileName;
                try
                {
                    candidateFileName = Path.GetFileName(assetName);
                }
                catch
                {
                    candidateFileName = assetName;
                }

                if (!string.Equals(
                        candidateFileName,
                        fileName,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    font = InvokeAssetBundleLoadFont(packagedFontBundle, assetName);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        "[AlbumFonts] AssetBundle path '" + assetName +
                        "' could not be loaded as Font: " + ex.Message);
                }

                if (font != null)
                    return font;
            }

            return null;
        }

        private static void EnsureAssetBundleApi()
        {
            if (assetBundleType != null &&
                assetBundleLoadFromFile != null &&
                assetBundleGetAllAssetNames != null &&
                assetBundleUnload != null &&
                (assetBundleLoadAssetByType != null || assetBundleLoadAssetGeneric != null))
                return;

            assetBundleType = FindUnityAssetBundleType();
            if (assetBundleType == null)
                throw new InvalidOperationException(
                    "UnityEngine.AssetBundle runtime type was not found. " +
                    "UnityEngine.AssetBundleModule may not be loaded by this Idol Manager build.");

            const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
            const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

            assetBundleLoadFromFile = assetBundleType.GetMethod(
                "LoadFromFile",
                PublicStatic,
                null,
                new[] { typeof(string) },
                null);

            assetBundleGetAllAssetNames = assetBundleType.GetMethod(
                "GetAllAssetNames",
                PublicInstance,
                null,
                Type.EmptyTypes,
                null);

            assetBundleLoadAssetByType = assetBundleType.GetMethod(
                "LoadAsset",
                PublicInstance,
                null,
                new[] { typeof(string), typeof(Type) },
                null);

            assetBundleLoadAssetGeneric = assetBundleType
                .GetMethods(PublicInstance)
                .FirstOrDefault(method =>
                    method.Name == "LoadAsset" &&
                    method.IsGenericMethodDefinition &&
                    method.GetGenericArguments().Length == 1 &&
                    method.GetParameters().Length == 1 &&
                    method.GetParameters()[0].ParameterType == typeof(string));

            assetBundleUnload = assetBundleType.GetMethod(
                "Unload",
                PublicInstance,
                null,
                new[] { typeof(bool) },
                null);

            if (assetBundleLoadFromFile == null ||
                assetBundleGetAllAssetNames == null ||
                assetBundleUnload == null ||
                (assetBundleLoadAssetByType == null && assetBundleLoadAssetGeneric == null))
            {
                throw new MissingMethodException(
                    "The loaded UnityEngine.AssetBundle runtime type does not expose the APIs " +
                    "required by Create An Album (LoadFromFile, GetAllAssetNames, LoadAsset, Unload). " +
                    "Runtime assembly: " + assetBundleType.Assembly.FullName);
            }

            Debug.Log(
                "[AlbumFonts] Bound native AssetBundle runtime API from " +
                assetBundleType.Assembly.FullName + ".");
        }

        private static Type FindUnityAssetBundleType()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type candidate = null;
                try
                {
                    candidate = assemblies[i].GetType("UnityEngine.AssetBundle", false);
                }
                catch
                {
                    // Ignore partially-loadable unrelated assemblies and keep searching.
                }

                if (candidate != null && HasAssetBundleLoadFromFile(candidate))
                    return candidate;
            }

            // AssetBundleModule may not have been touched yet. Asking the CLR to load the Unity
            // module by its normal assembly name is safe in the game process and does not introduce
            // a compile-time reference from CAA.
            try
            {
                Assembly module = Assembly.Load("UnityEngine.AssetBundleModule");
                Type candidate = module == null
                    ? null
                    : module.GetType("UnityEngine.AssetBundle", false);
                if (candidate != null && HasAssetBundleLoadFromFile(candidate))
                    return candidate;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumFonts] Could not explicitly load UnityEngine.AssetBundleModule: " +
                    ex.Message);
            }

            // Last chance: return any loaded AssetBundle type so EnsureAssetBundleApi can produce a
            // precise missing-method diagnostic rather than a misleading type-not-found message.
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    Type candidate = assemblies[i].GetType("UnityEngine.AssetBundle", false);
                    if (candidate != null)
                        return candidate;
                }
                catch
                {
                }
            }

            return null;
        }

        private static bool HasAssetBundleLoadFromFile(Type candidate)
        {
            if (candidate == null)
                return false;

            return candidate.GetMethod(
                "LoadFromFile",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null) != null;
        }

        private static Font InvokeAssetBundleLoadFont(object bundle, string assetName)
        {
            if (bundle == null || string.IsNullOrEmpty(assetName))
                return null;

            EnsureAssetBundleApi();

            object result;
            if (assetBundleLoadAssetByType != null)
            {
                result = assetBundleLoadAssetByType.Invoke(
                    bundle,
                    new object[] { assetName, typeof(Font) });
            }
            else
            {
                MethodInfo closed = assetBundleLoadAssetGeneric.MakeGenericMethod(typeof(Font));
                result = closed.Invoke(bundle, new object[] { assetName });
            }

            return result as Font;
        }

        private static void InvokeAssetBundleUnload(object bundle, bool unloadAllLoadedObjects)
        {
            if (bundle == null)
                return;

            EnsureAssetBundleApi();
            assetBundleUnload.Invoke(bundle, new object[] { unloadAllLoadedObjects });
        }

        private static void AddMissingPackagedFallbacks()
        {
            for (int i = 0; i < PackagedFonts.Length; i++)
                AddUnavailablePackagedOption(PackagedFonts[i]);
        }

        private static void AddUnavailablePackagedOption(PackagedFont packaged)
        {
            if (packaged == null)
                return;

            options.Add(new AlbumFontOption
            {
                Key = packaged.Key,
                DisplayName = packaged.DisplayName + " (Bundle Missing)",
                Font = AlbumUiResources.GetGameFont(),
                Packaged = true
            });
        }

        private static void LoadWindowsSystemFonts()
        {
            if (!IsWindows())
                return;

            string[] installed;
            try
            {
                installed = Font.GetOSInstalledFontNames() ?? new string[0];
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumFonts] Unity could not enumerate installed Windows fonts: " +
                    ex.Message);
                return;
            }

            for (int i = 0; i < WindowsSystemFonts.Length; i++)
                AddSystemFontOption(WindowsSystemFonts[i], installed);
        }

        private static void AddSystemFontOption(SystemFont systemFont, string[] installed)
        {
            if (systemFont == null ||
                string.IsNullOrWhiteSpace(systemFont.Key) ||
                systemFont.FamilyCandidates == null ||
                systemFont.FamilyCandidates.Length == 0 ||
                options.Any(o => string.Equals(
                    o.Key, systemFont.Key, StringComparison.OrdinalIgnoreCase)))
                return;

            string visibleFamily = null;
            for (int i = 0; i < systemFont.FamilyCandidates.Length && visibleFamily == null; i++)
            {
                string candidate = systemFont.FamilyCandidates[i];
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                visibleFamily = installed.FirstOrDefault(
                    name => string.Equals(
                        name,
                        candidate,
                        StringComparison.OrdinalIgnoreCase));
            }

            // Direct lookup is still worth trying if Unity's installed-font enumeration is stale,
            // but unlike the old CAA path we do not register private files or accept an unverifiable
            // fallback face. These choices are genuine operating-system fonts only.
            string[] lookupCandidates = visibleFamily == null
                ? systemFont.FamilyCandidates
                : new[] { visibleFamily };

            Font font = null;
            string resolvedFamily = null;
            for (int i = 0; i < lookupCandidates.Length; i++)
            {
                string candidate = lookupCandidates[i];
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                try
                {
                    Font candidateFont = Font.CreateDynamicFontFromOSFont(candidate, 32);
                    if (candidateFont == null)
                        continue;

                    font = candidateFont;
                    resolvedFamily = candidate;
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        "[AlbumFonts] Unity rejected installed font family '" + candidate +
                        "': " + ex.Message);
                }
            }

            if (font == null)
            {
                Debug.LogWarning(
                    "[AlbumFonts] Installed system font '" + systemFont.DisplayName +
                    "' is unavailable through Unity; omitting it from the cover font list.");
                return;
            }

            options.Add(new AlbumFontOption
            {
                Key = systemFont.Key,
                DisplayName = systemFont.DisplayName,
                Font = font,
                Packaged = false
            });

            Debug.Log(
                "[AlbumFonts] Loaded installed system font '" + systemFont.DisplayName +
                "' through Unity family '" + resolvedFamily + "'.");
        }

        private static int GetSystemSemanticFallbackIndex(string key)
        {
            if (string.IsNullOrEmpty(key))
                return -1;

            int count = Math.Min(
                LegacySystemFontCount,
                Math.Min(WindowsSystemFonts.Length, PackagedFonts.Length));
            for (int i = 0; i < count; i++)
            {
                if (!string.Equals(
                        WindowsSystemFonts[i].Key,
                        key,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                // The packaged choices occupy the first four stable semantic slots even if a
                // particular asset failed and is represented by a visible Bundle Missing fallback.
                return i < options.Count ? i : 0;
            }

            return -1;
        }

        private static void WarnAboutUnsupportedLooseFonts()
        {
            string directory = AlbumPaths.CustomFontsDirectory;
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return;

            string[] files;
            try
            {
                files = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories)
                    .Where(IsLooseFontFile)
                    .ToArray();
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumFonts] Could not inspect legacy CustomFonts directory: " + ex.Message);
                return;
            }

            if (files.Length == 0)
                return;

            Debug.LogWarning(
                "[AlbumFonts] Ignoring " + files.Length +
                " loose CustomFonts TTF/OTF file(s). CAA 4.2.2 no longer uses the broken " +
                "GDI/private-registration path. Package additional fonts as Unity 2019.4.23f1 " +
                "Font assets in createanalbum_fonts instead.");
        }

        private static bool IsLooseFontFile(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".ttf", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".otf", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddGameFontFallback()
        {
            Font fallback = AlbumUiResources.GetGameFont();
            options.Add(new AlbumFontOption
            {
                Key = "game.default",
                DisplayName = "Game Font",
                Font = fallback,
                Packaged = false
            });
        }

        private static bool IsWindows()
        {
            return Application.platform == RuntimePlatform.WindowsPlayer ||
                   Application.platform == RuntimePlatform.WindowsEditor;
        }
    }
}
