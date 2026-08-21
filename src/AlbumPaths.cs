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
