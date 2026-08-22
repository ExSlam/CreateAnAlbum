using System;
using System.IO;
using UnityEngine;

namespace Albummodelite
{
    /// <summary>
    /// Stable save-slot addressing. Vanilla saves live below persistentDataPath/data;
    /// CAA mirrors the same relative path below persistentDataPath/CreateAnAlbum.
    /// No OS/user-specific absolute path is persisted as slot identity.
    /// </summary>
    internal static class AlbumSaveScope
    {
        private const string VanillaRootName = "data";
        private const string SupplementalRootName = "CreateAnAlbum";

        internal static string GetVanillaRoot()
        {
            return NormalizeDirectory(Path.Combine(Application.persistentDataPath, VanillaRootName));
        }

        internal static string GetSupplementalRoot()
        {
            string root = NormalizeDirectory(Path.Combine(Application.persistentDataPath, SupplementalRootName));
            if (!string.IsNullOrEmpty(root) && !Directory.Exists(root))
                Directory.CreateDirectory(root);
            return root;
        }

        internal static bool TryResolveWriteTarget(
            string dataFileName,
            bool fullPath,
            out string physicalPath,
            out string relativeSlot)
        {
            physicalPath = string.Empty;
            relativeSlot = string.Empty;
            if (string.IsNullOrWhiteSpace(dataFileName))
                return false;

            try
            {
                string candidate = fullPath
                    ? dataFileName
                    : Path.Combine(GetVanillaRoot(), dataFileName + ".json");
                return TryResolvePhysicalTarget(candidate, out physicalPath, out relativeSlot);
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryResolveLoadTarget(
            string dataFileName,
            out string physicalPath,
            out string relativeSlot)
        {
            physicalPath = string.Empty;
            relativeSlot = string.Empty;
            if (string.IsNullOrWhiteSpace(dataFileName))
                return false;

            try
            {
                // Match DataSaver.loadData<T> exactly: combine with data, append .json,
                // then collapse the literal duplicate extension token.
                string candidate = Path.Combine(GetVanillaRoot(), dataFileName + ".json")
                    .Replace(".json.json", ".json");
                return TryResolvePhysicalTarget(candidate, out physicalPath, out relativeSlot);
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryResolvePhysicalTarget(
            string path,
            out string physicalPath,
            out string relativeSlot)
        {
            physicalPath = string.Empty;
            relativeSlot = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
                return false;

            try
            {
                string root = GetVanillaRoot();
                string candidate = Path.GetFullPath(
                    Path.IsPathRooted(path) ? path : Path.Combine(root, path));
                string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;

                if (!candidate.StartsWith(prefix, comparison))
                    return false;

                string relative = candidate.Substring(prefix.Length)
                    .Replace('\\', '/')
                    .TrimStart('/');
                if (string.IsNullOrEmpty(relative) || relative.Contains("../") || relative == "..")
                    return false;

                // Game-controlled save path names are effectively case-insensitive on the
                // supported Windows build. Lower-casing also makes copied saves portable.
                relativeSlot = relative.ToLowerInvariant();
                physicalPath = candidate;
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static string GetSidecarPath(string relativeSlot)
        {
            if (string.IsNullOrWhiteSpace(relativeSlot))
                return string.Empty;

            string root = GetSupplementalRoot();
            string relative = relativeSlot.Replace('/', Path.DirectorySeparatorChar);
            string path = Path.GetFullPath(Path.Combine(root, relative));
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!path.StartsWith(prefix, comparison))
                return string.Empty;

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            return path;
        }

        internal static string GetSidecarPathForPhysicalVanillaPath(string vanillaPath)
        {
            string physical;
            string relative;
            return TryResolvePhysicalTarget(vanillaPath, out physical, out relative)
                ? GetSidecarPath(relative)
                : string.Empty;
        }

        internal static bool IsSameLogicalSlot(string first, string second)
        {
            string a = CanonicalizeCheckpointPath(first);
            string b = CanonicalizeCheckpointPath(second);
            return !string.IsNullOrEmpty(a) &&
                   string.Equals(a, b, StringComparison.Ordinal);
        }

        internal static string CanonicalizeCheckpointPath(string storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath))
                return string.Empty;

            string normalized = storedPath.Replace('\\', '/').Trim().ToLowerInvariant();

            // Migration for v4.1.4 checkpoints that persisted an absolute OS path. Do the
            // textual /data/ extraction before asking the host OS whether the path is rooted,
            // because a Windows C:/ path may be inspected on a Unix host after a save transfer.
            int marker = normalized.LastIndexOf("/data/", StringComparison.Ordinal);
            if (marker >= 0 && marker + 6 < normalized.Length)
                return normalized.Substring(marker + 6);
            if (normalized.StartsWith("data/", StringComparison.Ordinal))
                return normalized.Substring(5);

            bool looksAbsolute = Path.IsPathRooted(storedPath) ||
                                 normalized.StartsWith("/", StringComparison.Ordinal) ||
                                 (normalized.Length > 2 && normalized[1] == ':' && normalized[2] == '/');
            if (!looksAbsolute)
                return normalized.TrimStart('/');

            string physical;
            string relative;
            if (TryResolvePhysicalTarget(storedPath, out physical, out relative))
                return relative;
            return string.Empty;
        }

        private static string NormalizeDirectory(string path)
        {
            try { return Path.GetFullPath(path); }
            catch { return path ?? string.Empty; }
        }
    }
}
