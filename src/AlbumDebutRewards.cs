using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Albummodelite
{
    internal static class AlbumDebutRewards
    {
        internal static long TryAward(AlbumData album)
        {
            if (album == null || !album.PlayerAlbum || !album.Released ||
                album.DebutFanRewardGranted)
                return 0L;

            int songCount = album.Songs != null ? album.Songs.Count : 0;
            double multiplier = songCount <= 6
                ? 0.80d
                : (songCount <= 10 ? 1.00d : 1.20d);

            long requested = Math.Max(
                100L,
                (long)Math.Round(album.WeeklySales * 0.02d * multiplier)
            );

            List<data_girls.girls> activeMembers = album.Members == null
                ? new List<data_girls.girls>()
                : album.Members
                    .Where(member => member != null &&
                        member.status != data_girls._status.graduated)
                    .Distinct()
                    .ToList();

            if (activeMembers.Count == 0)
            {
                // Mark it consumed even when nobody is eligible. Otherwise a historical
                // release could unexpectedly pay out later if its member list is repaired.
                album.DebutFanRewardGranted = true;
                AlbumPersistence.MarkDirty();
                return 0L;
            }

            try
            {
                data_girls.AddFans_Equally(requested, activeMembers);
                album.DebutFanRewardGranted = true;
                AlbumPersistence.MarkDirty();
                Debug.Log(
                    "[AlbumDebutRewards] Awarded " + requested.ToString("N0") +
                    " debut fans for " + (album.Title ?? "album") + "."
                );
                return requested;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumDebutRewards] Could not award debut fans: " + ex.Message
                );
                return 0L;
            }
        }
    }
}
