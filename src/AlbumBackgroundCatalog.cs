using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Albummodelite
{
    internal sealed class AlbumBackgroundOption
    {
        internal string Key;
        internal string DisplayName;
        internal string RelativePath;
        internal Sprite Sprite;
    }

    /// <summary>
    /// Shared, directory-driven album background catalog. Background files are discovered from
    /// AlbumBackgrounds at runtime, keyed by normalized relative path, and reused by both the
    /// cover designer and persisted cover renderer. New files therefore do not require code changes.
    /// </summary>
    internal static class AlbumBackgroundCatalog
    {
        private const long RescanIntervalTicks = TimeSpan.TicksPerSecond * 2L;

        // v4.0/v4.1 used raw Directory.GetFiles indexes. Preserve the packaged slots explicitly
        // so old saves can migrate to stable keys even after users add more backgrounds later.
        private static readonly string[] LegacyPackagedFiles =
        {
            "Dreamy.png",
            "Geometric.png",
            "Winter.png",
            "aurora.png",
            "desert.png",
            "eclipse.png",
            "floral art-deco.png",
            "floral.png",
            "neon.png"
        };

        private sealed class CachedSprite
        {
            internal long Length;
            internal long LastWriteUtcTicks;
            internal Sprite Sprite;
        }

        private sealed class Candidate
        {
            internal string FullPath;
            internal string RelativePath;
            internal string Key;
            internal string DisplayName;
            internal long Length;
            internal long LastWriteUtcTicks;
            internal int LegacyRank;
        }

        private static readonly List<AlbumBackgroundOption> options =
            new List<AlbumBackgroundOption>();
        private static readonly List<string> legacyRuntimeKeys = new List<string>();
        private static bool legacyRuntimeOrderCaptured;
        private static readonly Dictionary<string, CachedSprite> cache =
            new Dictionary<string, CachedSprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<Sprite> retiredSprites = new List<Sprite>();

        private static bool loaded;
        private static string contentSignature = string.Empty;
        private static long nextRescanUtcTicks;

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
            Refresh(false);
        }

        /// <summary>
        /// Rescans immediately when force is true. Otherwise scans at most once every two seconds,
        /// allowing newly added files to appear during a running game without doing disk I/O on
        /// every chart-cover materialization.
        /// </summary>
        internal static bool Refresh(bool force = true)
        {
            long now = DateTime.UtcNow.Ticks;
            if (!force && loaded && now < nextRescanUtcTicks)
                return false;
            nextRescanUtcTicks = now + RescanIntervalTicks;

            List<Candidate> candidates = EnumerateCandidates();
            string signature = BuildSignature(candidates);
            if (loaded && string.Equals(signature, contentSignature, StringComparison.Ordinal))
                return false;

            List<AlbumBackgroundOption> rebuilt = new List<AlbumBackgroundOption>();
            foreach (Candidate candidate in candidates)
            {
                Sprite sprite = LoadSprite(candidate);
                if (sprite == null)
                    continue;

                rebuilt.Add(new AlbumBackgroundOption
                {
                    Key = candidate.Key,
                    DisplayName = candidate.DisplayName,
                    RelativePath = candidate.RelativePath,
                    Sprite = sprite
                });
            }

            options.Clear();
            options.AddRange(rebuilt);
            contentSignature = signature;
            loaded = true;

            Debug.Log("[AlbumBackgrounds] Loaded " + options.Count + " background image(s).");
            return true;
        }

        internal static Sprite[] GetSprites()
        {
            EnsureLoaded();
            return options.Select(o => o.Sprite).ToArray();
        }

        internal static string GetKey(int index)
        {
            EnsureLoaded();
            if (options.Count == 0)
                return string.Empty;
            index = Mathf.Clamp(index, 0, options.Count - 1);
            return options[index].Key;
        }

        internal static string GetLegacyKey(int legacyIndex)
        {
            EnsureLoaded();

            // First reproduce the exact top-level PNG enumeration that 4.1.0 used on this
            // installation. This is the strongest available migration signal because the old
            // loader never defined a portable sort order.
            if (legacyIndex >= 0 && legacyIndex < legacyRuntimeKeys.Count)
                return legacyRuntimeKeys[legacyIndex];

            if (legacyIndex >= 0 && legacyIndex < LegacyPackagedFiles.Length)
                return BuildKey(LegacyPackagedFiles[legacyIndex]);

            // Extra user images were technically reachable through Randomize Cover in older
            // builds. If the original raw order cannot be reconstructed, fall back to the
            // currently resolved slot rather than discarding the background entirely.
            if (legacyIndex >= 0 && legacyIndex < options.Count)
                return options[legacyIndex].Key;
            return string.Empty;
        }

        internal static int GetIndex(string key, int legacyIndex)
        {
            EnsureLoaded();
            if (options.Count == 0)
                return 0;

            if (!string.IsNullOrEmpty(key))
            {
                int keyed = options.FindIndex(o =>
                    string.Equals(o.Key, key, StringComparison.OrdinalIgnoreCase));
                if (keyed >= 0)
                    return keyed;

                // A stable-key save must not silently drift to a different image when its file
                // is temporarily missing. Use the first available background as a visual fallback;
                // keep the persisted key untouched so restoring the file restores the intended art.
                return 0;
            }

            string legacyKey = GetLegacyKey(legacyIndex);
            if (!string.IsNullOrEmpty(legacyKey))
            {
                int legacy = options.FindIndex(o =>
                    string.Equals(o.Key, legacyKey, StringComparison.OrdinalIgnoreCase));
                if (legacy >= 0)
                    return legacy;
            }

            return Mathf.Clamp(legacyIndex, 0, options.Count - 1);
        }

        internal static Sprite Resolve(string key, int legacyIndex)
        {
            EnsureLoaded();
            if (options.Count == 0)
                return null;
            return options[GetIndex(key, legacyIndex)].Sprite;
        }

        internal static string GetDisplayName(string key, int legacyIndex)
        {
            EnsureLoaded();
            if (options.Count == 0)
                return "None";
            return options[GetIndex(key, legacyIndex)].DisplayName;
        }

        internal static void Shutdown()
        {
            HashSet<Sprite> destroyed = new HashSet<Sprite>();
            foreach (CachedSprite cached in cache.Values)
            {
                if (cached != null && cached.Sprite != null && destroyed.Add(cached.Sprite))
                    DestroySprite(cached.Sprite);
            }
            foreach (Sprite sprite in retiredSprites)
            {
                if (sprite != null && destroyed.Add(sprite))
                    DestroySprite(sprite);
            }

            options.Clear();
            legacyRuntimeKeys.Clear();
            legacyRuntimeOrderCaptured = false;
            cache.Clear();
            retiredSprites.Clear();
            contentSignature = string.Empty;
            nextRescanUtcTicks = 0L;
            loaded = false;
        }

        private static List<Candidate> EnumerateCandidates()
        {
            string folder = AlbumPaths.BackgroundsDirectory;
            try
            {
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AlbumBackgrounds] Could not prepare AlbumBackgrounds directory: " + ex.Message);
                return new List<Candidate>();
            }

            CaptureLegacyRuntimeOrder(folder);

            string[] files;
            try
            {
                files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories)
                    .Where(IsSupportedImage)
                    .ToArray();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AlbumBackgrounds] Could not enumerate background images: " + ex.Message);
                return new List<Candidate>();
            }

            List<Candidate> result = new List<Candidate>();
            foreach (string fullPath in files)
            {
                try
                {
                    FileInfo info = new FileInfo(fullPath);
                    string relative = GetRelativePath(folder, fullPath);
                    result.Add(new Candidate
                    {
                        FullPath = fullPath,
                        RelativePath = relative,
                        Key = BuildKey(relative),
                        DisplayName = BuildDisplayName(relative),
                        Length = info.Length,
                        LastWriteUtcTicks = info.LastWriteTimeUtc.Ticks,
                        LegacyRank = GetLegacyRank(relative)
                    });
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[AlbumBackgrounds] Could not inspect " + Path.GetFileName(fullPath) + ": " + ex.Message);
                }
            }

            return result
                .OrderBy(c => c.LegacyRank)
                .ThenBy(c => c.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static Sprite LoadSprite(Candidate candidate)
        {
            CachedSprite cached;
            if (cache.TryGetValue(candidate.FullPath, out cached) &&
                cached != null && cached.Sprite != null &&
                cached.Length == candidate.Length &&
                cached.LastWriteUtcTicks == candidate.LastWriteUtcTicks)
            {
                return cached.Sprite;
            }

            try
            {
                Texture2D texture = new Texture2D(2, 2);
                texture.name = "AlbumBackgroundTexture_" + Path.GetFileNameWithoutExtension(candidate.FullPath);
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;

                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(candidate.FullPath)))
                {
                    UnityEngine.Object.Destroy(texture);
                    Debug.LogWarning("[AlbumBackgrounds] Unity could not decode " + candidate.RelativePath + ".");
                    return null;
                }

                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f));
                sprite.name = "AlbumBackground_" + Path.GetFileNameWithoutExtension(candidate.FullPath);

                if (cached != null && cached.Sprite != null)
                    retiredSprites.Add(cached.Sprite);

                cache[candidate.FullPath] = new CachedSprite
                {
                    Length = candidate.Length,
                    LastWriteUtcTicks = candidate.LastWriteUtcTicks,
                    Sprite = sprite
                };
                return sprite;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AlbumBackgrounds] Could not load " + candidate.RelativePath + ": " + ex.Message);
                return null;
            }
        }

        private static bool IsSupportedImage(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase);
        }

        private static int GetLegacyRank(string relativePath)
        {
            string key = BuildKey(relativePath);
            int runtimeRank = legacyRuntimeKeys.FindIndex(existing =>
                string.Equals(existing, key, StringComparison.OrdinalIgnoreCase));
            if (runtimeRank >= 0)
                return runtimeRank;

            string fileName = Path.GetFileName(relativePath);
            for (int i = 0; i < LegacyPackagedFiles.Length; i++)
            {
                if (string.Equals(fileName, LegacyPackagedFiles[i], StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(relativePath, fileName, StringComparison.OrdinalIgnoreCase))
                    return legacyRuntimeKeys.Count + i;
            }
            return legacyRuntimeKeys.Count + LegacyPackagedFiles.Length;
        }

        private static void CaptureLegacyRuntimeOrder(string folder)
        {
            if (legacyRuntimeOrderCaptured)
                return;
            legacyRuntimeOrderCaptured = true;
            legacyRuntimeKeys.Clear();

            try
            {
                // Deliberately mirror 4.1.0 exactly: top-level *.png with no explicit sorting.
                // Capture once before the new catalog imposes deterministic ordering.
                string[] legacyFiles = Directory.GetFiles(folder, "*.png");
                foreach (string file in legacyFiles)
                    legacyRuntimeKeys.Add(BuildKey(GetRelativePath(folder, file)));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AlbumBackgrounds] Could not capture legacy background order: " + ex.Message);
            }
        }

        private static string BuildSignature(IEnumerable<Candidate> candidates)
        {
            return string.Join("|", candidates.Select(c =>
                c.Key + ":" + c.Length + ":" + c.LastWriteUtcTicks));
        }

        private static string BuildKey(string relativePath)
        {
            return "file:" + NormalizeRelativePath(relativePath).ToLowerInvariant();
        }

        private static string BuildDisplayName(string relativePath)
        {
            string normalized = NormalizeRelativePath(relativePath);
            string directory = Path.GetDirectoryName(normalized) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(normalized)
                .Replace('_', ' ')
                .Replace('-', ' ')
                .Trim();
            if (string.IsNullOrEmpty(name))
                name = "Background";

            if (!string.IsNullOrEmpty(directory))
            {
                directory = directory.Replace('\\', '/').Trim('/');
                return directory + " / " + name;
            }
            return name;
        }

        private static string GetRelativePath(string folder, string fullPath)
        {
            string root = Path.GetFullPath(folder)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(fullPath);
            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return NormalizeRelativePath(path.Substring(root.Length));
            return NormalizeRelativePath(Path.GetFileName(path));
        }

        private static string NormalizeRelativePath(string path)
        {
            return (path ?? string.Empty)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/')
                .TrimStart('/');
        }

        private static void DestroySprite(Sprite sprite)
        {
            if (sprite == null)
                return;
            Texture texture = sprite.texture;
            UnityEngine.Object.Destroy(sprite);
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }
    }
}
