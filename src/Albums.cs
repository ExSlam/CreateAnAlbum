using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Albummodelite
{
    public class Albums : MonoBehaviour
    {
        public static List<AlbumData> AlbumList =
            new List<AlbumData>();

        public static string GetIdentityKey(AlbumData album)
        {
            if (album == null)
                return "";

            if (album.ID != 0)
            {
                return
                    (album.PlayerAlbum ? "P:" : "R:") +
                    album.ID;
            }

            string owner = album.PlayerAlbum
                ? (album.GroupName ?? "")
                : (!string.IsNullOrEmpty(album.RivalName)
                    ? album.RivalName
                    : (album.GroupName ?? ""));

            return
                (album.PlayerAlbum ? "P:" : "R:") +
                owner + "|" +
                (album.Title ?? "") + "|" +
                album.ReleaseDate.Ticks;
        }

        public static void AddAlbum(AlbumData album)
        {
            if (album == null)
                return;

            DeduplicateInPlace();

            string key = GetIdentityKey(album);

            int existingIndex = AlbumList.FindIndex(
                a => a != null && GetIdentityKey(a) == key
            );

            if (existingIndex >= 0)
            {
                AlbumData existing = AlbumList[existingIndex];

                if (!ReferenceEquals(existing, album) &&
                    GetCompletenessScore(album) >
                    GetCompletenessScore(existing))
                {
                    AlbumList[existingIndex] = album;
                }

                Debug.LogWarning(
                    "[CreateAlbum] Duplicate album ignored: " +
                    album.Title +
                    " | key=" + key
                );

                AlbumPersistence.MarkDirty();
                return;
            }

            AlbumList.Add(album);

            Debug.Log(
                "[CreateAlbum] Added " +
                album.Title +
                " | key=" + key
            );

            AlbumPersistence.MarkDirty();
        }

        public static void DeduplicateInPlace()
        {
            if (AlbumList == null || AlbumList.Count <= 1)
                return;

            Dictionary<string, AlbumData> unique =
                new Dictionary<string, AlbumData>();

            List<string> order =
                new List<string>();

            foreach (AlbumData album in AlbumList)
            {
                if (album == null)
                    continue;

                string key = GetIdentityKey(album);

                if (!unique.ContainsKey(key))
                {
                    unique[key] = album;
                    order.Add(key);
                    continue;
                }

                if (GetCompletenessScore(album) >
                    GetCompletenessScore(unique[key]))
                {
                    unique[key] = album;
                }
            }

            if (unique.Count == AlbumList.Count)
                return;

            int removed = AlbumList.Count - unique.Count;

            AlbumList.Clear();

            foreach (string key in order)
            {
                AlbumData album;

                if (unique.TryGetValue(key, out album) &&
                    album != null)
                {
                    AlbumList.Add(album);
                }
            }

            Debug.LogWarning(
                "[CreateAlbum] Removed " +
                removed +
                " duplicate album entr" +
                (removed == 1 ? "y." : "ies.")
            );
        }

        public static List<AlbumData> GetUniqueAlbumsSnapshot()
        {
            DeduplicateInPlace();

            return AlbumList
                .Where(a => a != null)
                .ToList();
        }

        private static long GetCompletenessScore(AlbumData album)
        {
            if (album == null)
                return long.MinValue;

            long score = album.Sales;

            score +=
                (long)Math.Max(0, album.WeeksOnChart) *
                1000000000L;

            score +=
                (long)(album.Songs != null ? album.Songs.Count : 0) *
                1000000L;

            score +=
                (long)(album.Members != null ? album.Members.Count : 0) *
                10000L;

            return score;
        }

        public static void RemoveAlbum(AlbumData album)
        {
            if (album == null)
                return;

            AlbumList.Remove(album);
            DeduplicateInPlace();
            AlbumPersistence.MarkDirty();
        }

        public static void SaveNow()
        {
            DeduplicateInPlace();
            AlbumPersistence.MarkDirty();
        }

        public static void TestAlbum()
        {
            AlbumData a = new AlbumData();
            a.ID = 999999;
            a.Title = "ECLIPSE";
            a.Released = true;
            a.Sales = 532000;
            a.WeeklySales = 532000;

            AddAlbum(a);
        }
    }
}
