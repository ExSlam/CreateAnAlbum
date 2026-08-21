using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Albummodelite;

namespace CreateAnAlbumChartTrackEnhancements
{
    internal static class AlbumTrackRepair
    {
        private static readonly Dictionary<string, List<int>>
            SavedSongIds =
                new Dictionary<string, List<int>>();

        internal static string GetAlbumKey(AlbumData album)
        {
            if (album == null)
                return "";

            if (album.ID != 0)
            {
                return
                    (album.PlayerAlbum ? "P:" : "R:") +
                    album.ID;
            }

            return
                (album.PlayerAlbum ? "P:" : "R:") +
                (album.GroupName ?? "") + "|" +
                (album.Title ?? "") + "|" +
                album.ReleaseDate.Ticks;
        }

        internal static void Remember(
            AlbumData album,
            IEnumerable<int> ids)
        {
            if (album == null || ids == null)
                return;

            List<int> clean = ids
                .Distinct()
                .ToList();

            if (clean.Count == 0)
                return;

            SavedSongIds[
                GetAlbumKey(album)
            ] = clean;
        }

        internal static void RememberLiveSongs(
            AlbumData album)
        {
            if (album == null ||
                album.Songs == null ||
                album.Songs.Count == 0)
            {
                return;
            }

            Remember(
                album,
                album.Songs
                    .Where(s => s != null)
                    .Select(s => s.id)
            );
        }

        internal static int Repair(
            AlbumData album)
        {
            if (album == null)
                return 0;

            // A valid live list is better than pending IDs.
            if (album.Songs != null &&
                album.Songs.Count > 0)
            {
                RememberLiveSongs(album);
                return album.Songs.Count;
            }

            List<int> ids;

            if (!SavedSongIds.TryGetValue(
                    GetAlbumKey(album),
                    out ids) ||
                ids == null ||
                ids.Count == 0)
            {
                return 0;
            }

            List<singles._single> restored =
                new List<singles._single>();

            foreach (int id in ids)
            {
                try
                {
                    singles._single song =
                        singles.GetSingleByID(id);

                    if (song != null &&
                        !restored.Contains(song))
                    {
                        restored.Add(song);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        "[AlbumTrackRepair] Could not restore single " +
                        id + ": " + ex.Message
                    );
                }
            }

            if (restored.Count > 0)
            {
                album.Songs = restored;

                Debug.Log(
                    "[AlbumTrackRepair] Restored " +
                    restored.Count +
                    " track(s) for " +
                    album.Title
                );
            }

            return restored.Count;
        }

        internal static void RepairAll()
        {
            if (Albums.AlbumList == null)
                return;

            foreach (AlbumData album in Albums.AlbumList)
            {
                if (album != null &&
                    album.PlayerAlbum)
                {
                    Repair(album);
                }
            }
        }

        internal static List<int> GetRememberedIds(
            AlbumData album)
        {
            if (album == null)
                return null;

            RememberLiveSongs(album);

            List<int> ids;

            if (SavedSongIds.TryGetValue(
                    GetAlbumKey(album),
                    out ids))
            {
                return new List<int>(ids);
            }

            return null;
        }
    }

    // Capture private AlbumSaveEntry.SongIds while the working base DLL
    // rebuilds AlbumData. This works even when the game's singles list is
    // not ready yet.
    [HarmonyPatch]
    internal static class AlbumPersistenceFromSaveTrackPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(AlbumPersistence),
                "FromSaveEntry"
            );
        }

        private static void Postfix(
            object __0,
            AlbumData __result)
        {
            if (__0 == null ||
                __result == null)
            {
                return;
            }

            try
            {
                FieldInfo songIdsField =
                    AccessTools.Field(
                        __0.GetType(),
                        "SongIds"
                    );

                if (songIdsField == null)
                    return;

                List<int> ids =
                    songIdsField.GetValue(__0)
                        as List<int>;

                AlbumTrackRepair.Remember(
                    __result,
                    ids
                );

                AlbumTrackRepair.Repair(
                    __result
                );
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumTrackRepair] FromSaveEntry capture failed: " +
                    ex.Message
                );
            }
        }
    }

    // Preserve remembered SongIds if AlbumData.Songs is temporarily empty
    // when the base DLL serializes the album again.
    [HarmonyPatch]
    internal static class AlbumPersistenceToSaveTrackPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(AlbumPersistence),
                "ToSaveEntry"
            );
        }

        private static void Postfix(
            AlbumData __0,
            object __result)
        {
            if (__0 == null ||
                __result == null)
            {
                return;
            }

            try
            {
                List<int> ids =
                    AlbumTrackRepair
                        .GetRememberedIds(__0);

                if (ids == null ||
                    ids.Count == 0)
                {
                    return;
                }

                FieldInfo songIdsField =
                    AccessTools.Field(
                        __result.GetType(),
                        "SongIds"
                    );

                if (songIdsField == null)
                    return;

                List<int> current =
                    songIdsField.GetValue(__result)
                        as List<int>;

                if (current == null ||
                    current.Count == 0)
                {
                    songIdsField.SetValue(
                        __result,
                        new List<int>(ids)
                    );

                    Debug.Log(
                        "[AlbumTrackRepair] Preserved " +
                        ids.Count +
                        " saved track ID(s) for " +
                        __0.Title
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumTrackRepair] ToSaveEntry preservation failed: " +
                    ex.Message
                );
            }
        }
    }

    // Repair just before the existing detail popup reads album.Songs.Count
    // and renders the TRACK LIST.
    [HarmonyPatch(typeof(AlbumDetailPopup), "Open")]
    internal static class AlbumDetailTrackRepairPatch
    {
        private static void Prefix(
            AlbumData album)
        {
            AlbumTrackRepair.Repair(album);
        }
    }

    internal static class AlbumPersistenceBridge
    {
        internal static bool EnsurePlayerAlbumsLoaded(string reason)
        {
            try
            {
                // 4.1.0: never force a second disk reload just because a campaign has no
                // released player albums. That old workaround could discard dirty production
                // state before the next vanilla save. EnsureCurrentSaveLoaded is idempotent and
                // only loads when the active save/checkpoint identity actually changed.
                if (!AlbumPersistence.EnsureCurrentSaveLoaded())
                {
                    Debug.LogWarning(
                        "[AlbumPersistenceBridge] Album state is not ready during " + reason + "."
                    );
                    return false;
                }

                return Albums.AlbumList != null &&
                    Albums.AlbumList.Any(a =>
                        a != null && a.PlayerAlbum && a.Released);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumPersistenceBridge] Save-aware load check failed: " + ex.Message
                );
                return false;
            }
        }
    }

    // Try again whenever Discography opens. Rival albums can already be
    // present in AlbumList, so checking AlbumList.Count is not enough:
    // specifically require a PLAYER album before deciding restoration
    // has already happened.
    [HarmonyPatch(typeof(AlbumLibraryPopup), "Open")]
    internal static class AlbumLibraryTrackRepairPatch
    {
        private static void Prefix()
        {
            AlbumPersistenceBridge
                .EnsurePlayerAlbumsLoaded(
                    "Discography Open"
                );

            AlbumTrackRepair.RepairAll();
        }
    }
}
