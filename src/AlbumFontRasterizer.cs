using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Albummodelite
{
    /// <summary>
    /// Windows GDI-backed font renderer used when Unity 2019 cannot expose a supplied/private
    /// or already-installed system font through Font.CreateDynamicFontFromOSFont. This
    /// implementation intentionally uses only Win32 P/Invoke plus Unity types, so Create An
    /// Album does not acquire a load-time
    /// System.Drawing dependency that Idol Manager's Mono runtime may not provide.
    /// </summary>
    internal static class AlbumFontRasterizer
    {
        private const uint BiRgb = 0;
        private const uint DibRgbColors = 0;
        private const int Transparent = 1;
        private const int FwNormal = 400;
        private const uint DefaultCharset = 1;
        private const uint OutDefaultPrecision = 0;
        private const uint ClipDefaultPrecision = 0;
        private const uint AntialiasedQuality = 4;
        private const uint DefaultPitch = 0;
        private const uint DtLeft = 0x00000000;
        private const uint DtCenter = 0x00000001;
        private const uint DtRight = 0x00000002;
        private const uint DtVCenter = 0x00000004;
        private const uint DtSingleLine = 0x00000020;
        private const uint DtNoPrefix = 0x00000800;
        private const uint DtEndEllipsis = 0x00008000;

        private const int MaxCachedSprites = 128;

        private sealed class CachedSpriteEntry
        {
            internal string CacheKey;
            internal int SpriteId;
            internal Sprite Sprite;
            internal int LeaseCount;
            internal long LastUseSequence;
        }

        private static readonly Dictionary<string, CachedSpriteEntry> spriteCache =
            new Dictionary<string, CachedSpriteEntry>(StringComparer.Ordinal);
        private static readonly Dictionary<int, CachedSpriteEntry> entriesBySpriteId =
            new Dictionary<int, CachedSpriteEntry>();
        private static long useSequence;

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfoHeader
        {
            internal uint biSize;
            internal int biWidth;
            internal int biHeight;
            internal ushort biPlanes;
            internal ushort biBitCount;
            internal uint biCompression;
            internal uint biSizeImage;
            internal int biXPelsPerMeter;
            internal int biYPelsPerMeter;
            internal uint biClrUsed;
            internal uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RgbQuad
        {
            internal byte rgbBlue;
            internal byte rgbGreen;
            internal byte rgbRed;
            internal byte rgbReserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfo
        {
            internal BitmapInfoHeader bmiHeader;
            internal RgbQuad bmiColors;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WinRect
        {
            internal int left;
            internal int top;
            internal int right;
            internal int bottom;
        }

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateDIBSection(
            IntPtr hdc,
            ref BitmapInfo bitmapInfo,
            uint usage,
            out IntPtr bits,
            IntPtr section,
            uint offset);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr obj);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFontW(
            int height,
            int width,
            int escapement,
            int orientation,
            int weight,
            uint italic,
            uint underline,
            uint strikeOut,
            uint charSet,
            uint outputPrecision,
            uint clipPrecision,
            uint quality,
            uint pitchAndFamily,
            string faceName);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetTextFaceW(IntPtr hdc, int count, StringBuilder faceName);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int SetBkMode(IntPtr hdc, int mode);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern uint SetTextColor(IntPtr hdc, uint colorRef);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int DrawTextW(
            IntPtr hdc,
            string text,
            int textLength,
            ref WinRect rect,
            uint format);

        internal static Sprite Render(
            string cacheIdentity,
            string[] familyCandidates,
            string text,
            int width,
            int height,
            int pixelSize,
            UnityEngine.Color color,
            TextAnchor alignment)
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer &&
                Application.platform != RuntimePlatform.WindowsEditor)
                return null;

            if (string.IsNullOrEmpty(text) ||
                familyCandidates == null || familyCandidates.Length == 0 ||
                width <= 0 || height <= 0)
                return null;

            if (string.IsNullOrWhiteSpace(cacheIdentity))
                cacheIdentity = string.Join("|", familyCandidates);

            string cacheKey = cacheIdentity.ToLowerInvariant() + "|" + text + "|" +
                width + "|" + height + "|" + pixelSize + "|" +
                ColorKey(color) + "|" + (int)alignment;
            TrimCache();

            CachedSpriteEntry cached;
            if (spriteCache.TryGetValue(cacheKey, out cached) &&
                cached != null && cached.Sprite != null)
            {
                cached.LastUseSequence = ++useSequence;
                return cached.Sprite;
            }

            if (cached != null)
                RemoveEntry(cached, false);

            IntPtr dc = IntPtr.Zero;
            IntPtr bitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;
            IntPtr font = IntPtr.Zero;
            IntPtr oldFont = IntPtr.Zero;
            Texture2D managedTexture = null;
            Sprite managedSprite = null;

            try
            {
                dc = CreateCompatibleDC(IntPtr.Zero);
                if (dc == IntPtr.Zero)
                    return null;

                BitmapInfo bitmapInfo = new BitmapInfo
                {
                    bmiHeader = new BitmapInfoHeader
                    {
                        biSize = (uint)Marshal.SizeOf(typeof(BitmapInfoHeader)),
                        biWidth = width,
                        // A normal bottom-up DIB stores its first row as the image bottom,
                        // which matches Unity's raw RGBA texture memory orientation.
                        biHeight = height,
                        biPlanes = 1,
                        biBitCount = 32,
                        biCompression = BiRgb,
                        biSizeImage = (uint)(width * height * 4)
                    }
                };

                IntPtr bits;
                bitmap = CreateDIBSection(
                    dc,
                    ref bitmapInfo,
                    DibRgbColors,
                    out bits,
                    IntPtr.Zero,
                    0);
                if (bitmap == IntPtr.Zero || bits == IntPtr.Zero)
                    return null;

                oldBitmap = SelectObject(dc, bitmap);

                int byteCount = checked(width * height * 4);
                byte[] clear = new byte[byteCount];
                Marshal.Copy(clear, 0, bits, clear.Length);

                string selectedFamily;
                font = CreateVerifiedFont(dc, familyCandidates, pixelSize, out selectedFamily);
                if (font == IntPtr.Zero)
                {
                    Debug.LogWarning(
                        "[AlbumFonts] Win32 could not select the requested font face. Tried: " +
                        string.Join(", ", familyCandidates) + ". Using the game-font fallback.");
                    return null;
                }

                oldFont = SelectObject(dc, font);
                SetBkMode(dc, Transparent);
                SetTextColor(dc, ColorRef(255, 255, 255));

                WinRect rect = new WinRect
                {
                    left = 0,
                    top = 0,
                    right = width,
                    bottom = height
                };

                uint format = DtSingleLine | DtVCenter | DtNoPrefix | DtEndEllipsis;
                if (alignment == TextAnchor.MiddleLeft)
                    format |= DtLeft;
                else if (alignment == TextAnchor.MiddleRight)
                    format |= DtRight;
                else
                    format |= DtCenter;

                if (DrawTextW(dc, text, text.Length, ref rect, format) == 0)
                    return null;

                byte[] bgra = new byte[byteCount];
                Marshal.Copy(bits, bgra, 0, bgra.Length);
                Color32[] pixels = ConvertCoverageToPixels(bgra, color);

                managedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                managedTexture.name = "CreateAnAlbum_GdiFont_" + selectedFamily;
                managedTexture.wrapMode = TextureWrapMode.Clamp;
                managedTexture.filterMode = FilterMode.Bilinear;
                managedTexture.SetPixels32(pixels);
                managedTexture.Apply(false, false);

                managedSprite = Sprite.Create(
                    managedTexture,
                    new UnityEngine.Rect(0f, 0f, managedTexture.width, managedTexture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);

                CachedSpriteEntry created = new CachedSpriteEntry
                {
                    CacheKey = cacheKey,
                    SpriteId = managedSprite.GetInstanceID(),
                    Sprite = managedSprite,
                    LeaseCount = 0,
                    LastUseSequence = ++useSequence
                };
                spriteCache[cacheKey] = created;
                entriesBySpriteId[created.SpriteId] = created;
                return managedSprite;
            }
            catch (Exception ex)
            {
                if (managedSprite != null)
                {
                    UnityEngine.Object.Destroy(managedSprite);
                    managedSprite = null;
                }
                if (managedTexture != null)
                {
                    UnityEngine.Object.Destroy(managedTexture);
                    managedTexture = null;
                }

                Debug.LogWarning(
                    "[AlbumFonts] Win32 font rasterization failed: " + ex.Message +
                    ". Using the game-font fallback.");
                return null;
            }
            finally
            {
                if (oldFont != IntPtr.Zero && dc != IntPtr.Zero)
                    SelectObject(dc, oldFont);
                if (font != IntPtr.Zero)
                    DeleteObject(font);
                if (oldBitmap != IntPtr.Zero && dc != IntPtr.Zero)
                    SelectObject(dc, oldBitmap);
                if (bitmap != IntPtr.Zero)
                    DeleteObject(bitmap);
                if (dc != IntPtr.Zero)
                    DeleteDC(dc);
            }
        }

        internal static bool CanResolve(string[] familyCandidates)
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer &&
                Application.platform != RuntimePlatform.WindowsEditor)
                return false;

            if (familyCandidates == null || familyCandidates.Length == 0)
                return false;

            IntPtr dc = IntPtr.Zero;
            IntPtr font = IntPtr.Zero;
            try
            {
                dc = CreateCompatibleDC(IntPtr.Zero);
                if (dc == IntPtr.Zero)
                    return false;

                string selectedFamily;
                font = CreateVerifiedFont(
                    dc, familyCandidates, 16, out selectedFamily, false);
                return font != IntPtr.Zero;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (font != IntPtr.Zero)
                    DeleteObject(font);
                if (dc != IntPtr.Zero)
                    DeleteDC(dc);
            }
        }

        private static IntPtr CreateVerifiedFont(
            IntPtr dc,
            string[] candidates,
            int pixelSize,
            out string selectedFamily,
            bool logSelection = true)
        {
            selectedFamily = string.Empty;

            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i];
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                IntPtr font = CreateFontW(
                    -Math.Max(6, pixelSize),
                    0,
                    0,
                    0,
                    FwNormal,
                    0,
                    0,
                    0,
                    DefaultCharset,
                    OutDefaultPrecision,
                    ClipDefaultPrecision,
                    AntialiasedQuality,
                    DefaultPitch,
                    candidate);
                if (font == IntPtr.Zero)
                    continue;

                IntPtr previous = SelectObject(dc, font);
                string actual = GetSelectedFaceName(dc);
                if (previous != IntPtr.Zero)
                    SelectObject(dc, previous);

                if (FontIdentityMatches(candidate, actual))
                {
                    selectedFamily = string.IsNullOrEmpty(actual) ? candidate : actual;
                    if (logSelection)
                    {
                        Debug.Log(
                            "[AlbumFonts] Win32 font renderer selected '" + selectedFamily +
                            "' for requested face '" + candidate + "'.");
                    }
                    return font;
                }

                DeleteObject(font);
            }

            return IntPtr.Zero;
        }

        private static string GetSelectedFaceName(IntPtr dc)
        {
            try
            {
                StringBuilder buffer = new StringBuilder(128);
                int length = GetTextFaceW(dc, buffer.Capacity, buffer);
                return length > 0 ? buffer.ToString() : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool FontIdentityMatches(string requested, string actual)
        {
            if (string.IsNullOrWhiteSpace(requested) || string.IsNullOrWhiteSpace(actual))
                return false;

            string left = NormalizeFontIdentity(requested);
            string right = NormalizeFontIdentity(actual);
            if (left.Length == 0 || right.Length == 0)
                return false;

            if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
                return true;

            // GDI may report a style-qualified face for a requested family (for example,
            // "Cormorant Garamond Light" for "Cormorant Garamond"). Accept that direction
            // only. Never accept a shorter substituted family such as "Arial" when the
            // requested face was "Arial Black".
            return right.StartsWith(left, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeFontIdentity(string value)
        {
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

        private static Color32[] ConvertCoverageToPixels(byte[] bgra, UnityEngine.Color color)
        {
            byte red = ToByte(color.r);
            byte green = ToByte(color.g);
            byte blue = ToByte(color.b);
            byte alpha = ToByte(color.a);
            Color32[] pixels = new Color32[bgra.Length / 4];

            for (int source = 0, pixel = 0; source + 3 < bgra.Length; source += 4, pixel++)
            {
                // ANTIALIASED_QUALITY draws grayscale white glyphs into the black DIB.
                // GDI does not maintain an alpha channel for a 32-bit BI_RGB DIB, so derive
                // coverage from the brightest color component and tint it in managed code.
                int coverage = Math.Max(
                    bgra[source],
                    Math.Max(bgra[source + 1], bgra[source + 2]));
                pixels[pixel] = new Color32(
                    red,
                    green,
                    blue,
                    (byte)((coverage * alpha + 127) / 255));
            }

            return pixels;
        }

        private static uint ColorRef(byte red, byte green, byte blue)
        {
            return (uint)(red | (green << 8) | (blue << 16));
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
        }

        private static string ColorKey(UnityEngine.Color color)
        {
            return ToByte(color.r).ToString("X2") +
                   ToByte(color.g).ToString("X2") +
                   ToByte(color.b).ToString("X2") +
                   ToByte(color.a).ToString("X2");
        }

        internal static void TrackUsage(GameObject owner, Sprite sprite)
        {
            if (owner == null || sprite == null)
                return;

            CachedSpriteEntry entry;
            int spriteId = sprite.GetInstanceID();
            if (!entriesBySpriteId.TryGetValue(spriteId, out entry) || entry == null)
                return;

            entry.LeaseCount++;
            entry.LastUseSequence = ++useSequence;
            if (!AlbumFontSpriteLease.Attach(owner, spriteId))
            {
                entry.LeaseCount--;
                TrimCache();
                return;
            }

            TrimCache();
        }

        internal static void ReleaseUsage(int spriteId)
        {
            CachedSpriteEntry entry;
            if (!entriesBySpriteId.TryGetValue(spriteId, out entry) || entry == null)
                return;

            if (entry.LeaseCount > 0)
                entry.LeaseCount--;
            entry.LastUseSequence = ++useSequence;
            TrimCache();
        }

        private static void TrimCache()
        {
            while (spriteCache.Count > MaxCachedSprites)
            {
                CachedSpriteEntry oldest = null;
                foreach (CachedSpriteEntry candidate in spriteCache.Values)
                {
                    if (candidate == null || candidate.LeaseCount > 0)
                        continue;

                    if (oldest == null ||
                        candidate.LastUseSequence < oldest.LastUseSequence)
                    {
                        oldest = candidate;
                    }
                }

                // More than MaxCachedSprites may be visible simultaneously. Never destroy a
                // sprite that an active Image still uses; trim as those covers are released.
                if (oldest == null)
                    return;

                RemoveEntry(oldest, true);
            }
        }

        private static void RemoveEntry(CachedSpriteEntry entry, bool destroySprite)
        {
            if (entry == null)
                return;

            if (!string.IsNullOrEmpty(entry.CacheKey))
                spriteCache.Remove(entry.CacheKey);
            if (entry.SpriteId != 0)
                entriesBySpriteId.Remove(entry.SpriteId);

            if (destroySprite && entry.Sprite != null)
            {
                Texture texture = entry.Sprite.texture;
                UnityEngine.Object.Destroy(entry.Sprite);
                if (texture != null)
                    UnityEngine.Object.Destroy(texture);
            }

            entry.Sprite = null;
        }

        internal static void Shutdown()
        {
            foreach (CachedSpriteEntry entry in new List<CachedSpriteEntry>(spriteCache.Values))
                RemoveEntry(entry, true);

            spriteCache.Clear();
            entriesBySpriteId.Clear();
            useSequence = 0L;
        }
    }

    internal sealed class AlbumFontSpriteLease : MonoBehaviour
    {
        private int spriteId;

        internal static bool Attach(GameObject owner, int trackedSpriteId)
        {
            if (owner == null || trackedSpriteId == 0)
                return false;

            AlbumFontSpriteLease lease = owner.AddComponent<AlbumFontSpriteLease>();
            if (lease == null)
                return false;

            lease.spriteId = trackedSpriteId;
            return true;
        }

        private void OnDestroy()
        {
            if (spriteId == 0)
                return;

            AlbumFontRasterizer.ReleaseUsage(spriteId);
            spriteId = 0;
        }
    }
}
