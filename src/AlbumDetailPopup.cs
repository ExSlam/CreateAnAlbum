using System;
using UnityEngine;
using UnityEngine.UI;

namespace Albummodelite
{
    public class AlbumDetailPopup : MonoBehaviour
    {
        private GameObject panel;

        public bool Open(AlbumData album)
        {
            if (album == null)
                return false;

            bool fromLibrary = AlbumPopupHost.IsOpen(AlbumPopupKind.Library);
            GameObject popupRoot = AlbumPopupHost.Prepare(AlbumPopupKind.Detail);
            if (popupRoot == null)
                return false;

            panel = new GameObject("AlbumDetailPopup");
            panel.transform.SetParent(popupRoot.transform, false);

            RectTransform pr = panel.AddComponent<RectTransform>();
            AlbumUiResources.ConfigureCenteredPanel(
                pr,
                new Vector2(700f, 520f)
            );

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.975f, 0.975f, 0.985f, 0.998f);

            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.78f, 0.76f, 0.87f);
            outline.effectDistance = new Vector2(2, -2);

            CreateText(
                panel.transform,
                "Title",
                album.Title,
                24,
                FontStyle.Bold,
                new Color(0.20f, 0.20f, 0.24f),
                new Vector2(28, -18),
                new Vector2(600, 40),
                TextAnchor.MiddleLeft
            );

            CreateText(
                panel.transform,
                "Group",
                album.GroupName,
                12,
                FontStyle.Bold,
                new Color(0.43f, 0.38f, 0.70f),
                new Vector2(28, -54),
                new Vector2(600, 24),
                TextAnchor.MiddleLeft
            );

            AlbumCoverRenderer.Build(
                panel.transform,
                album,
                "LargeCover",
                new Vector2(28, -90),
                275f
            );

            float x = 330f;

            AddInfoRow(panel.transform, x, 100, "Release", album.ReleaseDate.ToString("MMM d, yyyy"));
            AddInfoRow(panel.transform, x, 135, "Songs", album.Songs != null ? album.Songs.Count.ToString() : "0");
            AddInfoRow(panel.transform, x, 170, "Members", album.Members != null ? album.Members.Count.ToString() : "0");
            AddInfoRow(panel.transform, x, 205, "Sales", FormatNumber(album.Sales));
            AddInfoRow(panel.transform, x, 240, "Peak", album.PeakChartPosition > 0 ? "#" + album.PeakChartPosition : "—");
            AddInfoRow(panel.transform, x, 270, "Weeks", album.WeeksOnChart.ToString());

            CreateText(
                panel.transform,
                "TracksLabel",
                "TRACK LIST",
                12,
                FontStyle.Bold,
                new Color(0.39f, 0.34f, 0.72f),
                new Vector2(x, -320),
                new Vector2(260, 24),
                TextAnchor.MiddleLeft
            );

            if (album.Songs != null)
            {
                for (int i = 0; i < album.Songs.Count && i < 10; i++)
                {
                    singles._single song = album.Songs[i];

                    CreateText(
                        panel.transform,
                        "Track_" + i,
                        (i + 1) + ". " + (song != null ? song.title : "Unknown"),
                        10,
                        FontStyle.Normal,
                        new Color(0.28f, 0.27f, 0.34f),
                        new Vector2(x, -345 - i * 22),
                        new Vector2(300, 20),
                        TextAnchor.MiddleLeft
                    );
                }
            }

            CreateCloseButton();

            if (fromLibrary)
                return AlbumPopupHost.Transition(AlbumPopupKind.Library, AlbumPopupKind.Detail);

            return AlbumPopupHost.Open(AlbumPopupKind.Detail);
        }

        private void AddInfoRow(
            Transform parent,
            float x,
            float y,
            string label,
            string value
        )
        {
            CreateText(
                parent,
                label + "_Label",
                label,
                10,
                FontStyle.Bold,
                new Color(0.44f, 0.43f, 0.50f),
                new Vector2(x, -y),
                new Vector2(90, 22),
                TextAnchor.MiddleLeft
            );

            CreateText(
                parent,
                label + "_Value",
                value,
                10,
                FontStyle.Normal,
                new Color(0.39f, 0.34f, 0.72f),
                new Vector2(x + 95, -y),
                new Vector2(160, 22),
                TextAnchor.MiddleRight
            );
        }

        private void CreateCloseButton()
        {
            GameObject obj = AlbumUiResources.InstantiateButton(
                panel.transform,
                "Close",
                "Close",
                AlbumButtonStyle.Destructive,
                Close
            );
            if (obj == null)
                return;

            RectTransform r = obj.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.5f, 0);
            r.anchorMax = new Vector2(0.5f, 0);
            r.pivot = new Vector2(0.5f, 0);
            r.sizeDelta = new Vector2(140, 32);
            r.anchoredPosition = new Vector2(0, 16);
        }

        private void Close()
        {
            AlbumPopupHost.Close(
                AlbumPopupKind.Detail,
                delegate { panel = null; }
            );
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
