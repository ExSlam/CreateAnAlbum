using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Albummodelite;

namespace CreateAnAlbumChartTrackEnhancements
{
    internal static class DiscographyRivalFilter
    {
        internal static List<AlbumData> HideRivals()
        {
            if (Albums.AlbumList == null)
                return null;

            List<AlbumData> rivals =
                Albums.AlbumList
                    .Where(a =>
                        a != null &&
                        !a.PlayerAlbum)
                    .ToList();

            if (rivals.Count == 0)
                return rivals;

            Albums.AlbumList.RemoveAll(
                a =>
                    a != null &&
                    !a.PlayerAlbum
            );

            return rivals;
        }

        internal static void RestoreRivals(
            List<AlbumData> rivals)
        {
            if (rivals == null ||
                Albums.AlbumList == null)
            {
                return;
            }

            foreach (AlbumData album in rivals)
            {
                if (album != null &&
                    !Albums.AlbumList.Contains(album))
                {
                    Albums.AlbumList.Add(album);
                }
            }
        }
    }

    [HarmonyPatch(typeof(AlbumLibraryPopup), "DrawHeader")]
    internal static class DiscographyHeaderFilterPatch
    {
        private static void Prefix(
            ref List<AlbumData> __state)
        {
            __state =
                DiscographyRivalFilter.HideRivals();
        }

        private static void Postfix(
            List<AlbumData> __state)
        {
            DiscographyRivalFilter
                .RestoreRivals(__state);
        }
    }

    [HarmonyPatch(typeof(AlbumLibraryPopup), "DrawLibrary")]
    internal static class DiscographyLibraryFilterPatch
    {
        private static void Prefix(
            ref List<AlbumData> __state)
        {
            __state =
                DiscographyRivalFilter.HideRivals();
        }

        private static void Postfix(
            List<AlbumData> __state)
        {
            DiscographyRivalFilter
                .RestoreRivals(__state);
        }
    }
}
