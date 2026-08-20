using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Albummodelite
{
    public class AlbumLibraryPopup : MonoBehaviour
    {
        private GameObject panel;
        private GameObject content;

        public void Open()
        {
            if (AlbumPopupHost.IsOpen(AlbumPopupKind.Library))
            {
                Close();
                return;
            }

            AlbumPersistence.EnsureCurrentSaveLoaded();
            Albums.DeduplicateInPlace();

            GameObject popupRoot = AlbumPopupHost.Prepare(AlbumPopupKind.Library);
            if (popupRoot == null)
                return;

            panel = new GameObject("AlbumLibraryPopup");
            panel.transform.SetParent(popupRoot.transform, false);

            RectTransform pr = panel.AddComponent<RectTransform>();
            pr.anchorMin = new Vector2(0.06f, 0.06f);
            pr.anchorMax = new Vector2(0.94f, 0.94f);
            pr.offsetMin = Vector2.zero;
            pr.offsetMax = Vector2.zero;

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.965f, 0.965f, 0.975f, 0.995f);

            DrawHeader();
            DrawLibrary();
            DrawCloseButton();
            AlbumPopupHost.Open(AlbumPopupKind.Library);
        }

        private void DrawHeader()
        {
            CreateText(
                panel.transform,
                "Title",
                "Albums",
                24,
                FontStyle.Bold,
                new Color(0.39f, 0.34f, 0.72f),
                new Vector2(32, -18),
                new Vector2(400, 40),
                TextAnchor.MiddleLeft
            );

            CreateText(
                panel.transform,
                "Count",
                GetLibraryAlbums().Count + " released",
                12,
                FontStyle.Normal,
                new Color(0.50f, 0.49f, 0.58f),
                new Vector2(720, -20),
                new Vector2(180, 40),
                TextAnchor.MiddleRight
            );
        }

        private void DrawLibrary()
        {
            List<AlbumData> libraryAlbums =
                GetLibraryAlbums();

            GameObject viewport = new GameObject("LibraryViewport");
            viewport.transform.SetParent(panel.transform, false);

            RectTransform vr = viewport.AddComponent<RectTransform>();
            vr.anchorMin = new Vector2(0, 0);
            vr.anchorMax = new Vector2(1, 1);
            vr.offsetMin = new Vector2(28, 68);
            vr.offsetMax = new Vector2(-28, -72);

            Image viewportBg = viewport.AddComponent<Image>();
            viewportBg.color = new Color(0.985f, 0.985f, 0.992f);

            Outline outline = viewport.AddComponent<Outline>();
            outline.effectColor = new Color(0.83f, 0.82f, 0.89f);
            outline.effectDistance = new Vector2(1, -1);

            viewport.AddComponent<RectMask2D>();

            ScrollRect scroll = viewport.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35f;

            content = new GameObject("LibraryContent");
            content.transform.SetParent(viewport.transform, false);

            RectTransform cr = content.AddComponent<RectTransform>();
            cr.anchorMin = new Vector2(0, 1);
            cr.anchorMax = new Vector2(1, 1);
            cr.pivot = new Vector2(0.5f, 1);

            int columns = 4;
            int rows = Mathf.Max(
                1,
                Mathf.CeilToInt(libraryAlbums.Count / (float)columns)
            );

            cr.sizeDelta = new Vector2(-20, rows * 245f + 20f);
            cr.anchoredPosition = Vector2.zero;

            scroll.viewport = vr;
            scroll.content = cr;

            if (libraryAlbums.Count == 0)
            {
                CreateText(
                    content.transform,
                    "Empty",
                    "No albums released yet.\nCreate an album first, then it will appear here.",
                    16,
                    FontStyle.Italic,
                    new Color(0.50f, 0.49f, 0.58f),
                    new Vector2(100, -130),
                    new Vector2(700, 80),
                    TextAnchor.MiddleCenter
                );

                return;
            }

            for (int i = 0; i < libraryAlbums.Count; i++)
            {
                AlbumData album = libraryAlbums[i];
                if (album == null)
                    continue;

                int row = i / columns;
                int col = i % columns;

                DrawAlbumCard(
                    album,
                    new Vector2(
                        28 + col * 215f,
                        -20 - row * 245f
                    )
                );
            }
        }

        private List<AlbumData> GetLibraryAlbums()
        {
            return Albums
                .GetUniqueAlbumsSnapshot()
                .Where(a =>
                    a != null &&
                    a.PlayerAlbum &&
                    a.Released)
                .OrderBy(a => a.ReleaseDate)
                .ThenBy(a => a.ID)
                .ToList();
        }

        private void DrawAlbumCard(
            AlbumData album,
            Vector2 position
        )
        {
            GameObject card = new GameObject("AlbumCard_" + album.ID);
            card.transform.SetParent(content.transform, false);

            RectTransform r = card.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(0, 1);
            r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.sizeDelta = new Vector2(195, 225);
            r.anchoredPosition = position;

            Image bg = card.AddComponent<Image>();
            bg.color = Color.white;

            Outline outline = card.AddComponent<Outline>();
            outline.effectColor = new Color(0.82f, 0.82f, 0.88f);
            outline.effectDistance = new Vector2(1, -1);

            Button button = card.AddComponent<Button>();
            AlbumData captured = album;
            button.onClick.AddListener(() => OpenAlbumDetails(captured));

            AlbumCoverRenderer.Build(
                card.transform,
                album,
                "Cover",
                new Vector2(12, -12),
                171f
            );

            CreateText(
                card.transform,
                "AlbumTitle",
                album.Title,
                12,
                FontStyle.Bold,
                new Color(0.20f, 0.20f, 0.24f),
                new Vector2(12, -187),
                new Vector2(171, 18),
                TextAnchor.MiddleLeft
            );

            string sales =
                album.Sales > 0
                    ? FormatNumber(album.Sales) + " sales"
                    : "New release";

            CreateText(
                card.transform,
                "Sales",
                sales,
                9,
                FontStyle.Normal,
                new Color(0.48f, 0.46f, 0.58f),
                new Vector2(12, -205),
                new Vector2(171, 15),
                TextAnchor.MiddleLeft
            );
        }

        private void OpenAlbumDetails(AlbumData album)
        {
            AlbumDetailPopup details =
                gameObject.GetComponent<AlbumDetailPopup>();

            if (details == null)
                details = gameObject.AddComponent<AlbumDetailPopup>();

            details.Open(album);
        }

        private void DrawCloseButton()
        {
            GameObject buttonObj = AlbumUiResources.InstantiateButton(
                panel.transform,
                "Close",
                "Close",
                false,
                Close
            );
            if (buttonObj == null)
                return;

            RectTransform r = buttonObj.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.5f, 0);
            r.anchorMax = new Vector2(0.5f, 0);
            r.pivot = new Vector2(0.5f, 0);
            r.sizeDelta = new Vector2(150, 32);
            r.anchoredPosition = new Vector2(0, 18);
        }

        private void Close()
        {
            AlbumPopupHost.Close(AlbumPopupKind.Library);
            panel = null;
        }

        private string FormatNumber(long value)
        {
            if (value >= 1000000000L)
                return (value / 1000000000f).ToString("0.0") + "B";
            if (value >= 1000000L)
                return (value / 1000000f).ToString("0.0") + "M";
            if (value >= 1000L)
                return (value / 1000f).ToString("0.0") + "K";

            return value.ToString();
        }

        private void CreateText(
            Transform parent,
            string name,
            string value,
            int fontSize,
            FontStyle style,
            Color color,
            Vector2 position,
            Vector2 size,
            TextAnchor anchor
        )
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            RectTransform r = obj.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(0, 1);
            r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.anchoredPosition = position;
            r.sizeDelta = size;

            Text text = obj.AddComponent<Text>();
            text.font = AlbumUiResources.GetGameFont();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
        }
    }
}
