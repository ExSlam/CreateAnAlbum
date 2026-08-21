using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Albummodelite
{
    internal sealed class AlbumChartPeriodState
    {
        internal long Sales;
        internal int ChartPosition;
    }

    internal sealed class AlbumChartPeriodResult
    {
        internal string AlbumTitle;
        internal long SalesGained;
        internal long FansGained;
        internal int PreviousChartPosition;
        internal int ChartPosition;
    }

    internal sealed class AlbumChartPeriodReport
    {
        internal DateTime Date;
        internal string SaveId;
        internal int LoadGeneration;
        internal List<AlbumChartPeriodResult> Results;
    }

    internal static class AlbumChartPeriodPerformance
    {
        internal static Dictionary<string, AlbumChartPeriodState> Capture()
        {
            Dictionary<string, AlbumChartPeriodState> state =
                new Dictionary<string, AlbumChartPeriodState>();
            if (Albums.AlbumList == null)
                return state;

            foreach (AlbumData album in Albums.AlbumList)
            {
                if (album == null || !album.PlayerAlbum || !album.Released)
                    continue;

                state[Albums.GetIdentityKey(album)] = new AlbumChartPeriodState
                {
                    Sales = album.Sales,
                    ChartPosition = album.ChartPosition
                };
            }
            return state;
        }

        internal static List<AlbumChartPeriodResult> Complete(
            Dictionary<string, AlbumChartPeriodState> state)
        {
            List<AlbumChartPeriodResult> results =
                new List<AlbumChartPeriodResult>();
            if (Albums.AlbumList == null)
                return results;

            foreach (AlbumData album in Albums.AlbumList)
            {
                if (album == null || !album.PlayerAlbum || !album.Released)
                    continue;

                AlbumChartPeriodState before = null;
                if (state != null)
                    state.TryGetValue(Albums.GetIdentityKey(album), out before);

                long salesGained = Math.Max(
                    0L,
                    album.Sales - (before != null ? before.Sales : 0L)
                );
                if (salesGained <= 0L)
                    continue;

                long fansGained = AddPeriodFans(album, salesGained);
                results.Add(new AlbumChartPeriodResult
                {
                    AlbumTitle = album.Title ?? "",
                    SalesGained = salesGained,
                    FansGained = fansGained,
                    PreviousChartPosition = before != null ? before.ChartPosition : 0,
                    ChartPosition = album.ChartPosition
                });
            }

            return results
                .OrderByDescending(result => result.SalesGained)
                .ToList();
        }

        private static long AddPeriodFans(AlbumData album, long salesGained)
        {
            if (album == null || album.Members == null)
                return 0L;

            List<data_girls.girls> activeMembers = album.Members
                .Where(member =>
                    member != null &&
                    member.status != data_girls._status.graduated)
                .Distinct()
                .ToList();
            if (activeMembers.Count == 0)
                return 0L;

            long fans = Math.Max(1L, (long)Math.Round(salesGained * 0.02d));
            try
            {
                data_girls.AddFans_Equally(fans, activeMembers);
                return fans;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumChartRewards] Could not award album fans: " +
                    ex.Message
                );
                return 0L;
            }
        }
    }

    internal sealed class AlbumChartUpdatePopup : MonoBehaviour
    {
        private const float PopupIdleGraceSeconds = 0.5f;
        private const int RequiredPopupIdleFrames = 3;

        private static AlbumChartUpdatePopup instance;
        private readonly Queue<AlbumChartPeriodReport> pending =
            new Queue<AlbumChartPeriodReport>();
        private GameObject panel;
        private float popupIdleSince = -1f;
        private int popupIdleFrames;

        private void Awake()
        {
            instance = this;
        }

        private void OnDestroy()
        {
            pending.Clear();
            panel = null;
            popupIdleSince = -1f;
            popupIdleFrames = 0;
            if (instance == this)
                instance = null;
        }

        internal static void ResetForSaveLoad()
        {
            if (instance == null)
                return;

            instance.pending.Clear();
            instance.panel = null;
            instance.popupIdleSince = -1f;
            instance.popupIdleFrames = 0;
        }

        internal void Enqueue(
            List<AlbumChartPeriodResult> results,
            DateTime date)
        {
            pending.Enqueue(new AlbumChartPeriodReport
            {
                Date = date,
                SaveId = AlbumPersistence.CurrentSaveId,
                LoadGeneration = AlbumPersistence.LoadGeneration,
                Results = results ?? new List<AlbumChartPeriodResult>()
            });
            popupIdleSince = -1f;
            popupIdleFrames = 0;
        }

        private void LateUpdate()
        {
            if (!AlbumPersistence.IsCurrentSaveLoaded)
            {
                popupIdleSince = -1f;
                popupIdleFrames = 0;
                return;
            }

            bool popupActive = AlbumPopupHost.IsOpenQueuedOrClosing(
                AlbumPopupKind.ChartUpdate
            );
            if (panel != null && !popupActive)
                panel = null;

            while (pending.Count > 0 &&
                (!string.Equals(
                        pending.Peek().SaveId,
                        AlbumPersistence.CurrentSaveId,
                        StringComparison.Ordinal) ||
                    pending.Peek().LoadGeneration !=
                        AlbumPersistence.LoadGeneration))
            {
                pending.Dequeue();
            }

            if (pending.Count == 0 || panel != null || popupActive ||
                !AlbumPopupHost.IsPopupSystemIdle())
            {
                popupIdleSince = -1f;
                popupIdleFrames = 0;
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (popupIdleSince < 0f)
            {
                popupIdleSince = now;
                popupIdleFrames = 1;
                return;
            }
            popupIdleFrames++;
            if (popupIdleFrames < RequiredPopupIdleFrames ||
                now - popupIdleSince < PopupIdleGraceSeconds)
                return;

            AlbumChartPeriodReport report = pending.Peek();
            popupIdleSince = -1f;
            popupIdleFrames = 0;
            if (TryOpen(report))
                pending.Dequeue();
        }

        private bool TryOpen(AlbumChartPeriodReport report)
        {
            GameObject popupRoot = AlbumPopupHost.Prepare(
                AlbumPopupKind.ChartUpdate
            );
            if (popupRoot == null)
                return false;

            panel = new GameObject("AlbumChartUpdatePanel");
            panel.transform.SetParent(popupRoot.transform, false);

            RectTransform panelRect = panel.AddComponent<RectTransform>();
            AlbumUiResources.ConfigureCenteredPanel(
                panelRect,
                new Vector2(720f, 500f)
            );

            Image background = panel.AddComponent<Image>();
            background.color = new Color(0.97f, 0.97f, 0.98f, 1f);

            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.72f, 0.70f, 0.84f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);

            DrawHeader(report);
            DrawResults(report);
            DrawFooter(report);

            bool accepted = AlbumPopupHost.Open(AlbumPopupKind.ChartUpdate);
            if (!accepted)
                panel = null;
            else
            {
                Debug.Log(
                    "[AlbumChartRewards] Performance popup opened. +" +
                    report.Results.Sum(result => result.SalesGained).ToString("N0") +
                    " sales, +" +
                    report.Results.Sum(result => result.FansGained).ToString("N0") +
                    " fans."
                );
            }
            return accepted;
        }

        private void DrawHeader(AlbumChartPeriodReport report)
        {
            CreateText(
                panel.transform,
                "Header",
                AlbumLocalization.Get(
                    "createanalbum.chart.update",
                    "ALBUM CHART UPDATE"
                ),
                20,
                FontStyle.Bold,
                new Color(0.40f, 0.42f, 0.74f),
                new Vector2(28f, -15f),
                new Vector2(664f, 30f),
                TextAnchor.MiddleCenter
            );

            CreateText(
                panel.transform,
                "Date",
                ExtensionMethods.ToString_Loc(
                    report.Date,
                    "DATETIME__LONG"
                ),
                10,
                FontStyle.Italic,
                new Color(0.42f, 0.42f, 0.48f),
                new Vector2(28f, -47f),
                new Vector2(664f, 20f),
                TextAnchor.MiddleCenter
            );
        }

        private void DrawResults(AlbumChartPeriodReport report)
        {
            GameObject scrollRoot = new GameObject("PerformanceScrollRoot");
            scrollRoot.transform.SetParent(panel.transform, false);

            RectTransform rootRect = scrollRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = new Vector2(24f, 104f);
            rootRect.offsetMax = new Vector2(-24f, -78f);

            ScrollRect scroll = scrollRoot.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;

            GameObject viewport = new GameObject("PerformanceViewport");
            viewport.transform.SetParent(scrollRoot.transform, false);
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0.985f, 0.985f, 0.992f, 1f);
            viewport.AddComponent<RectMask2D>();

            GameObject content = new GameObject("PerformanceContent");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(
                0f,
                Mathf.Max(310f, report.Results.Count * 68f + 12f)
            );

            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            AlbumUiResources.AttachVanillaListScrollIndicator(
                scrollRoot.transform,
                scroll,
                "PerformanceScrollIndicator"
            );
            scroll.verticalNormalizedPosition = 1f;

            if (report.Results.Count == 0)
            {
                CreateText(
                    content.transform,
                    "NoSales",
                    AlbumLocalization.Get(
                        "createanalbum.chart.no_sales",
                        "No player album sales this chart period."
                    ),
                    13,
                    FontStyle.Italic,
                    new Color(0.35f, 0.34f, 0.40f),
                    new Vector2(20f, -100f),
                    new Vector2(610f, 50f),
                    TextAnchor.MiddleCenter
                );
                return;
            }

            for (int i = 0; i < report.Results.Count; i++)
                DrawResultRow(content.transform, report.Results[i], i);
        }

        private void DrawResultRow(
            Transform parent,
            AlbumChartPeriodResult result,
            int index)
        {
            GameObject row = new GameObject("PerformanceRow_" + index);
            row.transform.SetParent(parent, false);
            RectTransform rowRect = row.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.sizeDelta = new Vector2(-12f, 58f);
            rowRect.anchoredPosition = new Vector2(0f, -6f - index * 68f);

            Image rowBackground = row.AddComponent<Image>();
            rowBackground.color = Color.white;
            Outline rowOutline = row.AddComponent<Outline>();
            rowOutline.effectColor = new Color(0.84f, 0.83f, 0.90f, 1f);
            rowOutline.effectDistance = new Vector2(1f, -1f);

            string title = !string.IsNullOrEmpty(result.AlbumTitle)
                ? result.AlbumTitle
                : AlbumLocalization.Get("createanalbum.ui.album", "Album");
            string movement = GetMovementText(result);
            string position = result.ChartPosition > 0
                ? "#" + result.ChartPosition +
                    (string.IsNullOrEmpty(movement) ? "" : "  " + movement)
                : AlbumLocalization.Get("createanalbum.chart.out", "OUT");
            CreateText(
                row.transform,
                "PositionAndAlbum",
                position + "  " + title,
                11,
                FontStyle.Bold,
                mainScript.black32,
                new Vector2(14f, -10f),
                new Vector2(350f, 36f),
                TextAnchor.MiddleLeft
            );

            string sales = string.Format(
                AlbumLocalization.Get(
                    "createanalbum.chart.sales_gain",
                    "+{0} sales"
                ),
                result.SalesGained.ToString("N0")
            );
            string fans = string.Format(
                AlbumLocalization.Get(
                    "createanalbum.chart.fans_gain",
                    "+{0} fans"
                ),
                result.FansGained.ToString("N0")
            );
            CreateText(
                row.transform,
                "Gains",
                sales + "   " + fans,
                10,
                FontStyle.Bold,
                new Color(0.25f, 0.55f, 0.34f),
                new Vector2(380f, -10f),
                new Vector2(220f, 36f),
                TextAnchor.MiddleRight
            );
        }

        private static string GetMovementText(AlbumChartPeriodResult result)
        {
            if (result == null || result.ChartPosition <= 0)
                return "";
            if (result.PreviousChartPosition <= 0)
            {
                return AlbumLocalization.Get(
                    "createanalbum.chart.new",
                    "NEW"
                );
            }

            int difference = result.PreviousChartPosition - result.ChartPosition;
            if (difference > 0)
                return "▲ " + difference;
            if (difference < 0)
                return "▼ " + Math.Abs(difference);
            return "—";
        }

        private void DrawFooter(AlbumChartPeriodReport report)
        {
            long totalSales = report.Results.Sum(result => result.SalesGained);
            long totalFans = report.Results.Sum(result => result.FansGained);
            string totalLabel = AlbumLocalization.Get(
                "createanalbum.chart.period_total",
                "Period total"
            );
            string sales = string.Format(
                AlbumLocalization.Get(
                    "createanalbum.chart.sales_gain",
                    "+{0} sales"
                ),
                totalSales.ToString("N0")
            );
            string fans = string.Format(
                AlbumLocalization.Get(
                    "createanalbum.chart.fans_gain",
                    "+{0} fans"
                ),
                totalFans.ToString("N0")
            );
            CreateText(
                panel.transform,
                "Totals",
                totalLabel + "   " + sales + "   " + fans,
                11,
                FontStyle.Bold,
                new Color(0.40f, 0.42f, 0.74f),
                new Vector2(30f, -405f),
                new Vector2(660f, 28f),
                TextAnchor.MiddleCenter
            );

            GameObject close = AlbumUiResources.InstantiateButton(
                panel.transform,
                "Close",
                AlbumLocalization.Get("createanalbum.ui.close", "Close"),
                AlbumButtonStyle.Destructive,
                Close
            );
            if (close == null)
                return;

            RectTransform closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.sizeDelta = new Vector2(150f, 32f);
            closeRect.anchoredPosition = new Vector2(0f, 15f);
        }

        private void Close()
        {
            AlbumPopupHost.Close(
                AlbumPopupKind.ChartUpdate,
                delegate { panel = null; }
            );
        }

        private static void CreateText(
            Transform parent,
            string name,
            string value,
            int fontSize,
            FontStyle style,
            Color color,
            Vector2 position,
            Vector2 size,
            TextAnchor anchor)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Text text = obj.AddComponent<Text>();
            text.font = AlbumUiResources.GetGameFont();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Math.Max(7, fontSize - 3);
            text.resizeTextMaxSize = fontSize;
        }
    }
}
