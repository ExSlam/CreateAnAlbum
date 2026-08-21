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
    /// Shared font source for both the live cover designer and persisted cover renderer.
    /// Packaged/custom TTF files are registered as private Windows fonts, so Unity can create
    /// dynamic Font objects from them without installing them system-wide.
    /// </summary>
    internal static class AlbumFontCatalog
    {
        private const uint FrPrivate = 0x10;
        private static readonly List<AlbumFontOption> options = new List<AlbumFontOption>();
        private static readonly List<string> registeredFiles = new List<string>();
        private static bool loaded;

        private sealed class PackagedFont
        {
            internal string Key;
            internal string DisplayName;
            internal string FileName;
            internal string FamilyName;
        }

        // Keep the first four slots aligned with the legacy v2.7 enhancement and the
        // v4 Elegant/Classic/Bold/Script semantic slots so old FontIndex values migrate
        // predictably: Cormorant, Cinzel, Cyberthrone, Allura.
        private static readonly PackagedFont[] PackagedFonts =
        {
            new PackagedFont { Key = "packaged.cormorant-garamond", DisplayName = "Cormorant Garamond", FileName = "CormorantGaramond-VariableFont_wght.ttf", FamilyName = "Cormorant Garamond Light" },
            new PackagedFont { Key = "packaged.cinzel", DisplayName = "Cinzel", FileName = "Cinzel-VariableFont_wght.ttf", FamilyName = "Cinzel" },
            new PackagedFont { Key = "packaged.cyberthrone", DisplayName = "Cyberthrone", FileName = "Cyberthrone.ttf", FamilyName = "Cyberthrone" },
            new PackagedFont { Key = "packaged.allura", DisplayName = "Allura", FileName = "Allura-Regular.ttf", FamilyName = "Allura" }
        };

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int AddFontResourceEx(string fileName, uint flags, IntPtr reserved);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool RemoveFontResourceEx(string fileName, uint flags, IntPtr reserved);

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
                RegisterPrivateFont(path);
                AddOption(packaged.Key, packaged.DisplayName, packaged.FamilyName, true);
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
                int match = options.FindIndex(o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
                if (match >= 0)
                    return match;

                // A keyed 4.1 save must not silently drift onto another custom font when a
                // .ttf is removed or the custom-font list changes. Numeric indexes are only
                // trustworthy for legacy saves that predate stable FontKey persistence.
                return 0;
            }
            return options.Count == 0 ? 0 : Mathf.Clamp(legacyIndex, 0, options.Count - 1);
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
            if (Application.platform == RuntimePlatform.WindowsPlayer ||
                Application.platform == RuntimePlatform.WindowsEditor)
            {
                for (int i = registeredFiles.Count - 1; i >= 0; i--)
                {
                    try { RemoveFontResourceEx(registeredFiles[i], FrPrivate, IntPtr.Zero); }
                    catch { }
                }
            }
            registeredFiles.Clear();
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
                Debug.LogWarning("[AlbumFonts] Could not prepare CustomFonts directory: " + ex.Message);
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
                Debug.LogWarning("[AlbumFonts] Could not enumerate custom fonts: " + ex.Message);
                return;
            }

            foreach (string file in files)
            {
                if (!RegisterPrivateFont(file))
                    continue;

                string family = TryReadTtfFamilyName(file);
                if (string.IsNullOrWhiteSpace(family))
                    family = Path.GetFileNameWithoutExtension(file).Replace('-', ' ').Replace('_', ' ').Trim();
                if (string.IsNullOrWhiteSpace(family))
                    continue;

                // Include the file stem in the key so two custom font files with the same
                // family label do not collide in persisted cover settings.
                string key = "custom." + SanitizeKey(Path.GetFileNameWithoutExtension(file));
                AddOption(key, family + " (Custom)", family, false);
            }
        }

        private static bool RegisterPrivateFont(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            if (Application.platform != RuntimePlatform.WindowsPlayer &&
                Application.platform != RuntimePlatform.WindowsEditor)
                return true;

            try
            {
                int added = AddFontResourceEx(path, FrPrivate, IntPtr.Zero);
                if (added > 0 && !registeredFiles.Contains(path))
                    registeredFiles.Add(path);
                return added > 0;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AlbumFonts] Could not register " + Path.GetFileName(path) + ": " + ex.Message);
                return false;
            }
        }

        private static string TryReadTtfFamilyName(string path)
        {
            try
            {
                byte[] data = File.ReadAllBytes(path);
                if (data.Length < 12)
                    return string.Empty;

                int tableCount = ReadUInt16BE(data, 4);
                int nameOffset = -1;
                for (int i = 0; i < tableCount; i++)
                {
                    int record = 12 + i * 16;
                    if (record + 16 > data.Length)
                        break;
                    if (data[record] == (byte)'n' && data[record + 1] == (byte)'a' &&
                        data[record + 2] == (byte)'m' && data[record + 3] == (byte)'e')
                    {
                        nameOffset = (int)ReadUInt32BE(data, record + 8);
                        break;
                    }
                }
                if (nameOffset < 0 || nameOffset + 6 > data.Length)
                    return string.Empty;

                int count = ReadUInt16BE(data, nameOffset + 2);
                int stringOffset = nameOffset + ReadUInt16BE(data, nameOffset + 4);
                string best = string.Empty;
                for (int i = 0; i < count; i++)
                {
                    int record = nameOffset + 6 + i * 12;
                    if (record + 12 > data.Length)
                        break;
                    int platform = ReadUInt16BE(data, record);
                    int nameId = ReadUInt16BE(data, record + 6);
                    if (nameId != 1)
                        continue;
                    int length = ReadUInt16BE(data, record + 8);
                    int offset = stringOffset + ReadUInt16BE(data, record + 10);
                    if (length <= 0 || offset < 0 || offset + length > data.Length)
                        continue;

                    string value = platform == 0 || platform == 3
                        ? System.Text.Encoding.BigEndianUnicode.GetString(data, offset, length)
                        : System.Text.Encoding.ASCII.GetString(data, offset, length);
                    value = value.Trim('\0', ' ', '\t', '\r', '\n');
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        best = value;
                        if (platform == 3)
                            break;
                    }
                }
                return best;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AlbumFonts] Could not read TTF family name: " + ex.Message);
                return string.Empty;
            }
        }

        private static int ReadUInt16BE(byte[] data, int offset)
        {
            return (data[offset] << 8) | data[offset + 1];
        }

        private static uint ReadUInt32BE(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) |
                   ((uint)data[offset + 2] << 8) | data[offset + 3];
        }

        private static void AddOption(string key, string displayName, string familyName, bool packaged)
        {
            if (options.Any(o => string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase)))
                return;

            Font font = null;
            try
            {
                font = Font.CreateDynamicFontFromOSFont(familyName, 32);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AlbumFonts] Could not create Unity font '" + familyName + "': " + ex.Message);
            }

            if (font == null)
                font = AlbumUiResources.GetGameFont();

            options.Add(new AlbumFontOption
            {
                Key = key,
                DisplayName = displayName,
                FamilyName = familyName,
                Font = font,
                Packaged = packaged
            });
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
