using System;
using System.IO;
using UnityEngine;

namespace Albummodelite
{
    internal static class AlbumPaths
    {
        internal static readonly string ModDirectory = ResolveModDirectory();

        internal static readonly string BackgroundsDirectory = Path.Combine(
            ModDirectory,
            "AlbumBackgrounds");

        internal static readonly string FontsDirectory = Path.Combine(
            ModDirectory,
            "AlbumFonts");

        internal static readonly string FontBundlePath = Path.Combine(
            FontsDirectory,
            "createanalbum_fonts");

        // Kept only to detect and warn about legacy loose-font installations. CAA 4.2.2 does
        // not create or load this directory because raw TTF/OTF import is not supported by the
        // game's Unity runtime.
        internal static readonly string CustomFontsDirectory = Path.Combine(
            ModDirectory,
            "CustomFonts");

        private static string ResolveModDirectory()
        {
            try
            {
                string assemblyPath = typeof(AlbumPaths).Assembly.Location;
                if (!string.IsNullOrEmpty(assemblyPath))
                {
                    string directory = Path.GetDirectoryName(assemblyPath);
                    if (!string.IsNullOrEmpty(directory))
                        return directory;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CreateAlbum] Could not resolve the mod directory: " + ex.Message);
            }

            return Application.dataPath;
        }
    }
}
