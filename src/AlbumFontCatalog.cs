using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Albummodelite
{
    internal sealed class AlbumFontOption
    {
        internal string Key;
        internal string DisplayName;
        internal string FamilyName;
        internal string[] FamilyCandidates;
        internal Font Font;
        internal bool UnityResolved;
        internal string FilePath;
        internal string RasterizerIdentity;
        internal bool Packaged;
    }

    /// <summary>
    /// Shared font source for the live cover designer and persisted cover renderer.
    ///
    /// Unity 2019 does not reliably refresh CreateDynamicFontFromOSFont after a font is added
    /// only with FR_PRIVATE. Resolve each TTF through several embedded name-table identities,
    /// notify Windows that the font table changed, and if private registration is still
    /// invisible to Unity, temporarily register the resource session-wide for the lifetime of
    /// the game process. Every registration is removed during Shutdown.
    /// </summary>
    internal static class AlbumFontCatalog
    {
        private const uint FrPrivate = 0x10;
        private const uint FrSession = 0x00;
        private const uint WmFontChange = 0x001D;
        private static readonly IntPtr HwndBroadcast = new IntPtr(0xFFFF);

        private static readonly List<AlbumFontOption> options = new List<AlbumFontOption>();
        private static readonly List<RegisteredFontResource> registeredResources =
            new List<RegisteredFontResource>();
        private static bool loaded;

        private sealed class RegisteredFontResource
        {
            internal string Path;
            internal uint Flags;
        }

        private sealed class PackagedFont
        {
            internal string Key;
            internal string DisplayName;
            internal string FileName;
            internal string PreferredFamily;
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
                FileName = "CormorantGaramond-VariableFont_wght.ttf",
                PreferredFamily = "Cormorant Garamond"
            },
            new PackagedFont
            {
                Key = "packaged.cinzel",
                DisplayName = "Cinzel",
                FileName = "Cinzel-VariableFont_wght.ttf",
                PreferredFamily = "Cinzel"
            },
            new PackagedFont
            {
                Key = "packaged.cyberthrone",
                DisplayName = "Cyberthrone",
                FileName = "Cyberthrone.ttf",
                PreferredFamily = "Cyberthrone"
            },
            new PackagedFont
            {
                Key = "packaged.allura",
                DisplayName = "Allura",
                FileName = "Allura-Regular.ttf",
                PreferredFamily = "Allura"
            }
        };

        // These were the four cover fonts used by pre-4.1 Create An Album releases. Keep
        // them as explicit stable-key choices on Windows, but append them after the packaged
        // slots so v4.x numeric FontIndex migration remains unchanged. A system font is added
        // only when Unity or Win32 GDI can positively resolve the requested family.
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

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int AddFontResourceEx(string fileName, uint flags, IntPtr reserved);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool RemoveFontResourceEx(string fileName, uint flags, IntPtr reserved);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SendNotifyMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

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

            for (int i = 0; i < PackagedFonts.Length; i++)
            {
                PackagedFont packaged = PackagedFonts[i];
                string path = Path.Combine(AlbumPaths.FontsDirectory, packaged.FileName);
                AddFileOption(
                    packaged.Key,
                    packaged.DisplayName,
                    path,
                    packaged.PreferredFamily,
                    true);
            }

            LoadWindowsSystemFonts();
            LoadCustomFonts();

            if (options.Count == 0)
            {
                Font fallback = AlbumUiResources.GetGameFont();
                options.Add(new AlbumFontOption
                {
                    Key = "game.default",
                    DisplayName = "Game Font",
                    FamilyName = fallback != null ? fallback.name : "Arial",
                    FamilyCandidates = new[] { fallback != null ? fallback.name : "Arial" },
                    Font = fallback,
                    UnityResolved = fallback != null,
                    FilePath = string.Empty,
                    RasterizerIdentity = "game.default",
                    Packaged = false
                });
            }
        }

        internal static string[] GetDisplayNames()
        {
            EnsureLoaded();
            return options.Select(o => o.DisplayName).ToArray();
        }

        internal static Font[] GetFonts()
        {
            EnsureLoaded();
            return options.Select(o => o.Font ?? AlbumUiResources.GetGameFont()).ToArray();
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
            // Save schema v1/v2 came from the pre-4.1 Cover Designer, whose four numeric
            // slots were Georgia, Times New Roman, Arial Black and Segoe Script. Return the
            // stable system key even when that font is unavailable on the current machine;
            // keyed lookup can then fall back safely now and recover the intended face later.
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

                // A keyed 4.1+ save must not silently drift onto another custom font when a
                // font file is removed or the custom-font list changes. The four known Windows
                // system keys are the exception: when unavailable (for example after moving a
                // save to another OS), fall back to their matching packaged semantic slot.
                int systemFallback = GetSystemSemanticFallbackIndex(key);
                return systemFallback >= 0 ? systemFallback : 0;
            }

            return options.Count == 0
                ? 0
                : Mathf.Clamp(legacyIndex, 0, options.Count - 1);
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
                if (string.Equals(
                        WindowsSystemFonts[i].Key,
                        key,
                        StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        internal static Font Resolve(string key, int legacyIndex)
        {
            EnsureLoaded();
            if (options.Count == 0)
                return AlbumUiResources.GetGameFont();

            int index = GetIndex(key, legacyIndex);
            return options[index].Font ?? AlbumUiResources.GetGameFont();
        }

        internal static string GetFilePath(string key, int legacyIndex)
        {
            EnsureLoaded();
            if (options.Count == 0)
                return string.Empty;
            int index = GetIndex(key, legacyIndex);
            return options[index].FilePath ?? string.Empty;
        }

        internal static Sprite RenderTextSprite(
            string key, int legacyIndex, string text, int width, int height,
            int pixelSize, Color color, TextAnchor alignment)
        {
            EnsureLoaded();
            if (options.Count == 0)
                return null;

            int index = GetIndex(key, legacyIndex);
            AlbumFontOption option = options[index];
            if (option == null || option.FamilyCandidates == null ||
                option.FamilyCandidates.Length == 0)
                return null;

            // Prefer Unity's real dynamic Font when it was positively resolved. This keeps
            // normal Unity Text behavior (including best-fit) and reserves GDI for the exact
            // case it was designed for: a requested face that Unity 2019 cannot expose. Returning
            // null here deliberately tells the cover renderer to use option.Font.
            if (option.UnityResolved && option.Font != null)
                return null;

            try
            {
                return AlbumFontRasterizer.Render(
                    string.IsNullOrEmpty(option.RasterizerIdentity)
                        ? option.Key
                        : option.RasterizerIdentity,
                    option.FamilyCandidates ?? new[] { option.FamilyName },
                    text,
                    width,
                    height,
                    pixelSize,
                    color,
                    alignment);
            }
            catch (Exception ex)
            {
                // Cover rendering must never disappear merely because a custom/system font
                // failed. Returning null deliberately hands control to the normal game-font
                // Text fallback in AlbumPopup/AlbumCoverRenderer.
                Debug.LogWarning(
                    "[AlbumFonts] Font rasterizer failed before drawing: " + ex.Message +
                    ". Using the game-font fallback.");
                return null;
            }
        }

        internal static void TrackRenderedSpriteUsage(GameObject owner, Sprite sprite)
        {
            AlbumFontRasterizer.TrackUsage(owner, sprite);
        }

        internal static void Shutdown()
        {
            if (IsWindows())
            {
                for (int i = registeredResources.Count - 1; i >= 0; i--)
                {
                    RegisteredFontResource resource = registeredResources[i];
                    try
                    {
                        RemoveFontResourceEx(resource.Path, resource.Flags, IntPtr.Zero);
                    }
                    catch
                    {
                    }
                }

                NotifyWindowsFontTableChanged();
            }

            registeredResources.Clear();
            AlbumFontRasterizer.Shutdown();
            options.Clear();
            loaded = false;
        }

        private static void LoadWindowsSystemFonts()
        {
            if (!IsWindows())
                return;

            for (int i = 0; i < WindowsSystemFonts.Length; i++)
                AddSystemFontOption(WindowsSystemFonts[i]);
        }

        private static void AddSystemFontOption(SystemFont systemFont)
        {
            if (systemFont == null ||
                string.IsNullOrWhiteSpace(systemFont.Key) ||
                systemFont.FamilyCandidates == null ||
                systemFont.FamilyCandidates.Length == 0 ||
                options.Any(o => string.Equals(
                    o.Key, systemFont.Key, StringComparison.OrdinalIgnoreCase)))
                return;

            List<string> candidates = new List<string>();
            for (int i = 0; i < systemFont.FamilyCandidates.Length; i++)
                InsertCandidate(candidates, systemFont.FamilyCandidates[i]);

            string resolvedFamily;
            Font font = TryCreateUnityFont(candidates, false, out resolvedFamily);
            bool unityResolved = font != null;
            bool gdiResolved = unityResolved ||
                AlbumFontRasterizer.CanResolve(candidates.ToArray());

            if (!unityResolved && !gdiResolved)
            {
                Debug.LogWarning(
                    "[AlbumFonts] Windows system font '" + systemFont.DisplayName +
                    "' is not available to Unity or Win32 GDI; omitting it from the cover font list.");
                return;
            }

            if (font == null)
            {
                font = AlbumUiResources.GetGameFont();
                resolvedFamily = candidates[0];
                Debug.Log(
                    "[AlbumFonts] Loaded Windows system font '" +
                    systemFont.DisplayName +
                    "' through Win32 GDI cover rasterization.");
            }
            else
            {
                Debug.Log(
                    "[AlbumFonts] Loaded Windows system font '" +
                    systemFont.DisplayName +
                    "' through Unity as family '" + resolvedFamily + "'.");
            }

            options.Add(new AlbumFontOption
            {
                Key = systemFont.Key,
                DisplayName = systemFont.DisplayName,
                FamilyName = string.IsNullOrEmpty(resolvedFamily)
                    ? candidates[0]
                    : resolvedFamily,
                FamilyCandidates = candidates.ToArray(),
                Font = font,
                UnityResolved = unityResolved,
                FilePath = string.Empty,
                RasterizerIdentity = systemFont.Key,
                Packaged = false
            });
        }

        private static void LoadCustomFonts()
        {
            string directory = AlbumPaths.CustomFontsDirectory;
            try
            {
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumFonts] Could not prepare CustomFonts directory: " + ex.Message);
                return;
            }

            string[] files;
            try
            {
                files = Directory
                    .GetFiles(directory, "*.*", SearchOption.AllDirectories)
                    .Where(IsSupportedCustomFontFile)
                    .ToArray();
                Array.Sort(files, delegate(string left, string right)
                {
                    bool leftTopLevel = IsTopLevelCustomFont(directory, left);
                    bool rightTopLevel = IsTopLevelCustomFont(directory, right);
                    if (leftTopLevel != rightTopLevel)
                        return leftTopLevel ? -1 : 1;

                    return StringComparer.OrdinalIgnoreCase.Compare(
                        GetCustomRelativePath(directory, left),
                        GetCustomRelativePath(directory, right));
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumFonts] Could not enumerate custom fonts: " + ex.Message);
                return;
            }

            foreach (string file in files)
            {
                List<string> names = TryReadTtfNames(file);
                string display = names.Count > 0
                    ? names[0]
                    : Path.GetFileNameWithoutExtension(file)
                        .Replace('-', ' ')
                        .Replace('_', ' ')
                        .Trim();

                if (string.IsNullOrWhiteSpace(display))
                    continue;

                string relativePath = GetCustomRelativePath(directory, file);
                string baseKey = "custom." +
                    SanitizeKey(Path.GetFileNameWithoutExtension(file));
                string key = MakeUniqueCustomKey(baseKey, relativePath);
                AddFileOption(key, display + " (Custom)", file, display, false);
            }
        }

        private static bool IsSupportedCustomFontFile(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".ttf", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".otf", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTopLevelCustomFont(string directory, string path)
        {
            string relative = GetCustomRelativePath(directory, path);
            return relative.IndexOf(Path.DirectorySeparatorChar) < 0 &&
                   relative.IndexOf(Path.AltDirectorySeparatorChar) < 0;
        }

        private static string GetCustomRelativePath(string directory, string path)
        {
            try
            {
                string root = Path.GetFullPath(directory)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;
                string fullPath = Path.GetFullPath(path);
                if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return fullPath.Substring(root.Length);
            }
            catch
            {
            }

            return Path.GetFileName(path) ?? path ?? string.Empty;
        }

        private static string MakeUniqueCustomKey(string baseKey, string relativePath)
        {
            if (!options.Any(o => string.Equals(
                    o.Key, baseKey, StringComparison.OrdinalIgnoreCase)))
                return baseKey;

            string suffix = StablePathHash(relativePath);
            string candidate = baseKey + "." + suffix;
            int collision = 2;
            while (options.Any(o => string.Equals(
                o.Key, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                candidate = baseKey + "." + suffix + "." + collision;
                collision++;
            }
            return candidate;
        }

        private static string StablePathHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                string normalized = (value ?? string.Empty)
                    .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                    .ToLowerInvariant();
                for (int i = 0; i < normalized.Length; i++)
                {
                    hash ^= normalized[i];
                    hash *= 16777619;
                }
                return hash.ToString("x8");
            }
        }

        private static void AddFileOption(
            string key,
            string displayName,
            string path,
            string preferredFamily,
            bool packaged)
        {
            if (options.Any(o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase)))
                return;

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogWarning(
                    "[AlbumFonts] " + (packaged ? "Packaged" : "Custom") +
                    " font file is missing: " + path);
                AddFallbackOption(key, displayName, preferredFamily, packaged);
                return;
            }

            List<string> candidates = TryReadTtfNames(path);
            // Prefer names embedded in the font plus our known family alias. The filename is
            // deliberately not treated as a family name: variable-font filenames often contain
            // style/axis suffixes that Unity cannot resolve through OS font lookup.
            InsertCandidate(candidates, preferredFamily);

            Font font = null;
            string resolvedFamily = string.Empty;

            bool registeredForUnityLookup = false;
            if (IsWindows())
            {
                // Register the file before the first Unity font query. Unity 2019 can cache its
                // native installed-family enumeration independently of CreateDynamicFontFromOSFont,
                // so use both private and temporary session-visible registrations. The resolver
                // first accepts an enumerated family, then performs a direct lookup and verifies
                // the Font object's own naming metadata. Both registrations are removed on Shutdown.
                bool privateRegistered = RegisterFontResource(path, FrPrivate);
                bool sessionRegistered = RegisterFontResource(path, FrSession);
                registeredForUnityLookup = privateRegistered || sessionRegistered;
                NotifyWindowsFontTableChanged();
            }

            font = TryCreateUnityFont(
                candidates,
                registeredForUnityLookup,
                out resolvedFamily);

            bool unityResolved = font != null;

            if (font == null)
            {
                Debug.LogWarning(
                    "[AlbumFonts] Could not resolve " +
                    (packaged ? "packaged" : "custom") +
                    " font '" + displayName + "' through Unity. Tried: " +
                    string.Join(", ", candidates.ToArray()) +
                    ". Unity UI font unavailable; Win32 font rasterization will be used for cover typography.");
                font = AlbumUiResources.GetGameFont();
                resolvedFamily = preferredFamily;
            }
            else
            {
                Debug.Log(
                    "[AlbumFonts] Loaded " +
                    (packaged ? "packaged" : "custom") +
                    " font '" + displayName + "' as Unity family '" +
                    resolvedFamily + "'.");
            }

            options.Add(new AlbumFontOption
            {
                Key = key,
                DisplayName = displayName,
                FamilyName = string.IsNullOrEmpty(resolvedFamily)
                    ? preferredFamily
                    : resolvedFamily,
                FamilyCandidates = candidates.ToArray(),
                Font = font,
                UnityResolved = unityResolved,
                FilePath = path,
                RasterizerIdentity = key + "|" + NormalizeRasterizerPath(path),
                Packaged = packaged
            });
        }

        private static void AddFallbackOption(
            string key,
            string displayName,
            string familyName,
            bool packaged)
        {
            options.Add(new AlbumFontOption
            {
                Key = key,
                DisplayName = displayName,
                FamilyName = familyName,
                FamilyCandidates = new[] { familyName },
                Font = AlbumUiResources.GetGameFont(),
                UnityResolved = false,
                FilePath = string.Empty,
                RasterizerIdentity = key,
                Packaged = packaged
            });
        }

        private static Font TryCreateUnityFont(
            List<string> candidates,
            bool registeredForUnityLookup,
            out string resolvedFamily)
        {
            resolvedFamily = string.Empty;
            if (candidates == null || candidates.Count == 0)
                return null;

            string[] installed = null;
            try
            {
                installed = Font.GetOSInstalledFontNames();
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumFonts] Could not enumerate Unity-visible OS fonts: " +
                    ex.Message);
            }

            // Fast path when Unity's installed-family snapshot already sees the registration.
            if (installed != null && installed.Length > 0)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    string candidate = candidates[i];
                    if (string.IsNullOrWhiteSpace(candidate))
                        continue;

                    string visibleName = installed.FirstOrDefault(
                        name => string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase));
                    if (string.IsNullOrEmpty(visibleName))
                        continue;

                    Font verified = TryCreateUnityFontByName(visibleName);
                    if (verified != null)
                    {
                        bool hasIdentityMetadata;
                        if (UnityFontMatchesCandidates(
                                verified,
                                candidates,
                                out hasIdentityMetadata) ||
                            !hasIdentityMetadata)
                        {
                            resolvedFamily = visibleName;
                            return verified;
                        }
                    }
                }
            }

            // Unity 2019 may keep GetOSInstalledFontNames() cached even after AddFontResourceEx +
            // WM_FONTCHANGE while direct dynamic-font lookup can already see the newly registered
            // face. Do not reject that valid path just because the enumeration is stale. Verify
            // the returned Font against its own fontNames/name metadata before accepting it.
            for (int i = 0; i < candidates.Count; i++)
            {
                string candidate = candidates[i];
                Font direct = TryCreateUnityFontByName(candidate);
                if (direct == null)
                    continue;

                bool hasIdentityMetadata;
                if (UnityFontMatchesCandidates(
                        direct,
                        candidates,
                        out hasIdentityMetadata))
                {
                    resolvedFamily = candidate;
                    return direct;
                }

                // Some Unity/Mono builds expose an empty fontNames array for a freshly created
                // dynamic font. If Windows positively accepted this exact TTF registration and
                // Unity returned a Font for one of its embedded names, accept only the metadata-
                // unavailable case. A concrete mismatching identity is still rejected.
                if (registeredForUnityLookup && !hasIdentityMetadata)
                {
                    Debug.LogWarning(
                        "[AlbumFonts] Unity created registered family '" + candidate +
                        "' but exposed no font identity metadata; accepting the direct lookup.");
                    resolvedFamily = candidate;
                    return direct;
                }
            }

            return null;
        }

        private static bool UnityFontMatchesCandidates(
            Font font,
            List<string> candidates,
            out bool hasIdentityMetadata)
        {
            hasIdentityMetadata = false;
            if (font == null || candidates == null)
                return false;

            try
            {
                string[] names = font.fontNames;
                if (names != null && names.Length > 0)
                {
                    hasIdentityMetadata = true;
                    for (int i = 0; i < names.Length; i++)
                    {
                        string actual = names[i];
                        if (string.IsNullOrWhiteSpace(actual))
                            continue;

                        for (int j = 0; j < candidates.Count; j++)
                        {
                            if (FontIdentityEquals(actual, candidates[j]))
                                return true;
                        }
                    }
                }
            }
            catch
            {
                // Fall through to Font.name.
            }

            try
            {
                string objectName = font.name;
                if (!string.IsNullOrWhiteSpace(objectName))
                {
                    // Generic Unity object names are not useful identity evidence.
                    bool generic = string.Equals(
                        objectName,
                        "DynamicFont",
                        StringComparison.OrdinalIgnoreCase);
                    if (!generic)
                    {
                        hasIdentityMetadata = true;
                        for (int i = 0; i < candidates.Count; i++)
                        {
                            if (FontIdentityEquals(objectName, candidates[i]))
                                return true;
                        }
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool FontIdentityEquals(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return false;

            if (string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;

            return string.Equals(
                NormalizeFontIdentity(left),
                NormalizeFontIdentity(right),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeFontIdentity(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            char[] buffer = new char[value.Length];
            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (char.IsLetterOrDigit(ch))
                    buffer[count++] = char.ToLowerInvariant(ch);
            }
            return new string(buffer, 0, count);
        }

        private static Font TryCreateUnityFontByName(string familyName)
        {
            if (string.IsNullOrWhiteSpace(familyName))
                return null;

            try
            {
                return Font.CreateDynamicFontFromOSFont(familyName, 32);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumFonts] Unity rejected font family '" +
                    familyName + "': " + ex.Message);
                return null;
            }
        }

        private static bool RegisterFontResource(string path, uint flags)
        {
            if (!IsWindows())
                return false;

            if (registeredResources.Any(
                    r => r.Flags == flags &&
                         string.Equals(r.Path, path, StringComparison.OrdinalIgnoreCase)))
                return true;

            try
            {
                int added = AddFontResourceEx(path, flags, IntPtr.Zero);
                if (added <= 0)
                {
                    Debug.LogWarning(
                        "[AlbumFonts] Windows rejected font registration for " +
                        Path.GetFileName(path) + " (flags=0x" +
                        flags.ToString("X") + ", Win32=" +
                        Marshal.GetLastWin32Error() + ").");
                    return false;
                }

                registeredResources.Add(new RegisteredFontResource
                {
                    Path = path,
                    Flags = flags
                });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumFonts] Could not register " +
                    Path.GetFileName(path) + ": " + ex.Message);
                return false;
            }
        }

        private static void NotifyWindowsFontTableChanged()
        {
            if (!IsWindows())
                return;

            try
            {
                SendNotifyMessage(
                    HwndBroadcast,
                    WmFontChange,
                    IntPtr.Zero,
                    IntPtr.Zero);
            }
            catch
            {
                // Font creation has its own fallback and diagnostics.
            }
        }

        private static bool IsWindows()
        {
            return Application.platform == RuntimePlatform.WindowsPlayer ||
                   Application.platform == RuntimePlatform.WindowsEditor;
        }

        /// <summary>
        /// Reads useful naming identities from a TrueType/OpenType name table. Variable fonts in
        /// particular can expose a preferred family (name ID 16) that differs from the legacy
        /// family (ID 1), while Unity may accept either the full name or PostScript name.
        /// </summary>
        private static List<string> TryReadTtfNames(string path)
        {
            List<string> result = new List<string>();

            try
            {
                byte[] data = File.ReadAllBytes(path);
                if (data.Length < 12)
                    return result;

                int tableCount = ReadUInt16BE(data, 4);
                int nameOffset = -1;
                for (int i = 0; i < tableCount; i++)
                {
                    int record = 12 + i * 16;
                    if (record + 16 > data.Length)
                        break;

                    if (data[record] == (byte)'n' &&
                        data[record + 1] == (byte)'a' &&
                        data[record + 2] == (byte)'m' &&
                        data[record + 3] == (byte)'e')
                    {
                        nameOffset = (int)ReadUInt32BE(data, record + 8);
                        break;
                    }
                }

                if (nameOffset < 0 || nameOffset + 6 > data.Length)
                    return result;

                int count = ReadUInt16BE(data, nameOffset + 2);
                int stringOffset = nameOffset + ReadUInt16BE(data, nameOffset + 4);

                Dictionary<int, string> bestById = new Dictionary<int, string>();
                Dictionary<int, int> scoreById = new Dictionary<int, int>();

                for (int i = 0; i < count; i++)
                {
                    int record = nameOffset + 6 + i * 12;
                    if (record + 12 > data.Length)
                        break;

                    int platform = ReadUInt16BE(data, record);
                    int nameId = ReadUInt16BE(data, record + 6);
                    if (nameId != 1 && nameId != 4 && nameId != 6 && nameId != 16)
                        continue;

                    int length = ReadUInt16BE(data, record + 8);
                    int offset = stringOffset + ReadUInt16BE(data, record + 10);
                    if (length <= 0 || offset < 0 || offset + length > data.Length)
                        continue;

                    string value = platform == 0 || platform == 3
                        ? System.Text.Encoding.BigEndianUnicode.GetString(data, offset, length)
                        : System.Text.Encoding.ASCII.GetString(data, offset, length);

                    value = value.Trim('\0', ' ', '\t', '\r', '\n');
                    if (string.IsNullOrWhiteSpace(value))
                        continue;

                    int score = platform == 3 ? 3 : (platform == 0 ? 2 : 1);
                    int oldScore;
                    if (!scoreById.TryGetValue(nameId, out oldScore) || score > oldScore)
                    {
                        scoreById[nameId] = score;
                        bestById[nameId] = value;
                    }
                }

                // Preferred family first, then legacy family, full name and PostScript name.
                AppendName(bestById, 16, result);
                AppendName(bestById, 1, result);
                AppendName(bestById, 4, result);
                AppendName(bestById, 6, result);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumFonts] Could not read font name table for " +
                    Path.GetFileName(path) + ": " + ex.Message);
            }

            return result;
        }

        private static void AppendName(
            Dictionary<int, string> values,
            int id,
            List<string> target)
        {
            string value;
            if (!values.TryGetValue(id, out value) || string.IsNullOrWhiteSpace(value))
                return;
            if (!target.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
                target.Add(value);
        }

        private static void InsertCandidate(List<string> candidates, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            int existing = candidates.FindIndex(
                x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
                candidates.RemoveAt(existing);
            candidates.Insert(0, value);
        }

        private static int ReadUInt16BE(byte[] data, int offset)
        {
            return (data[offset] << 8) | data[offset + 1];
        }

        private static uint ReadUInt32BE(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24) |
                   ((uint)data[offset + 1] << 16) |
                   ((uint)data[offset + 2] << 8) |
                   data[offset + 3];
        }

        private static string NormalizeRasterizerPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                return Path.GetFullPath(path).ToLowerInvariant();
            }
            catch
            {
                return path.ToLowerInvariant();
            }
        }

        private static string SanitizeKey(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "font";

            char[] chars = value.ToLowerInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]))
                    chars[i] = '-';
            }

            string result = new string(chars);
            while (result.Contains("--"))
                result = result.Replace("--", "-");
            return result.Trim('-');
        }
    }
}
