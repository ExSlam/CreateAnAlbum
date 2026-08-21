using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Albummodelite;

namespace CreateAnAlbumChartTrackEnhancements
{
    internal static class AlbumChartEnhancement
    {
        private const int MaximumEntries = 20;

        private static GameObject chartOverlay;

        internal static bool Toggle(bool queueBehindCurrentPopup = false)
        {
            if (AlbumPopupHost.IsOpen(AlbumPopupKind.Chart))
            {
                return Close();
            }

            AlbumPersistenceBridge.EnsurePlayerAlbumsLoaded("Album Chart Open");
            AlbumTrackRepair.RepairAll();

            try
            {
                RivalAlbumManager.EnsureInitialRivals(14);
                AlbumSalesManager.RebuildChartPositions();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AlbumChart] Rival setup warning: " + ex.Message);
            }

            GameObject popupRoot = AlbumPopupHost.Prepare(AlbumPopupKind.Chart);
            if (popupRoot == null)
            {
                Debug.LogError("[AlbumChart] PopupManager host unavailable.");
                return false;
            }

            Build(popupRoot.transform);
            return AlbumPopupHost.Open(
                AlbumPopupKind.Chart,
                queueBehindCurrentPopup
            );
        }

        internal static bool Close()
        {
            return AlbumPopupHost.Close(
                AlbumPopupKind.Chart,
                delegate
                {
                    chartOverlay = null;
                    Debug.Log("[AlbumChart] Closed.");
                }
            );
        }

        private static void Build(Transform popupRoot)
        {
            chartOverlay = popupRoot.gameObject;

            // Exact normalized footprint taken from the user's Singles Chart
            // screenshot: roughly x=163..1197 and y=71..625 at 1366x768.
            GameObject panel =
                new GameObject(
                    "AlbumChartPanel"
                );

            panel.transform.SetParent(
                popupRoot,
                false
            );

            RectTransform pr =
                panel.AddComponent<RectTransform>();

            AlbumUiResources.ConfigureCenteredPanel(
                pr,
                new Vector2(1000f, 480f)
            );
            pr.anchoredPosition = Vector2.zero;

            Image panelBg =
                panel.AddComponent<Image>();

            panelBg.color =
                new Color(
                    0.965f,
                    0.958f,
                    0.958f,
                    1f
                );

            Outline panelOutline =
                panel.AddComponent<Outline>();

            panelOutline.effectColor =
                new Color(
                    0.60f,
                    0.58f,
                    0.64f,
                    0.55f
                );

            panelOutline.effectDistance =
                new Vector2(
                    1f,
                    -1f
                );

            string month =
                staticVars.dateTime
                    .ToString(
                        "MMMM yyyy"
                    );

            CreateTextAnchored(
                panel.transform,
                "Month",
                month,
                25,
                FontStyle.Bold,
                new Color(
                    0.45f,
                    0.47f,
                    0.74f
                ),
                new Vector2(
                    0.31f,
                    0.845f
                ),
                new Vector2(
                    0.69f,
                    0.985f
                ),
                TextAnchor.MiddleCenter
            );

            CreateTextAnchored(
                panel.transform,
                "ChartType",
                "ALBUM CHART",
                13,
                FontStyle.Bold,
                new Color(
                    0.50f,
                    0.50f,
                    0.60f
                ),
                new Vector2(
                    0.025f,
                    0.86f
                ),
                new Vector2(
                    0.25f,
                    0.97f
                ),
                TextAnchor.MiddleLeft
            );

            CreateTextAnchored(
                panel.transform,
                "Cycle",
                "Updates every 2 weeks",
                12,
                FontStyle.Italic,
                new Color(
                    0.52f,
                    0.51f,
                    0.60f
                ),
                new Vector2(
                    0.70f,
                    0.86f
                ),
                new Vector2(
                    0.965f,
                    0.97f
                ),
                TextAnchor.MiddleRight
            );

            GameObject scrollRoot =
                new GameObject(
                    "AlbumChartScrollRoot"
                );

            scrollRoot.transform.SetParent(
                panel.transform,
                false
            );

            RectTransform scrollRootRect =
                scrollRoot
                    .AddComponent<RectTransform>();

            scrollRootRect.anchorMin =
                new Vector2(
                    0.026f,
                    0.13f
                );

            scrollRootRect.anchorMax =
                new Vector2(
                    0.967f,
                    0.83f
                );

            scrollRootRect.offsetMin =
                Vector2.zero;

            scrollRootRect.offsetMax =
                Vector2.zero;

            ScrollRect scroll =
                scrollRoot
                    .AddComponent<ScrollRect>();

            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType =
                ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;

            GameObject viewport =
                new GameObject(
                    "AlbumChartViewport"
                );

            viewport.transform.SetParent(
                scrollRoot.transform,
                false
            );

            RectTransform vr =
                viewport
                    .AddComponent<RectTransform>();

            vr.anchorMin =
                Vector2.zero;

            vr.anchorMax =
                Vector2.one;

            vr.offsetMin =
                Vector2.zero;

            vr.offsetMax =
                Vector2.zero;

            Image viewportBg =
                viewport.AddComponent<Image>();

            viewportBg.color =
                new Color(
                    0.985f,
                    0.985f,
                    0.992f,
                    1f
                );

            viewport.AddComponent<RectMask2D>();

            GameObject list =
                new GameObject(
                    "AlbumChartList"
                );

            list.transform.SetParent(
                viewport.transform,
                false
            );

            RectTransform lr =
                list.AddComponent<RectTransform>();

            lr.anchorMin =
                new Vector2(
                    0f,
                    1f
                );

            lr.anchorMax =
                new Vector2(
                    1f,
                    1f
                );

            lr.pivot =
                new Vector2(
                    0.5f,
                    1f
                );

            lr.anchoredPosition =
                Vector2.zero;

            List<AlbumData> entries =
                GetEntries();

            const float rowHeight = 98f;
            const float rowGap = 13f;
            float pitch =
                rowHeight + rowGap;

            float requiredHeight =
                Mathf.Max(
                    436f,
                    entries.Count *
                        pitch
                );

            lr.sizeDelta =
                new Vector2(
                    0f,
                    requiredHeight
                );

            scroll.viewport = vr;
            scroll.content = lr;
            AlbumUiResources.AttachVanillaListScrollIndicator(
                scrollRoot.transform,
                scroll,
                "AlbumChartScrollIndicator"
            );
            scroll.verticalNormalizedPosition = 1f;

            if (entries.Count == 0)
            {
                CreateText(
                    list.transform,
                    "Empty",
                    "No albums have charted yet.",
                    20,
                    FontStyle.Italic,
                    new Color(
                        0.50f,
                        0.49f,
                        0.57f
                    ),
                    new Vector2(
                        15f,
                        -160f
                    ),
                    new Vector2(
                        900f,
                        50f
                    ),
                    TextAnchor.MiddleCenter
                );
            }
            else
            {
                for (int i = 0;
                     i < entries.Count;
                     i++)
                {
                    DrawRow(
                        list.transform,
                        entries[i],
                        i + 1,
                        i,
                        pitch
                    );
                }

                AlbumChartCoverVirtualizer virtualizer =
                    scrollRoot.AddComponent<AlbumChartCoverVirtualizer>();
                virtualizer.Configure(scroll, lr, entries, pitch);
            }

            // Keep the close control tied to the fixed chart panel on every aspect ratio.
            CreateButtonAnchored(
                panel.transform,
                "Close",
                "Close",
                new Vector2(
                    0.40f,
                    0.025f
                ),
                new Vector2(
                    0.60f,
                    0.095f
                ),
                delegate
                {
                    Close();
                }
            );

            Debug.Log(
                "[AlbumChart] Opened with " +
                entries.Count +
                " album(s)."
            );
        }

        private static List<AlbumData>
            GetEntries()
        {
            if (Albums.AlbumList == null)
                return new List<AlbumData>();

            return Albums.AlbumList
                .Where(a =>
                    a != null &&
                    a.Released &&
                    a.WeeklySales > 0L)
                .OrderBy(a =>
                    a.ChartPosition > 0
                        ? a.ChartPosition
                        : int.MaxValue)
                .ThenByDescending(
                    a => a.WeeklySales)
                .Take(MaximumEntries)
                .ToList();
        }

        private static void DrawRow(
            Transform parent,
            AlbumData album,
            int rank,
            int rowIndex,
            float pitch)
        {
            GameObject row =
                new GameObject(
                    "AlbumRank_" +
                    rank
                );

            row.transform.SetParent(
                parent,
                false
            );

            RectTransform rr =
                row.AddComponent<RectTransform>();

            rr.anchorMin =
                new Vector2(
                    0f,
                    1f
                );

            rr.anchorMax =
                new Vector2(
                    1f,
                    1f
                );

            rr.pivot =
                new Vector2(
                    0.5f,
                    1f
                );

            rr.sizeDelta =
                new Vector2(
                    -10f,
                    98f
                );

            rr.anchoredPosition =
                new Vector2(
                    0f,
                    -(rowIndex *
                      pitch)
                );

            Image rowBg =
                row.AddComponent<Image>();

            rowBg.color = Color.white;

            // Rank block.
            GameObject rankBox =
                new GameObject("Rank");

            rankBox.transform.SetParent(
                row.transform,
                false
            );

            RectTransform rankRect =
                rankBox.AddComponent<RectTransform>();

            rankRect.anchorMin =
                new Vector2(
                    0f,
                    0f
                );

            rankRect.anchorMax =
                new Vector2(
                    0.082f,
                    1f
                );

            rankRect.offsetMin =
                Vector2.zero;

            rankRect.offsetMax =
                Vector2.zero;

            Image rankBg =
                rankBox.AddComponent<Image>();

            rankBg.color =
                new Color(
                    0.49f,
                    0.51f,
                    0.77f
                );

            CreateTextAnchored(
                rankBox.transform,
                "RankNumber",
                rank.ToString(),
                37,
                FontStyle.Normal,
                Color.white,
                Vector2.zero,
                Vector2.one,
                TextAnchor.MiddleCenter
            );

            // Album art slot.
            GameObject coverSlot =
                new GameObject(
                    "CoverSlot"
                );

            coverSlot.transform.SetParent(
                row.transform,
                false
            );

            RectTransform cr =
                coverSlot
                    .AddComponent<RectTransform>();

            cr.anchorMin =
                new Vector2(
                    0.095f,
                    0.07f
                );

            cr.anchorMax =
                new Vector2(
                    0.185f,
                    0.93f
                );

            cr.offsetMin =
                Vector2.zero;

            cr.offsetMax =
                Vector2.zero;

            Image cbg =
                coverSlot
                    .AddComponent<Image>();

            cbg.color =
                new Color(
                    0.96f,
                    0.96f,
                    0.97f
                );

            Outline coverOutline =
                coverSlot
                    .AddComponent<Outline>();

            coverOutline.effectColor =
                new Color(
                    0.75f,
                    0.75f,
                    0.82f
                );

            coverOutline.effectDistance =
                new Vector2(
                    1f,
                    -1f
                );


            string owner =
                !string.IsNullOrEmpty(
                    album.GroupName)
                    ? album.GroupName
                    : (!string.IsNullOrEmpty(
                        album.RivalName)
                        ? album.RivalName
                        : "Unknown");

            CreateTextAnchored(
                row.transform,
                "Title",
                owner +
                    "  -  " +
                    album.Title,
                19,
                FontStyle.Bold,
                new Color(
                    0.45f,
                    0.46f,
                    0.73f
                ),
                new Vector2(
                    0.205f,
                    0.50f
                ),
                new Vector2(
                    0.78f,
                    0.93f
                ),
                TextAnchor.MiddleLeft
            );

            CreateTextAnchored(
                row.transform,
                "Movement",
                GetMovement(album),
                14,
                FontStyle.Bold,
                GetMovementColor(album),
                new Vector2(
                    0.205f,
                    0.12f
                ),
                new Vector2(
                    0.33f,
                    0.50f
                ),
                TextAnchor.MiddleLeft
            );

            string detail =
                "Peak " +
                (album.PeakChartPosition > 0
                    ? "#" +
                      album.PeakChartPosition
                    : "—") +
                "   |   " +
                Math.Max(
                    1,
                    album.WeeksOnChart
                ) +
                " chart update" +
                (Math.Max(
                    1,
                    album.WeeksOnChart
                ) == 1
                    ? ""
                    : "s");

            CreateTextAnchored(
                row.transform,
                "Details",
                detail,
                12,
                FontStyle.Normal,
                new Color(
                    0.52f,
                    0.51f,
                    0.65f
                ),
                new Vector2(
                    0.37f,
                    0.12f
                ),
                new Vector2(
                    0.77f,
                    0.50f
                ),
                TextAnchor.MiddleLeft
            );

            long sales =
                album.WeeklySales > 0L
                    ? album.WeeklySales
                    : album.Sales;

            CreateTextAnchored(
                row.transform,
                "Sales",
                "Sales: " +
                    sales.ToString("N0"),
                15,
                FontStyle.Bold,
                new Color(
                    0.49f,
                    0.49f,
                    0.72f
                ),
                new Vector2(
                    0.79f,
                    0.25f
                ),
                new Vector2(
                    0.985f,
                    0.75f
                ),
                TextAnchor.MiddleRight
            );
        }

        private static string GetMovement(
            AlbumData album)
        {
            if (album == null ||
                album.ChartPosition <= 0)
            {
                return AlbumLocalization.Get(
                    "createanalbum.chart.out",
                    "OUT"
                );
            }

            if (album.PreviousChartPosition <= 0)
            {
                return AlbumLocalization.Get(
                    "createanalbum.chart.new",
                    "NEW"
                );
            }

            if (album.ChartPosition <
                album.PreviousChartPosition)
            {
                return
                    "▲ " +
                    (album.PreviousChartPosition -
                     album.ChartPosition);
            }

            if (album.ChartPosition >
                album.PreviousChartPosition)
            {
                return
                    "▼ " +
                    (album.ChartPosition -
                     album.PreviousChartPosition);
            }

            return AlbumLocalization.Get(
                "createanalbum.chart.no_change",
                "No change"
            );
        }

        private static Color GetMovementColor(
            AlbumData album)
        {
            if (album == null || album.ChartPosition <= 0)
            {
                return new Color(
                    0.50f,
                    0.50f,
                    0.56f
                );
            }

            if (album.PreviousChartPosition <= 0)
            {
                return new Color(
                    0.32f,
                    0.48f,
                    0.84f
                );
            }

            if (album.ChartPosition <
                album.PreviousChartPosition)
            {
                return new Color(
                    0.28f,
                    0.68f,
                    0.42f
                );
            }

            if (album.ChartPosition >
                album.PreviousChartPosition)
            {
                return new Color(
                    0.79f,
                    0.35f,
                    0.37f
                );
            }

            return new Color(
                0.50f,
                0.50f,
                0.56f
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
            TextAnchor alignment)
        {
            GameObject obj =
                new GameObject(name);

            obj.transform.SetParent(
                parent,
                false
            );

            RectTransform rect =
                obj.AddComponent<RectTransform>();

            rect.anchorMin =
                new Vector2(
                    0f,
                    1f
                );

            rect.anchorMax =
                new Vector2(
                    0f,
                    1f
                );

            rect.pivot =
                new Vector2(
                    0f,
                    1f
                );

            rect.anchoredPosition =
                position;

            rect.sizeDelta =
                size;

            Text text =
                obj.AddComponent<Text>();

            text.font =
                AlbumUiResources.GetGameFont();

            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
        }

        private static void CreateTextAnchored(
            Transform parent,
            string name,
            string value,
            int fontSize,
            FontStyle style,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            TextAnchor alignment)
        {
            GameObject obj =
                new GameObject(name);

            obj.transform.SetParent(
                parent,
                false
            );

            RectTransform rect =
                obj.AddComponent<RectTransform>();

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin =
                Vector2.zero;
            rect.offsetMax =
                Vector2.zero;

            Text text =
                obj.AddComponent<Text>();

            text.font =
                AlbumUiResources.GetGameFont();

            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize =
                Math.Max(
                    8,
                    fontSize - 5
                );
            text.resizeTextMaxSize =
                fontSize;
        }

        private static void CreateButtonAnchored(
            Transform parent,
            string name,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            UnityEngine.Events.UnityAction action)
        {
            GameObject obj = AlbumUiResources.InstantiateButton(
                parent,
                name,
                label,
                AlbumButtonStyle.Destructive,
                action
            );
            if (obj == null)
                return;

            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

}
