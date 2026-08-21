using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Albummodelite
{
    public class AlbumSalesManager : MonoBehaviour
    {
        public static AlbumSalesManager Instance;

        // Album chart processing happens every 14 in-game days.
        private DateTime lastProcessedDate = DateTime.MinValue;
        private string activeSaveId = "";
        private int activeLoadGeneration = -1;

        private void Awake()
        {
            Instance = this;
            lastProcessedDate = DateTime.MinValue;
            activeSaveId = "";
            activeLoadGeneration = -1;

            Debug.Log(
                "[AlbumSales] Manager ready. Waiting for loaded save."
            );
        }

        private void Update()
        {
            if (!AlbumPersistence.IsCurrentSaveLoaded)
                return;

            DateTime currentDate;

            try
            {
                currentDate = staticVars.dateTime.Date;
            }
            catch
            {
                return;
            }

            string currentSave =
                AlbumPersistence.CurrentSaveId;

            int loadGeneration = AlbumPersistence.LoadGeneration;
            if (activeSaveId != currentSave ||
                activeLoadGeneration != loadGeneration)
            {
                activeSaveId = currentSave;
                activeLoadGeneration = loadGeneration;
                lastProcessedDate = AlbumPersistence.LastChartProcessedDate;
                if (lastProcessedDate == DateTime.MinValue ||
                    lastProcessedDate > currentDate)
                {
                    lastProcessedDate = currentDate;
                    AlbumPersistence.SetLastChartProcessedDate(
                        lastProcessedDate
                    );
                }

                RivalAlbumManager.EnsureInitialRivals();

                Albums.DeduplicateInPlace();
                AlbumPersistence.MarkDirty();

                Debug.Log(
                    "[AlbumSales] Initialized 14-day chart for " +
                    activeSaveId +
                    " at " +
                    lastProcessedDate.ToString("yyyy-MM-dd")
                );

                return;
            }

            if (lastProcessedDate == DateTime.MinValue)
            {
                lastProcessedDate = currentDate;
                AlbumPersistence.SetLastChartProcessedDate(
                    lastProcessedDate
                );
                return;
            }

            if (currentDate < lastProcessedDate)
            {
                lastProcessedDate = currentDate;
                AlbumPersistence.SetLastChartProcessedDate(
                    lastProcessedDate
                );
                return;
            }

            int safety = 0;
            while ((currentDate - lastProcessedDate).TotalDays >= 14d &&
                safety < 24)
            {
                DateTime cycleDate = lastProcessedDate.AddDays(14);
                ProcessChartPeriod(cycleDate);
                lastProcessedDate = cycleDate;
                AlbumPersistence.SetLastChartProcessedDate(
                    lastProcessedDate
                );
                safety++;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static void RegisterNewAlbum(AlbumData album)
        {
            if (album == null)
                return;

            long firstWeek = album.PlayerAlbum
                ? CalculateFirstWeekSales(album)
                : RivalAlbumManager.CalculateRivalFirstWeekSales(album);

            album.WeeklySales = firstWeek;
            album.Sales = firstWeek;
            album.WeeksOnChart = 1;

            // Chart position is assigned when rankings are rebuilt.
            album.PreviousChartPosition = 0;
            album.ChartPosition = 0;
            album.PeakChartPosition = 0;

            RebuildChartPositions();

            Debug.Log(
                "[AlbumSales] Debut: " +
                album.Title +
                " | First chart period: " +
                firstWeek.ToString("N0") +
                " | Chart: #" +
                album.ChartPosition
            );

            Albums.DeduplicateInPlace();
            AlbumPersistence.MarkDirty();
        }

        public static void ProcessWeek()
        {
            ProcessChartPeriod(staticVars.dateTime);
        }

        private static void ProcessChartPeriod(DateTime reportDate)
        {
            Dictionary<string, AlbumChartPeriodState> periodState =
                AlbumChartPeriodPerformance.Capture();

            // New AI releases enter before this week's ranking is calculated.
            RivalAlbumManager.ReleaseWeeklyRivals();

            List<AlbumData> activeAlbums = Albums.AlbumList
                .Where(a => a != null && a.Released)
                .ToList();

            foreach (AlbumData album in activeAlbums)
            {
                // A brand-new album already received Week 1 sales when it
                // was registered. Its next weekly tick becomes Week 2.
                if (album.WeeksOnChart <= 0)
                {
                    RegisterNewAlbum(album);
                    continue;
                }

                album.PreviousChartPosition = album.ChartPosition;

                long nextWeek = CalculateFollowingWeekSales(album);

                album.WeeklySales = nextWeek;
                album.Sales += nextWeek;
                album.WeeksOnChart++;

                // Sales eventually fall low enough for the album to leave
                // the active chart, while lifetime sales remain stored.
                if (album.WeeklySales < 250L)
                {
                    album.WeeklySales = 0L;
                    album.ChartPosition = 0;
                }
            }

            RebuildChartPositions();

            List<AlbumChartPeriodResult> periodResults =
                AlbumChartPeriodPerformance.Complete(periodState);

            Albums.DeduplicateInPlace();
            AlbumPersistence.MarkDirty();

            AlbumChartUpdatePopup updatePopup = Instance != null
                ? Instance.GetComponent<AlbumChartUpdatePopup>()
                : UnityEngine.Object.FindObjectOfType<AlbumChartUpdatePopup>();
            if (updatePopup != null)
                updatePopup.Enqueue(periodResults, reportDate);

            Debug.Log(
                "[AlbumSales] 14-day album chart processed. Albums: " +
                activeAlbums.Count
            );
        }

        public static void RebuildChartPositions()
        {
            List<AlbumData> ranked = Albums.AlbumList
                .Where(a =>
                    a != null &&
                    a.Released &&
                    a.WeeklySales > 0L
                )
                .OrderByDescending(a => a.WeeklySales)
                .ThenByDescending(a => a.Sales)
                .Take(20)
                .ToList();

            // Clear positions for albums outside the Top 20.
            foreach (AlbumData album in Albums.AlbumList)
            {
                if (album != null &&
                    !ranked.Contains(album))
                {
                    album.ChartPosition = 0;
                }
            }

            for (int i = 0; i < ranked.Count; i++)
            {
                AlbumData album = ranked[i];

                // Keep the old position when RegisterNewAlbum calls this.
                if (album.ChartPosition > 0 &&
                    album.PreviousChartPosition <= 0 &&
                    album.WeeksOnChart > 1)
                {
                    album.PreviousChartPosition =
                        album.ChartPosition;
                }

                album.ChartPosition = i + 1;

                if (album.PeakChartPosition <= 0 ||
                    album.ChartPosition < album.PeakChartPosition)
                {
                    album.PeakChartPosition =
                        album.ChartPosition;
                }
            }
        }

        public static long CalculateFirstWeekSales(AlbumData album)
        {
            if (album == null)
                return 0L;

            int songCount =
                album.Songs != null
                    ? album.Songs.Count
                    : 0;

            int memberCount =
                album.Members != null
                    ? album.Members.Count
                    : 0;

            // Stable baseline that compiles without depending on private
            // Idol Manager fame/quality fields. We can connect those later.
            double baseSales = 30000d;

            baseSales += songCount * 14000d;
            baseSales += memberCount * 9000d;

            // Albums with more tracks get a small additional launch boost,
            // but the bonus is deliberately capped.
            double trackBonus =
                1d + Math.Min(0.20d, Math.Max(0d, songCount - 6) * 0.04d);

            // Larger lineups give a modest audience reach bonus.
            double memberBonus =
                1d + Math.Min(0.18d, Math.Max(0d, memberCount - 1) * 0.03d);

            double randomness =
                UnityEngine.Random.Range(0.82f, 1.18f);

            long result = (long)Math.Round(
                baseSales *
                trackBonus *
                memberBonus *
                randomness
            );

            return Math.Max(1000L, result);
        }

        private static long CalculateFollowingWeekSales(
            AlbumData album
        )
        {
            if (album == null ||
                album.WeeklySales <= 0L)
            {
                return 0L;
            }

            // Normal weekly decay.
            float retention =
                UnityEngine.Random.Range(0.57f, 0.82f);

            // Early weeks can hold slightly better.
            if (album.WeeksOnChart <= 2)
                retention += 0.06f;

            // Very long-running albums decay a little faster.
            if (album.WeeksOnChart >= 8)
                retention -= 0.08f;

            retention = Mathf.Clamp(
                retention,
                0.35f,
                0.90f
            );

            long result = (long)Math.Round(
                album.WeeklySales * retention
            );

            // Occasional small rebound/word-of-mouth week.
            if (album.WeeksOnChart >= 2 &&
                UnityEngine.Random.value < 0.08f)
            {
                result = (long)Math.Round(
                    result *
                    UnityEngine.Random.Range(1.10f, 1.28f)
                );
            }

            return Math.Max(0L, result);
        }

        public static string GetMovementText(
            AlbumData album
        )
        {
            if (album == null ||
                album.ChartPosition <= 0)
            {
                return "";
            }

            if (album.PreviousChartPosition <= 0)
                return "NEW";

            int difference =
                album.PreviousChartPosition -
                album.ChartPosition;

            if (difference > 0)
                return "▲ " + difference;

            if (difference < 0)
                return "▼ " + Math.Abs(difference);

            return "—";
        }
    }
}
