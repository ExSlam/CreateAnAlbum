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
        internal Font Font;
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

            LoadCustomFonts();

            if (options.Count == 0)
            {
                Font fallback = AlbumUiResources.GetGameFont();
                options.Add(new AlbumFontOption
                {
                    Key = "game.default",
                    DisplayName = "Game Font",
                    FamilyName = fallback != null ? fallback.name : "Arial",
                    Font = fallback,
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
                // .ttf is removed or the custom-font list changes. Numeric indexes are only
                // trustworthy for legacy saves that predate stable FontKey persistence.
                return 0;
            }

            return options.Count == 0
                ? 0
                : Mathf.Clamp(legacyIndex, 0, options.Count - 1);
        }

        internal static Font Resolve(string key, int legacyIndex)
        {
            EnsureLoaded();
            if (options.Count == 0)
                return AlbumUiResources.GetGameFont();

            int index = GetIndex(key, legacyIndex);
            return options[index].Font ?? AlbumUiResources.GetGameFont();
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
            options.Clear();
            loaded = false;
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
                files = Directory.GetFiles(directory, "*.ttf");
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
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

                string key = "custom." + SanitizeKey(Path.GetFileNameWithoutExtension(file));
                AddFileOption(key, display + " (Custom)", file, display, false);
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

            if (IsWindows())
            {
                // Register the file before the first Unity font query. Unity 2019 can cache its
                // native font-family view, and CreateDynamicFontFromOSFont may return a fallback
                // Font object even when the requested family is unavailable. Register privately
                // and session-visible up front, then verify the family through Unity's own OS
                // font enumeration before accepting the dynamic font. Both registrations are
                // temporary and are removed during Shutdown.
                RegisterFontResource(path, FrPrivate);
                RegisterFontResource(path, FrSession);
                NotifyWindowsFontTableChanged();
            }

            font = TryCreateUnityFont(candidates, out resolvedFamily);

            if (font == null)
            {
                Debug.LogWarning(
                    "[AlbumFonts] Could not resolve " +
                    (packaged ? "packaged" : "custom") +
                    " font '" + displayName + "' through Unity. Tried: " +
                    string.Join(", ", candidates.ToArray()) +
                    ". Falling back to the game font.");
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
                Font = font,
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
                Font = AlbumUiResources.GetGameFont(),
                Packaged = packaged
            });
        }

        private static Font TryCreateUnityFont(
            List<string> candidates,
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

            // When Unity exposes its installed-family list, require an exact family-name match.
            // This prevents CreateDynamicFontFromOSFont from being mistaken for success when it
            // quietly substitutes a fallback face for a missing family.
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
                        resolvedFamily = visibleName;
                        return verified;
                    }
                }

                return null;
            }

            // Fail-open compatibility for platforms/builds where Unity cannot enumerate OS
            // families. Diagnostics make this path visible; Windows normally uses the verified
            // branch above after AddFontResourceEx + WM_FONTCHANGE.
            Debug.LogWarning(
                "[AlbumFonts] Unity returned no OS font-family enumeration; " +
                "falling back to direct family lookup.");
            for (int i = 0; i < candidates.Count; i++)
            {
                Font direct = TryCreateUnityFontByName(candidates[i]);
                if (direct != null)
                {
                    resolvedFamily = candidates[i];
                    return direct;
                }
            }

            return null;
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
        /// Reads useful naming identities from the TTF/OpenType name table. Variable fonts in
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
                    "[AlbumFonts] Could not read TTF name table for " +
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
