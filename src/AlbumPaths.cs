using System;
using System.IO;
using UnityEngine;

namespace Albummodelite
{
    internal static class AlbumPaths
    {
        internal static readonly string BackgroundsDirectory = Path.Combine(
            ResolveModDirectory(),
            "AlbumBackgrounds");

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
