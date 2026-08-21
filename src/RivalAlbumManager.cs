using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Albummodelite
{
    public static class RivalAlbumManager
    {
        private static int nextRivalId = -1;

        private static readonly string[] titleLeft =
        {
            "Midnight", "Electric", "Velvet", "Crystal", "Neon", "Blue", "Golden", "Silent",
            "Dream", "Broken", "Summer", "Lunar", "Secret", "Wild", "After"
        };

        private static readonly string[] titleRight =
        {
            "Bloom", "Signal", "Memories", "Hearts", "Parade", "Echo", "Dream", "Story",
            "Light", "Fever", "Promise", "Mirage", "City", "Hour", "Love"
        };

        internal static void RebuildIdAllocator()
        {
            int lowest = Albums.AlbumList
                .Where(a => a != null && !a.PlayerAlbum && a.ID < 0)
                .Select(a => a.ID)
                .DefaultIfEmpty(0)
                .Min();
            nextRivalId = lowest <= -1 ? lowest - 1 : -1;
        }

        public static void EnsureInitialRivals(int minimumActive = 14)
        {
            if (!RivalsRebornIntegration.IsReady)
                return;

            List<RivalLabelView> eligible = GetEligibleLabels();
            if (eligible.Count == 0)
                return;

            int activeRivals = Albums.AlbumList.Count(a =>
                a != null && !a.PlayerAlbum && a.Released && a.WeeklySales > 0L);

            int needed = Mathf.Max(0, minimumActive - activeRivals);
            eligible = eligible.OrderBy(x => UnityEngine.Random.value).ToList();

            int safety = 0;
            while (needed > 0 && safety < minimumActive * 3)
            {
                safety++;
                RivalLabelView label = eligible[UnityEngine.Random.Range(0, eligible.Count)];
                AlbumData album = CreateRivalAlbum(label, false);
                if (album == null)
                    continue;

                int ageWeeks = UnityEngine.Random.Range(1, 8);
                long weekly = CalculateRivalSalesForLabel(label, ageWeeks);
                album.WeeksOnChart = ageWeeks;
                album.WeeklySales = weekly;
                album.Sales = weekly;

                double previous = weekly;
                for (int week = 1; week < ageWeeks; week++)
                {
                    previous *= UnityEngine.Random.Range(1.10f, 1.38f);
                    album.Sales += (long)previous;
                }

                Albums.AddAlbum(album);
                needed--;
            }

            AlbumSalesManager.RebuildChartPositions();
        }

        public static void ReleaseWeeklyRivals()
        {
            if (!RivalsRebornIntegration.IsReady)
                return;

            List<RivalLabelView> eligible = GetEligibleLabels()
                .Where(label => !Albums.AlbumList.Any(a =>
                    a != null && !a.PlayerAlbum && a.RivalGroupId == label.GroupId && a.Released &&
                    (staticVars.dateTime - a.ReleaseDate).TotalDays < 35d))
                .ToList();
            if (eligible.Count == 0)
                return;

            // Apply the same-label cooldown before choosing the cycle's 1-3 releases.
            // Selecting first and filtering afterward could produce zero releases even when
            // other labels were eligible and outside the cooldown.
            int releaseCount = Mathf.Min(UnityEngine.Random.Range(1, 4), eligible.Count);
            List<RivalLabelView> selected = eligible
                .OrderBy(x => UnityEngine.Random.value)
                .Take(releaseCount)
                .ToList();

            foreach (RivalLabelView label in selected)
            {
                AlbumData album = CreateRivalAlbum(label, true);
                if (album == null)
                    continue;

                Albums.AddAlbum(album);
                AlbumSalesManager.RegisterNewAlbum(album);
                RivalsRebornIntegration.TryQueueNews(
                    album.GroupName + " released a new album, \"" + album.Title + "\"."
                );
            }

            EnsureInitialRivals(14);
        }

        internal static List<RivalLabelView> GetEligibleLabels()
        {
            return RivalsRebornIntegration.GetLabels().Where(CanReleaseAlbum).ToList();
        }

        internal static bool CanReleaseAlbum(RivalLabelView label)
        {
            if (label == null || label.Disbanded || label.Roster == null || label.Roster.Count == 0)
                return false;

            // The live RR label is authoritative. A vanilla Rivals._group is optional enrichment;
            // RR can legitimately have an active label without a corresponding vanilla group.
            return label.Roster.Any(RivalsRebornIntegration.IsActiveIdol);
        }

        private static AlbumData CreateRivalAlbum(RivalLabelView label, bool brandNew)
        {
            if (!CanReleaseAlbum(label))
                return null;

            Rivals._group group = RivalsRebornIntegration.TryGetVanillaGroup(label.GroupId);
            AlbumData album = new AlbumData();
            album.ID = nextRivalId--;
            album.RivalGroupId = label.GroupId;
            album.Title = titleLeft[UnityEngine.Random.Range(0, titleLeft.Length)] + " " +
                          titleRight[UnityEngine.Random.Range(0, titleRight.Length)];

            string groupName = "";
            try
            {
                if (group != null)
                    groupName = group.GetGroupName();
            }
            catch
            {
            }

            album.GroupName = !string.IsNullOrEmpty(groupName)
                ? groupName
                : (!string.IsNullOrEmpty(label.Name) ? label.Name : "Unknown Group");
            album.RivalName = album.GroupName;
            album.PlayerAlbum = false;
            album.Released = true;
            album.ReleaseDate = brandNew
                ? staticVars.dateTime
                : staticVars.dateTime.AddDays(-7 * UnityEngine.Random.Range(1, 8));
            album.Members = BuildAlbumMembers(label);

            album.ThemeIndex = UnityEngine.Random.Range(0, 5);
            album.Theme = GetThemeName(album.ThemeIndex);
            int backgroundCount = AlbumBackgroundCatalog.Count;
            album.BackgroundIndex = backgroundCount > 0
                ? UnityEngine.Random.Range(0, backgroundCount)
                : 0;
            album.BackgroundKey = backgroundCount > 0
                ? AlbumBackgroundCatalog.GetKey(album.BackgroundIndex)
                : "";
            album.LayoutIndex = UnityEngine.Random.Range(0, 5);
            album.FontIndex = UnityEngine.Random.Range(0, Mathf.Max(1, AlbumFontCatalog.Count));
            album.FontKey = AlbumFontCatalog.GetKey(album.FontIndex);
            album.ReleaseKind = (int)CreateAnAlbumGroupRules.AlbumReleaseKind.LP;
            album.TextColorIndex = UnityEngine.Random.Range(0, 7);
            album.TitlePosition = UnityEngine.Random.Range(0, 3);
            album.ShowGroupName = true;
            album.OrnamentStyle = UnityEngine.Random.Range(0, 3);
            album.FrameStyle = UnityEngine.Random.Range(0, 5);
            album.TitleEffect = UnityEngine.Random.Range(0, 4);
            album.PortraitScale = UnityEngine.Random.Range(0.90f, 1.10f);
            album.CenterEmphasis = UnityEngine.Random.Range(1.04f, 1.13f);
            album.PortraitYOffset = UnityEngine.Random.Range(-18f, 14f);
            album.PortraitSpacing = UnityEngine.Random.Range(0.88f, 1.12f);
            album.EffectsIntensity = UnityEngine.Random.Range(0.80f, 1.20f);
            album.CenterMemberIndex = FindCenterIndex(label);
            album.Songs = new List<singles._single>();
            return album;
        }

        private static List<data_girls.girls> BuildAlbumMembers(RivalLabelView label)
        {
            List<RivalIdolView> active = label.Roster
                .Where(RivalsRebornIntegration.IsActiveIdol)
                .OrderByDescending(idol => idol.IsCenter)
                .ThenByDescending(idol => idol.Fame)
                .ThenByDescending(idol => idol.StatTotal)
                .Take(8)
                .ToList();

            List<data_girls.girls> members = new List<data_girls.girls>();
            foreach (RivalIdolView idol in active)
            {
                data_girls.girls displayGirl = RivalsRebornIntegration.TryGetDisplayGirl(idol);
                if (displayGirl != null)
                    members.Add(displayGirl);
            }
            return members;
        }

        private static int FindCenterIndex(RivalLabelView label)
        {
            List<RivalIdolView> active = label.Roster
                .Where(RivalsRebornIntegration.IsActiveIdol)
                .OrderByDescending(idol => idol.IsCenter)
                .ThenByDescending(idol => idol.Fame)
                .ThenByDescending(idol => idol.StatTotal)
                .Take(8)
                .ToList();

            for (int i = 0; i < active.Count; i++)
            {
                if (active[i].IsCenter)
                    return i;
            }
            return active.Count > 0 ? 0 : -1;
        }

        public static long CalculateRivalFirstWeekSales(AlbumData album)
        {
            if (album == null)
                return 0L;

            RivalLabelView label = RivalsRebornIntegration.FindLabel(album.RivalGroupId);
            return label == null
                ? UnityEngine.Random.Range(50000, 160000)
                : CalculateRivalSalesForLabel(label, 1);
        }

        private static long CalculateRivalSalesForLabel(RivalLabelView label, int chartWeek)
        {
            Rivals._group group = RivalsRebornIntegration.TryGetVanillaGroup(label.GroupId);
            long fans = group != null ? Math.Max(1000L, group.Fans) : 10000L;
            List<RivalIdolView> active = label.Roster.Where(RivalsRebornIntegration.IsActiveIdol).ToList();
            double avgFame = active.Count > 0 ? active.Average(idol => (double)idol.Fame) : 0d;
            double avgStats = active.Count > 0 ? active.Average(idol => idol.StatTotal / 7.0) : 30d;

            double fanConversion = UnityEngine.Random.Range(0.16f, 0.34f);
            double momentum = Mathf.Clamp(label.Momentum, 0.5f, 1.6f);
            double wealthBoost = 0.85d + (Mathf.Clamp(label.Wealth, 0, 100) / 100d) * 0.35d;
            double fameBoost = 1d + Math.Min(0.30d, avgFame * 0.045d);
            double qualityBoost = 0.88d + Math.Min(0.28d, avgStats / 300d);
            double sales = fans * fanConversion * momentum * wealthBoost * fameBoost * qualityBoost;

            if (staticVars.IsStoryMode() && staticVars.PlayerData.Chapter < tasks._chapter.post_game)
                sales = Math.Min(sales, 180000d);
            if (chartWeek > 1)
                sales *= Math.Pow(0.73d, chartWeek - 1);

            return Math.Max(1000L, (long)Math.Round(sales));
        }

        private static string GetThemeName(int index)
        {
            switch (index)
            {
                case 1: return "Dark";
                case 2: return "Neon";
                case 3: return "Vintage";
                case 4: return "Minimal";
                default: return "Dreamy";
            }
        }
    }
}
