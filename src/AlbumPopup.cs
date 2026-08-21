using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Albummodelite
{
    public class AlbumPopup : MonoBehaviour
    {
        enum AlbumStep
        {
            Info,
            Members,
            Cover,
            Release
        }

        private AlbumStep step = AlbumStep.Info;

        private GameObject panel;
        private GameObject content;
        private ScrollRect contentScroll;
        private ScrollRect coverOptionsScroll;
        private AlbumStep renderedStep;
        private bool hasRenderedStep;

        public string AlbumTitle = "";

        private TMP_InputField albumTitleInput;

        private readonly Dictionary<int, Image> memberBoxes =
            new Dictionary<int, Image>();

        private readonly List<int> selectedMembers =
            new List<int>();

        private readonly List<data_girls.girls> selectedGirls =
            new List<data_girls.girls>();

        // Album track list
        private readonly List<singles._single> selectedSongs =
            new List<singles._single>();

        // Cover designer
        private Font[] albumFonts;
        private int selectedFont = 0;

        private readonly string[] fontNames =
        {
            "Elegant",
            "Classic",
            "Bold",
            "Script"
        };

        private readonly string[] themeNames =
        {
            "Dreamy",
            "Dark",
            "Neon",
            "Vintage",
            "Minimal"
        };

        private int selectedTheme = 0;
        private int selectedBackground = 0;
        private int selectedLayout = 2;
        private int selectedTextColor = 0;

        // Advanced cover customization
        private int titlePosition = 2; // 0 top, 1 middle, 2 bottom
        private bool showGroupName = true;
        private int ornamentStyle = 0; // 0 crown, 1 diamonds, 2 stars
        private float portraitScale = 1.00f;
        private float centerEmphasis = 1.08f;
        private float effectsIntensity = 1.00f;

        // More cover controls
        private data_girls.girls selectedCenterGirl = null;
        private float portraitYOffset = 0f;
        private float portraitSpacing = 1.00f;
        private int frameStyle = 1; // 0 none, 1 simple, 2 elegant, 3 stars, 4 neon
        private int titleEffect = 1; // 0 none, 1 shadow, 2 outline, 3 glow

        private readonly Color[] titleColors =
        {
            Color.white,
            Color.black,
            new Color(0.95f, 0.82f, 0.40f),
            new Color(1.00f, 0.55f, 0.75f),
            new Color(0.70f, 0.45f, 0.95f),
            new Color(0.35f, 0.85f, 0.90f),
            new Color(0.55f, 0.72f, 1.00f)
        };

        private readonly List<Sprite> coverBackgrounds =
            new List<Sprite>();

        private GameObject coverPreviewHolder;

        public bool Open(bool queueBehindCurrentPopup = false)
        {
            if (AlbumPopupHost.IsOpen(AlbumPopupKind.Create))
            {
                return AlbumPopupHost.Close(
                    AlbumPopupKind.Create,
                    delegate { panel = null; }
                );
            }

            GameObject popupRoot = AlbumPopupHost.Prepare(AlbumPopupKind.Create);
            if (popupRoot == null)
                return false;

            Debug.Log("[CreateAlbum] Popup Open");

            panel = new GameObject("AlbumPopup");
            panel.transform.SetParent(popupRoot.transform, false);

            RectTransform rect = panel.AddComponent<RectTransform>();
            AlbumUiResources.ConfigureCenteredPanel(
                rect,
                new Vector2(1000f, 500f)
            );

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.965f, 0.965f, 0.975f, 0.99f);

            DrawHeader();
            CreateContent();
            RefreshUI();
            return AlbumPopupHost.Open(
                AlbumPopupKind.Create,
                queueBehindCurrentPopup
            );
        }

        private void DrawHeader()
        {
            CreatePanelText(
                panel.transform,
                "HeaderTitle",
                "Create Album",
                20,
                FontStyle.Normal,
                new Color(0.39f, 0.34f, 0.72f),
                new Vector2(34, -14),
                new Vector2(420, 40),
                TextAnchor.MiddleLeft
            );

            GameObject line = new GameObject("Divider");
            line.transform.SetParent(panel.transform, false);

            Image divider = line.AddComponent<Image>();
            divider.color = new Color(0.84f, 0.81f, 0.94f);

            RectTransform lr = line.GetComponent<RectTransform>();
            lr.anchorMin = new Vector2(0, 1);
            lr.anchorMax = new Vector2(1, 1);
            lr.pivot = new Vector2(0.5f, 1f);
            lr.sizeDelta = new Vector2(-68, 2);
            lr.anchoredPosition = new Vector2(0, -58);

            DrawStepHeader();
        }

        private void DrawStepHeader()
        {
            string[] labels =
            {
                "1  Album Info",
                "2  Members",
                "3  Cover Design",
                "4  Release"
            };

            for (int i = 0; i < labels.Length; i++)
            {
                bool active = (int)step == i;
                bool complete = (int)step > i;

                Color color = active
                    ? new Color(0.39f, 0.34f, 0.78f)
                    : complete
                        ? new Color(0.35f, 0.65f, 0.48f)
                        : new Color(0.58f, 0.58f, 0.64f);

                CreatePanelText(
                    panel.transform,
                    "Step_" + i,
                    complete ? labels[i] + " ✓" : labels[i],
                    12,
                    active ? FontStyle.Bold : FontStyle.Normal,
                    color,
                    new Vector2(80 + (i * 205), -60),
                    new Vector2(185, 28),
                    TextAnchor.MiddleCenter
                );
            }
        }

        private void CreateContent()
        {
            if (content != null)
                RetireUiObject(content);

            GameObject scrollRoot = new GameObject("ContentScrollRoot");
            scrollRoot.transform.SetParent(panel.transform, false);

            RectTransform scrollRootRect = scrollRoot.AddComponent<RectTransform>();
            scrollRootRect.anchorMin = Vector2.zero;
            scrollRootRect.anchorMax = Vector2.one;
            scrollRootRect.offsetMin = new Vector2(16f, 58f);
            scrollRootRect.offsetMax = new Vector2(-16f, -98f);

            contentScroll = scrollRoot.AddComponent<ScrollRect>();
            contentScroll.horizontal = false;
            contentScroll.vertical = true;
            contentScroll.movementType = ScrollRect.MovementType.Clamped;
            contentScroll.inertia = true;
            contentScroll.decelerationRate = 0.135f;
            contentScroll.scrollSensitivity = 25f;

            GameObject viewport = new GameObject("ContentViewport");
            viewport.transform.SetParent(scrollRoot.transform, false);

            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = Color.clear;
            viewportImage.raycastTarget = true;
            viewport.AddComponent<RectMask2D>();

            content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);

            RectTransform rect = content.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, GetContentHeight());

            contentScroll.viewport = viewportRect;
            contentScroll.content = rect;
            AlbumUiResources.AttachVanillaListScrollIndicator(
                scrollRoot.transform,
                contentScroll,
                "ContentScrollIndicator"
            );
            contentScroll.verticalNormalizedPosition = 1f;
        }

        private void RefreshUI()
        {
            bool preserveScroll = hasRenderedStep && renderedStep == step;
            float contentScrollPosition = preserveScroll && contentScroll != null
                ? Mathf.Clamp01(contentScroll.verticalNormalizedPosition)
                : 1f;
            float coverScrollPosition = preserveScroll &&
                step == AlbumStep.Cover && coverOptionsScroll != null
                    ? Mathf.Clamp01(coverOptionsScroll.verticalNormalizedPosition)
                    : 1f;
            coverOptionsScroll = null;

            RectTransform contentRect = content != null
                ? content.GetComponent<RectTransform>()
                : null;
            if (contentRect != null)
                contentRect.sizeDelta = new Vector2(0f, GetContentHeight());

            foreach (Transform child in content.transform)
                RetireUiObject(child.gameObject);

            foreach (Transform child in panel.transform)
            {
                if (child.name == "Cancel" ||
                    child.name == "Back" ||
                    child.name == "Continue" ||
                    child.name == "Create")
                {
                    RetireUiObject(child.gameObject);
                }
            }

            // Refresh step header text because the current step changed.
            for (int i = panel.transform.childCount - 1; i >= 0; i--)
            {
                Transform old = panel.transform.GetChild(i);
                if (old.name.StartsWith("Step_", StringComparison.Ordinal))
                    RetireUiObject(old.gameObject);
            }
            DrawStepHeader();

            switch (step)
            {
                case AlbumStep.Info:
                    DrawInfo();
                    break;

                case AlbumStep.Members:
                    DrawMembers();
                    break;

                case AlbumStep.Cover:
                    DrawCoverDesigner();
                    break;

                case AlbumStep.Release:
                    DrawRelease();
                    break;
            }

            DrawButtons();
            Canvas.ForceUpdateCanvases();
            if (contentScroll != null)
            {
                contentScroll.StopMovement();
                contentScroll.verticalNormalizedPosition = contentScrollPosition;
            }
            if (coverOptionsScroll != null)
            {
                coverOptionsScroll.StopMovement();
                coverOptionsScroll.verticalNormalizedPosition = coverScrollPosition;
            }
            renderedStep = step;
            hasRenderedStep = true;
            if (panel != null && panel.transform.parent != null)
            {
                AlbumPopupHost.ApplyLayerRecursively(
                    panel,
                    panel.transform.parent.gameObject.layer
                );
            }
        }

        private float GetContentHeight()
        {
            if (step == AlbumStep.Members)
            {
                int memberCount = 0;
                try
                {
                    List<data_girls.girls> members = data_girls.GetActiveGirls();
                    memberCount = members != null ? members.Count : 0;
                }
                catch
                {
                }

                int rows = Mathf.Max(1, Mathf.CeilToInt(memberCount / 2f));
                return Mathf.Max(470f, 90f + rows * 72f);
            }

            return step == AlbumStep.Cover ? 440f : 470f;
        }

        internal void EnsureContentHeight(float minimumHeight)
        {
            if (content == null)
                return;

            RectTransform rect = content.GetComponent<RectTransform>();
            if (rect != null && rect.sizeDelta.y < minimumHeight)
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, minimumHeight);
        }

        private void DrawButtons()
        {
            CreateButton("Cancel", -360, CancelAlbum, true);

            if (step != AlbumStep.Info)
                CreateButton("Back", 0, Back, false);

            if (step == AlbumStep.Release)
                CreateButton("Create", 360, Save, false);
            else
                CreateButton("Continue", 360, Continue, false);
        }

        private void CancelAlbum()
        {
            AlbumPopupHost.Close(
                AlbumPopupKind.Create,
                delegate
                {
                    panel = null;
                    ResetAlbumCreation();
                }
            );
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(AlbumTitle))
            {
                Debug.LogWarning("[Album] Album needs a title.");
                return;
            }

            if (selectedSongs.Count < 6 || selectedSongs.Count > 10)
            {
                Debug.LogWarning("[Album] Album must contain 6 to 10 songs.");
                return;
            }

            if (selectedGirls.Count == 0)
            {
                Debug.LogWarning("[Album] Album needs at least one member.");
                return;
            }

            AlbumData album = new AlbumData();

            album.ID = GenerateAlbumID();
            album.Title = AlbumTitle.Trim();

            try
            {
                album.GroupName = staticVars.PlayerData.GetGroupName();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Album] Could not get group name: " + ex.Message);
                album.GroupName = "GROUP";
            }

            // Use Idol Manager's in-game date.
            album.ReleaseDate = staticVars.dateTime;
            album.Released = true;
            album.PlayerAlbum = true;

            album.Members = new List<data_girls.girls>(selectedGirls);
            album.Songs = new List<singles._single>(selectedSongs);

            album.Sales = 0L;
            album.WeeklySales = 0L;
            album.Profit = 0L;

            album.ChartPosition = 0;
            album.PreviousChartPosition = 0;
            album.PeakChartPosition = 0;
            album.WeeksOnChart = 0;

            // Preserve all cover settings so this exact cover can be rebuilt
            // later in the discography and album chart.
            album.Theme = themeNames[selectedTheme];
            album.ThemeIndex = selectedTheme;
            album.BackgroundIndex = selectedBackground;
            album.LayoutIndex = selectedLayout;
            album.FontIndex = selectedFont;
            album.TextColorIndex = selectedTextColor;
            album.TitlePosition = titlePosition;
            album.ShowGroupName = showGroupName;
            album.OrnamentStyle = ornamentStyle;
            album.FrameStyle = frameStyle;
            album.TitleEffect = titleEffect;
            album.PortraitScale = portraitScale;
            album.CenterEmphasis = centerEmphasis;
            album.PortraitYOffset = portraitYOffset;
            album.PortraitSpacing = portraitSpacing;
            album.EffectsIntensity = effectsIntensity;

            album.CenterMemberIndex =
                selectedCenterGirl != null
                    ? selectedGirls.IndexOf(selectedCenterGirl)
                    : -1;

            Albums.AddAlbum(album);

            // Give the album its debut-week sales and first chart position.
            AlbumSalesManager.RegisterNewAlbum(album);

            // Sales/chart fields changed after AddAlbum, so save again.
            AlbumPersistence.MarkDirty();

            Debug.Log(
                "[Album] RELEASED: " +
                album.Title +
                " | Group: " +
                album.GroupName +
                " | Songs: " +
                album.Songs.Count +
                " | Members: " +
                album.Members.Count +
                " | Album ID: " +
                album.ID
            );

            AlbumPopupHost.Close(
                AlbumPopupKind.Create,
                delegate
                {
                    panel = null;
                    ResetAlbumCreation();
                }
            );
        }

        private int GenerateAlbumID()
        {
            int highest = 0;

            foreach (AlbumData album in Albums.AlbumList)
            {
                if (album != null && album.ID > highest)
                    highest = album.ID;
            }

            return highest + 1;
        }

        private void DrawInfo()
        {
            CreateText(
                "InfoHeading",
                "Album Information",
                22,
                FontStyle.Bold,
                new Color(0.18f, 0.18f, 0.22f),
                new Vector2(70, -20),
                new Vector2(400, 36),
                TextAnchor.MiddleLeft
            );

            CreateText(
                "AlbumTitleLabel",
                "Album Name",
                13,
                FontStyle.Bold,
                new Color(0.39f, 0.34f, 0.72f),
                new Vector2(120, -78),
                new Vector2(240, 24),
                TextAnchor.MiddleLeft
            );

            GameObject inputRoot;
            albumTitleInput = AlbumUiResources.InstantiateInputField(
                content.transform,
                "AlbumTitleInput",
                AlbumTitle,
                "Enter album title...",
                SetTitle,
                out inputRoot
            );

            if (albumTitleInput != null && inputRoot != null)
            {
                RectTransform ir = inputRoot.GetComponent<RectTransform>();
                ir.anchorMin = new Vector2(0, 1);
                ir.anchorMax = new Vector2(0, 1);
                ir.pivot = new Vector2(0, 1);
                ir.sizeDelta = new Vector2(600, 42);
                ir.anchoredPosition = new Vector2(120, -106);
            }

            CreateText(
                "SongsHeading",
                "Choose Songs",
                16,
                FontStyle.Bold,
                new Color(0.39f, 0.34f, 0.72f),
                new Vector2(120, -170),
                new Vector2(300, 28),
                TextAnchor.MiddleLeft
            );

            CreateText(
                "SongsCount",
                "Selected: " + selectedSongs.Count + " / 10   •   Minimum 6 songs",
                11,
                FontStyle.Bold,
                selectedSongs.Count >= 6
                    ? new Color(0.30f, 0.62f, 0.40f)
                    : new Color(0.65f, 0.42f, 0.42f),
                new Vector2(430, -170),
                new Vector2(360, 28),
                TextAnchor.MiddleRight
            );

            DrawSongSelector();

            CreateText(
                "InfoTip",
                "Choose 6–10 songs for the album. You can only continue after selecting at least 6.",
                11,
                FontStyle.Normal,
                new Color(0.46f, 0.45f, 0.52f),
                new Vector2(120, -420),
                new Vector2(700, 30),
                TextAnchor.MiddleLeft
            );
        }

        private void DrawSongSelector()
        {
            List<singles._single> songs = singles.Singles
                .Where(s => s != null && !string.IsNullOrEmpty(s.title))
                .ToList();

            GameObject scrollRoot = new GameObject("SongSelectorScroll");
            scrollRoot.transform.SetParent(content.transform, false);

            RectTransform scrollRootRect = scrollRoot.AddComponent<RectTransform>();
            scrollRootRect.anchorMin = new Vector2(0, 1);
            scrollRootRect.anchorMax = new Vector2(0, 1);
            scrollRootRect.pivot = new Vector2(0, 1);
            scrollRootRect.sizeDelta = new Vector2(680, 205);
            scrollRootRect.anchoredPosition = new Vector2(120, -203);

            Image bg = scrollRoot.AddComponent<Image>();
            bg.color = new Color(0.985f, 0.985f, 0.992f);

            Outline outl = scrollRoot.AddComponent<Outline>();
            outl.effectColor = new Color(0.82f, 0.82f, 0.88f);
            outl.effectDistance = new Vector2(1, -1);

            ScrollRect scroll = scrollRoot.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollRoot.transform, false);

            RectTransform vr = viewport.AddComponent<RectTransform>();
            vr.anchorMin = Vector2.zero;
            vr.anchorMax = Vector2.one;
            vr.offsetMin = Vector2.zero;
            vr.offsetMax = Vector2.zero;

            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = Color.clear;
            viewportImage.raycastTarget = false;
            viewport.AddComponent<RectMask2D>();

            GameObject listObj = new GameObject("SongList");
            listObj.transform.SetParent(viewport.transform, false);

            RectTransform listRT = listObj.AddComponent<RectTransform>();
            listRT.anchorMin = new Vector2(0, 1);
            listRT.anchorMax = new Vector2(1, 1);
            listRT.pivot = new Vector2(0.5f, 1);

            int rows = Mathf.Max(1, Mathf.CeilToInt(songs.Count / 2f));
            listRT.sizeDelta = new Vector2(-16, rows * 38f + 12f);
            listRT.anchoredPosition = Vector2.zero;

            scroll.viewport = vr;
            scroll.content = listRT;
            AlbumUiResources.AttachVanillaListScrollIndicator(
                scrollRoot.transform,
                scroll,
                "SongSelectorScrollIndicator"
            );

            if (songs.Count == 0)
            {
                CreatePanelText(
                    listObj.transform,
                    "NoSongs",
                    "No singles are available yet.",
                    12,
                    FontStyle.Italic,
                    new Color(0.50f, 0.49f, 0.56f),
                    new Vector2(18, -18),
                    new Vector2(600, 34),
                    TextAnchor.MiddleLeft
                );
                return;
            }

            for (int i = 0; i < songs.Count; i++)
            {
                singles._single song = songs[i];
                bool selected = selectedSongs.Contains(song);

                int row = i / 2;
                int col = i % 2;

                GameObject box = new GameObject("Song_" + i);
                box.transform.SetParent(listObj.transform, false);

                RectTransform br = box.AddComponent<RectTransform>();
                br.anchorMin = new Vector2(0, 1);
                br.anchorMax = new Vector2(0, 1);
                br.pivot = new Vector2(0, 1);
                br.sizeDelta = new Vector2(300, 32);
                br.anchoredPosition = new Vector2(
                    12 + (col * 312),
                    -8 - (row * 38)
                );

                Image boxBg = box.AddComponent<Image>();
                boxBg.color = selected
                    ? new Color(0.95f, 1f, 0.96f)
                    : Color.white;

                Outline boxOutline = box.AddComponent<Outline>();
                boxOutline.effectColor = selected
                    ? new Color(0.35f, 0.72f, 0.47f)
                    : new Color(0.84f, 0.84f, 0.89f);
                boxOutline.effectDistance = new Vector2(1, -1);

                Button button = box.AddComponent<Button>();
                singles._single capturedSong = song;
                button.onClick.AddListener(() => ToggleSong(capturedSong));

                CreatePanelText(
                    box.transform,
                    "SongTitle",
                    song.title,
                    11,
                    selected ? FontStyle.Bold : FontStyle.Normal,
                    selected
                        ? new Color(0.27f, 0.56f, 0.34f)
                        : new Color(0.28f, 0.27f, 0.34f),
                    new Vector2(10, 0),
                        new Vector2(252, 32),
                    TextAnchor.MiddleLeft
                );

                if (selected)
                {
                    CreatePanelText(
                        box.transform,
                        "Check",
                        "✓",
                        13,
                        FontStyle.Bold,
                        new Color(0.28f, 0.64f, 0.38f),
                        new Vector2(268, 0),
                        new Vector2(24, 32),
                        TextAnchor.MiddleCenter
                    );
                }
            }
        }

        private void ToggleSong(singles._single song)
        {
            if (song == null)
                return;

            if (selectedSongs.Contains(song))
            {
                selectedSongs.Remove(song);
            }
            else
            {
                if (selectedSongs.Count >= 10)
                {
                    Debug.Log("[Album] Maximum 10 songs.");
                    return;
                }

                selectedSongs.Add(song);
            }

            RefreshUI();
        }

        private void SetTitle(string value)
        {
            AlbumTitle = value;
            Debug.Log("[Album] Title = " + AlbumTitle);
        }

        private void DrawMembers()
        {
            memberBoxes.Clear();

            List<data_girls.girls> members = data_girls.GetActiveGirls();

            CreateText(
                "MembersHeading",
                "Select Members",
                22,
                FontStyle.Bold,
                new Color(0.18f, 0.18f, 0.22f),
                new Vector2(55, -20),
                new Vector2(400, 40),
                TextAnchor.MiddleLeft
            );

            CreateText(
                "MembersCount",
                "Selected: " + selectedGirls.Count + " / 8",
                13,
                FontStyle.Bold,
                new Color(0.39f, 0.34f, 0.72f),
                new Vector2(620, -20),
                new Vector2(180, 40),
                TextAnchor.MiddleRight
            );

            for (int i = 0; i < members.Count; i++)
            {
                GameObject box = new GameObject("Member_" + i);
                box.transform.SetParent(content.transform, false);

                RectTransform br = box.AddComponent<RectTransform>();
                br.anchorMin = new Vector2(0, 1);
                br.anchorMax = new Vector2(0, 1);
                br.pivot = new Vector2(0, 1);
                br.sizeDelta = new Vector2(320, 62);

                int row = i / 2;
                int col = i % 2;

                br.anchoredPosition = new Vector2(
                    60 + (col * 360),
                    -65 - (row * 72)
                );

                bool selected = selectedMembers.Contains(i);

                Image bg = box.AddComponent<Image>();
                bg.color = selected
                    ? new Color(0.96f, 1f, 0.97f)
                    : Color.white;

                memberBoxes[i] = bg;

                Outline outline = box.AddComponent<Outline>();
                outline.effectColor = selected
                    ? new Color(0.38f, 0.76f, 0.50f)
                    : new Color(0.80f, 0.80f, 0.86f);
                outline.effectDistance = new Vector2(1.2f, -1.2f);

                Button btn = box.AddComponent<Button>();
                int index = i;
                btn.onClick.AddListener(() => SelectMember(index));

                GameObject portrait = new GameObject("Portrait");
                portrait.transform.SetParent(box.transform, false);

                RectTransform pr = portrait.AddComponent<RectTransform>();
                pr.anchorMin = new Vector2(0, 0.5f);
                pr.anchorMax = new Vector2(0, 0.5f);
                pr.pivot = new Vector2(0, 0.5f);
                pr.sizeDelta = new Vector2(58, 58);
                pr.anchoredPosition = new Vector2(8, 0);

                Image portraitImage = portrait.AddComponent<Image>();
                portraitImage.sprite = members[i].texture.middle;
                portraitImage.preserveAspect = true;
                portraitImage.color = Color.white;

                CreatePanelText(
                    box.transform,
                    "Name",
                    members[i].GetName(true),
                    16,
                    selected ? FontStyle.Bold : FontStyle.Normal,
                    selected
                        ? new Color(0.28f, 0.58f, 0.34f)
                        : new Color(0.43f, 0.42f, 0.68f),
                    new Vector2(80, 0),
                    new Vector2(220, 62),
                    TextAnchor.MiddleLeft
                );

                if (selected)
                {
                    CreatePanelText(
                        box.transform,
                        "Check",
                        "✓",
                        18,
                        FontStyle.Bold,
                        new Color(0.28f, 0.64f, 0.38f),
                        new Vector2(285, 0),
                        new Vector2(25, 62),
                        TextAnchor.MiddleCenter
                    );
                }
            }
        }

        private void SelectMember(int index)
        {
            List<data_girls.girls> members = data_girls.GetActiveGirls();

            if (index < 0 || index >= members.Count)
                return;

            if (selectedMembers.Contains(index))
            {
                selectedMembers.Remove(index);

                if (selectedCenterGirl == members[index])
                    selectedCenterGirl = null;

                selectedGirls.Remove(members[index]);
            }
            else
            {
                if (selectedGirls.Count >= 8)
                {
                    Debug.Log("[Album] Maximum 8 members.");
                    return;
                }

                selectedMembers.Add(index);
                selectedGirls.Add(members[index]);
            }

            RefreshUI();
        }

        // ---------------------------------------------------------------------
        // COVER DESIGNER
        // ---------------------------------------------------------------------

        private void DrawCoverDesigner()
        {
            LoadCoverBackgrounds();
            LoadAlbumFonts();

            CreateText(
                "CoverHeading",
                "Multi-Member Album Cover Generator",
                19,
                FontStyle.Bold,
                new Color(0.39f, 0.34f, 0.72f),
                new Vector2(360, -3),
                new Vector2(570, 35),
                TextAnchor.MiddleLeft
            );

            CreateText(
                "CoverTip",
                "TIP: Pick members, a layout, theme, background and title style.",
                11,
                FontStyle.Normal,
                new Color(0.42f, 0.40f, 0.62f),
                new Vector2(360, -35),
                new Vector2(575, 28),
                TextAnchor.MiddleLeft
            );

            Transform scrollContent = CreateCoverOptionsScroll();
            DrawCoverControls(scrollContent);
            DrawLargeCoverPreview();
        }

        private Transform CreateCoverOptionsScroll()
        {
            // Fixed surface: only the masked settings viewport scrolls.
            GameObject scrollRoot = new GameObject("CoverOptionsScroll");
            scrollRoot.transform.SetParent(content.transform, false);

            RectTransform scrollRootRT = scrollRoot.AddComponent<RectTransform>();
            scrollRootRT.anchorMin = new Vector2(0, 1);
            scrollRootRT.anchorMax = new Vector2(0, 1);
            scrollRootRT.pivot = new Vector2(0, 1);
            scrollRootRT.sizeDelta = new Vector2(345, 382);
            scrollRootRT.anchoredPosition = new Vector2(0, -8);

            Image viewportBg = scrollRoot.AddComponent<Image>();
            viewportBg.color = new Color(0.975f, 0.975f, 0.985f);

            Outline viewportOutline = scrollRoot.AddComponent<Outline>();
            viewportOutline.effectColor = new Color(0.82f, 0.82f, 0.88f);
            viewportOutline.effectDistance = new Vector2(1, -1);

            ScrollRect scroll = scrollRoot.AddComponent<ScrollRect>();
            coverOptionsScroll = scroll;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 32f;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollRoot.transform, false);

            RectTransform viewportRT = viewport.AddComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.offsetMin = Vector2.zero;
            viewportRT.offsetMax = Vector2.zero;

            Image viewportMaskImage = viewport.AddComponent<Image>();
            viewportMaskImage.color = Color.clear;
            viewportMaskImage.raycastTarget = false;
            RectMask2D mask = viewport.AddComponent<RectMask2D>();
            mask.padding = Vector4.zero;

            GameObject scrollContent = new GameObject("CoverOptionsContent");
            scrollContent.transform.SetParent(viewport.transform, false);

            RectTransform contentRT = scrollContent.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = new Vector2(1, 1);
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.sizeDelta = new Vector2(0, 690);
            contentRT.anchoredPosition = Vector2.zero;

            scroll.viewport = viewportRT;
            scroll.content = contentRT;

            AlbumUiResources.AttachVanillaListScrollIndicator(
                scrollRoot.transform,
                scroll,
                "CoverOptionsScrollIndicator"
            );

            scroll.verticalNormalizedPosition = 1f;

            return scrollContent.transform;
        }

        private void DrawCoverControls(Transform parent)
        {
            GameObject controls = CreateCardUnder(
                parent,
                "CoverControls",
                new Vector2(0, 0),
                new Vector2(317, 675),
                new Color(0.975f, 0.975f, 0.985f)
            );

            // Album name
            CreatePanelText(
                controls.transform,
                "AlbumNameLabel",
                "Album Name",
                11,
                FontStyle.Bold,
                new Color(0.39f, 0.34f, 0.72f),
                new Vector2(14, -6),
                new Vector2(288, 20),
                TextAnchor.MiddleLeft
            );

            GameObject titleBox = CreateSimpleBox(
                controls.transform,
                "TitleBox",
                new Vector2(14, -28),
                new Vector2(286, 30),
                Color.white
            );

            CreatePanelText(
                titleBox.transform,
                "Title",
                string.IsNullOrWhiteSpace(AlbumTitle) ? "ECLIPSE" : AlbumTitle,
                12,
                FontStyle.Bold,
                new Color(0.16f, 0.16f, 0.20f),
                new Vector2(9, 0),
                new Vector2(264, 30),
                TextAnchor.MiddleLeft
            );

            // Members
            CreatePanelText(
                controls.transform,
                "MembersLabel",
                "Members (" + selectedGirls.Count + "/8)",
                11,
                FontStyle.Bold,
                new Color(0.39f, 0.34f, 0.72f),
                new Vector2(14, -63),
                new Vector2(270, 18),
                TextAnchor.MiddleLeft
            );

            int maxThumbs = Mathf.Min(selectedGirls.Count, 6);

            for (int i = 0; i < maxThumbs; i++)
            {
                GameObject thumb = new GameObject("MemberThumb_" + i);
                thumb.transform.SetParent(controls.transform, false);

                RectTransform tr = thumb.AddComponent<RectTransform>();
                tr.anchorMin = new Vector2(0, 1);
                tr.anchorMax = new Vector2(0, 1);
                tr.pivot = new Vector2(0, 1);
                tr.sizeDelta = new Vector2(34, 34);
                tr.anchoredPosition = new Vector2(14 + (i * 43), -83);

                Image image = thumb.AddComponent<Image>();
                image.sprite = selectedGirls[i].texture.middle;
                image.preserveAspect = true;

                Outline o = thumb.AddComponent<Outline>();
                bool isCenter = selectedCenterGirl == selectedGirls[i];
                o.effectColor = isCenter
                    ? new Color(0.95f, 0.72f, 0.20f)
                    : new Color(0.75f, 0.72f, 0.88f);
                o.effectDistance = isCenter
                    ? new Vector2(2, -2)
                    : new Vector2(1, -1);

                Button centerButton = thumb.AddComponent<Button>();
                int centerIndex = i;
                centerButton.onClick.AddListener(() =>
                {
                    selectedCenterGirl = selectedGirls[centerIndex];
                    RefreshUI();
                });

                if (isCenter)
                {
                    CreatePanelText(
                        thumb.transform,
                        "CenterCrown",
                        "★",
                        9,
                        FontStyle.Bold,
                        new Color(1f, 0.78f, 0.20f),
                        new Vector2(22, 0),
                        new Vector2(12, 12),
                        TextAnchor.MiddleCenter
                    );
                }
            }

            if (selectedGirls.Count > 6)
            {
                CreatePanelText(
                    controls.transform,
                    "MoreMembers",
                    "+" + (selectedGirls.Count - 6),
                    11,
                    FontStyle.Bold,
                    new Color(0.39f, 0.34f, 0.72f),
                    new Vector2(278, -83),
                    new Vector2(34, 34),
                    TextAnchor.MiddleCenter
                );
            }

            // Layout
            CreatePanelText(
                controls.transform,
                "LayoutLabel",
                "Layout",
                11,
                FontStyle.Bold,
                new Color(0.39f, 0.34f, 0.72f),
                new Vector2(14, -121),
                new Vector2(310, 18),
                TextAnchor.MiddleLeft
            );

            string[] layoutNames = { "Center", "Line", "Pyramid", "V-Shape", "Staggered" };

            for (int i = 0; i < 5; i++)
            {
                int index = i;

                CreateChoiceButton(
                    controls.transform,
                    "Layout_" + i,
                    GetLayoutIcon(i),
                    new Vector2(14 + (i * 61), -141),
                    new Vector2(54, 34),
                    i == selectedLayout,
                    () =>
                    {
                        selectedLayout = index;
                        RefreshUI();
                    },
                    10
                );

                CreatePanelText(
                    controls.transform,
                    "LayoutName_" + i,
                    layoutNames[i],
                    8,
                    FontStyle.Normal,
                    new Color(0.25f, 0.25f, 0.30f),
                    new Vector2(12 + (i * 61), -176),
                    new Vector2(58, 15),
                    TextAnchor.MiddleCenter
                );
            }

            // Theme
            CreatePanelText(
                controls.transform,
                "ThemeLabel",
                "Theme",
                11,
                FontStyle.Bold,
                new Color(0.39f, 0.34f, 0.72f),
                new Vector2(14, -194),
                new Vector2(270, 18),
                TextAnchor.MiddleLeft
            );

            for (int i = 0; i < themeNames.Length; i++)
            {
                int index = i;

                CreateChoiceButton(
                    controls.transform,
                    "Theme_" + i,
                    themeNames[i],
                    new Vector2(14 + (i * 61), -214),
                    new Vector2(56, 26),
                    i == selectedTheme,
                    () =>
                    {
                        selectedTheme = index;
                        RefreshUI();
                    },
                    8
                );
            }

            // Background
            CreatePanelText(
                controls.transform,
                "BackgroundLabel",
                "Background",
                11,
                FontStyle.Bold,
                new Color(0.39f, 0.34f, 0.72f),
                new Vector2(14, -247),
                new Vector2(270, 18),
                TextAnchor.MiddleLeft
            );

            int bgCount = Mathf.Min(coverBackgrounds.Count, 6);

            for (int i = 0; i < bgCount; i++)
            {
                int index = i;

                GameObject bgThumb = new GameObject("Background_" + i);
                bgThumb.transform.SetParent(controls.transform, false);

                RectTransform r = bgThumb.AddComponent<RectTransform>();
                r.anchorMin = new Vector2(0, 1);
                r.anchorMax = new Vector2(0, 1);
                r.pivot = new Vector2(0, 1);
                r.sizeDelta = new Vector2(42, 36);
                r.anchoredPosition = new Vector2(14 + (i * 48), -267);

                Image img = bgThumb.AddComponent<Image>();
                img.sprite = coverBackgrounds[i];
                img.preserveAspect = true;

                Outline o = bgThumb.AddComponent<Outline>();
                o.effectColor = i == selectedBackground
                    ? new Color(0.42f, 0.32f, 0.90f)
                    : new Color(0.80f, 0.80f, 0.86f);
                o.effectDistance = i == selectedBackground
                    ? new Vector2(2, -2)
                    : new Vector2(1, -1);

                Button button = bgThumb.AddComponent<Button>();
                button.onClick.AddListener(() =>
                {
                    selectedBackground = index;
                    RefreshUI();
                });
            }

            // Text style
            CreatePanelText(
                controls.transform,
                "TextStyleLabel",
                "Text Style",
                11,
                FontStyle.Bold,
                new Color(0.39f, 0.34f, 0.72f),
                new Vector2(14, -308),
                new Vector2(270, 18),
                TextAnchor.MiddleLeft
            );

            for (int i = 0; i < fontNames.Length; i++)
            {
                int index = i;

                CreateChoiceButton(
                    controls.transform,
                    "Font_" + i,
                    fontNames[i],
                    new Vector2(14 + (i * 76), -328),
                    new Vector2(70, 28),
                    i == selectedFont,
                    () =>
                    {
                        selectedFont = index;
                        RefreshUI();
                    },
                    8
                );
            }

            // Text color
            CreatePanelText(
                controls.transform,
                "TextColorLabel",
                "Text Color",
                11,
                FontStyle.Bold,
                new Color(0.39f, 0.34f, 0.72f),
                new Vector2(14, -365),
                new Vector2(270, 18),
                TextAnchor.MiddleLeft
            );

            for (int i = 0; i < titleColors.Length; i++)
            {
                int index = i;

                GameObject swatch = new GameObject("Color_" + i);
                swatch.transform.SetParent(controls.transform, false);

                RectTransform r = swatch.AddComponent<RectTransform>();
                r.anchorMin = new Vector2(0, 1);
                r.anchorMax = new Vector2(0, 1);
                r.pivot = new Vector2(0, 1);
                r.sizeDelta = new Vector2(31, 28);
                r.anchoredPosition = new Vector2(14 + (i * 41), -386);

                Image img = swatch.AddComponent<Image>();
                img.color = titleColors[i];

                Outline outline = swatch.AddComponent<Outline>();
                outline.effectColor = i == selectedTextColor
                    ? new Color(0.42f, 0.32f, 0.90f)
                    : new Color(0.70f, 0.70f, 0.76f);
                outline.effectDistance = i == selectedTextColor
                    ? new Vector2(2, -2)
                    : new Vector2(1, -1);

                Button button = swatch.AddComponent<Button>();
                button.targetGraphic = img;
                button.onClick.AddListener(() =>
                {
                    selectedTextColor = index;
                    RefreshUI();
                });
            }


            // Advanced adjustments
            CreatePanelText(
                controls.transform,
                "AdjustmentsLabel",
                "Adjustments",
                11,
                FontStyle.Bold,
                new Color(0.39f, 0.34f, 0.72f),
                new Vector2(14, -422),
                new Vector2(280, 18),
                TextAnchor.MiddleLeft
            );

            // Title position
            CreatePanelText(
                controls.transform,
                "TitlePositionLabel",
                "Title Position",
                9,
                FontStyle.Bold,
                new Color(0.32f, 0.31f, 0.40f),
                new Vector2(14, -444),
                new Vector2(90, 18),
                TextAnchor.MiddleLeft
            );

            string[] titlePosLabels = { "Top", "Mid", "Bottom" };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                CreateChoiceButton(
                    controls.transform,
                    "TitlePos_" + i,
                    titlePosLabels[i],
                    new Vector2(14 + (i * 49), -463),
                    new Vector2(44, 22),
                    i == titlePosition,
                    () =>
                    {
                        titlePosition = idx;
                        RefreshUI();
                    },
                    7
                );
            }

            CreateChoiceButton(
                controls.transform,
                "GroupNameToggle",
                showGroupName ? "Group ON" : "Group OFF",
                new Vector2(165, -463),
                new Vector2(66, 22),
                showGroupName,
                () =>
                {
                    showGroupName = !showGroupName;
                    RefreshUI();
                },
                7
            );

            string[] ornaments = { "Crown", "Diamond", "Stars" };
            CreateChoiceButton(
                controls.transform,
                "Ornament",
                ornaments[ornamentStyle],
                new Vector2(236, -463),
                new Vector2(66, 22),
                true,
                () =>
                {
                    ornamentStyle = (ornamentStyle + 1) % ornaments.Length;
                    RefreshUI();
                },
                7
            );

            // Portrait scale
            CreatePanelText(
                controls.transform,
                "PortraitScaleLabel",
                "Portrait Scale",
                8,
                FontStyle.Bold,
                new Color(0.32f, 0.31f, 0.40f),
                new Vector2(14, -493),
                new Vector2(92, 20),
                TextAnchor.MiddleLeft
            );

            CreateChoiceButton(
                controls.transform,
                "PortraitMinus",
                "-",
                new Vector2(110, -493),
                new Vector2(24, 22),
                false,
                () =>
                {
                    portraitScale = Mathf.Clamp(portraitScale - 0.05f, 0.75f, 1.30f);
                    RefreshUI();
                },
                10
            );

            CreatePanelText(
                controls.transform,
                "PortraitScaleText",
                Mathf.RoundToInt(portraitScale * 100f) + "%",
                8,
                FontStyle.Normal,
                new Color(0.35f, 0.35f, 0.42f),
                new Vector2(137, -493),
                new Vector2(42, 22),
                TextAnchor.MiddleCenter
            );

            CreateChoiceButton(
                controls.transform,
                "PortraitPlus",
                "+",
                new Vector2(181, -493),
                new Vector2(24, 22),
                false,
                () =>
                {
                    portraitScale = Mathf.Clamp(portraitScale + 0.05f, 0.75f, 1.30f);
                    RefreshUI();
                },
                10
            );

            // Center emphasis
            CreatePanelText(
                controls.transform,
                "CenterEmphasisLabel",
                "Center",
                8,
                FontStyle.Bold,
                new Color(0.32f, 0.31f, 0.40f),
                new Vector2(213, -493),
                new Vector2(42, 22),
                TextAnchor.MiddleLeft
            );

            CreateChoiceButton(
                controls.transform,
                "CenterMinus",
                "-",
                new Vector2(255, -493),
                new Vector2(20, 22),
                false,
                () =>
                {
                    centerEmphasis = Mathf.Clamp(centerEmphasis - 0.03f, 1.00f, 1.25f);
                    RefreshUI();
                },
                9
            );

            CreateChoiceButton(
                controls.transform,
                "CenterPlus",
                "+",
                new Vector2(279, -493),
                new Vector2(20, 22),
                false,
                () =>
                {
                    centerEmphasis = Mathf.Clamp(centerEmphasis + 0.03f, 1.00f, 1.25f);
                    RefreshUI();
                },
                9
            );

            // Portrait Y
            CreatePanelText(
                controls.transform,
                "PortraitYLabel",
                "Portrait Y",
                8,
                FontStyle.Bold,
                new Color(0.32f, 0.31f, 0.40f),
                new Vector2(14, -523),
                new Vector2(82, 22),
                TextAnchor.MiddleLeft
            );

            CreateChoiceButton(
                controls.transform,
                "PortraitYMinus",
                "-",
                new Vector2(98, -523),
                new Vector2(24, 22),
                false,
                () =>
                {
                    portraitYOffset = Mathf.Clamp(portraitYOffset - 8f, -80f, 80f);
                    RefreshUI();
                },
                9
            );

            CreatePanelText(
                controls.transform,
                "PortraitYText",
                Mathf.RoundToInt(portraitYOffset).ToString(),
                8,
                FontStyle.Normal,
                new Color(0.35f, 0.35f, 0.42f),
                new Vector2(124, -523),
                new Vector2(42, 22),
                TextAnchor.MiddleCenter
            );

            CreateChoiceButton(
                controls.transform,
                "PortraitYPlus",
                "+",
                new Vector2(168, -523),
                new Vector2(24, 22),
                false,
                () =>
                {
                    portraitYOffset = Mathf.Clamp(portraitYOffset + 8f, -80f, 80f);
                    RefreshUI();
                },
                9
            );

            // Spacing
            CreatePanelText(
                controls.transform,
                "SpacingLabel",
                "Spacing",
                8,
                FontStyle.Bold,
                new Color(0.32f, 0.31f, 0.40f),
                new Vector2(205, -523),
                new Vector2(48, 22),
                TextAnchor.MiddleLeft
            );

            CreateChoiceButton(
                controls.transform,
                "SpacingMinus",
                "-",
                new Vector2(253, -523),
                new Vector2(20, 22),
                false,
                () =>
                {
                    portraitSpacing = Mathf.Clamp(portraitSpacing - 0.05f, 0.70f, 1.35f);
                    RefreshUI();
                },
                9
            );

            CreateChoiceButton(
                controls.transform,
                "SpacingPlus",
                "+",
                new Vector2(277, -523),
                new Vector2(20, 22),
                false,
                () =>
                {
                    portraitSpacing = Mathf.Clamp(portraitSpacing + 0.05f, 0.70f, 1.35f);
                    RefreshUI();
                },
                9
            );

            // FX intensity
            CreatePanelText(
                controls.transform,
                "EffectsLabel",
                "Effects",
                8,
                FontStyle.Bold,
                new Color(0.32f, 0.31f, 0.40f),
                new Vector2(14, -553),
                new Vector2(58, 22),
                TextAnchor.MiddleLeft
            );

            CreateChoiceButton(
                controls.transform,
                "EffectsMinus",
                "-",
                new Vector2(74, -553),
                new Vector2(24, 22),
                false,
                () =>
                {
                    effectsIntensity = Mathf.Clamp(effectsIntensity - 0.10f, 0.40f, 1.50f);
                    RefreshUI();
                },
                9
            );

            CreatePanelText(
                controls.transform,
                "EffectsText",
                Mathf.RoundToInt(effectsIntensity * 100f) + "%",
                8,
                FontStyle.Normal,
                new Color(0.35f, 0.35f, 0.42f),
                new Vector2(101, -553),
                new Vector2(44, 22),
                TextAnchor.MiddleCenter
            );

            CreateChoiceButton(
                controls.transform,
                "EffectsPlus",
                "+",
                new Vector2(147, -553),
                new Vector2(24, 22),
                false,
                () =>
                {
                    effectsIntensity = Mathf.Clamp(effectsIntensity + 0.10f, 0.40f, 1.50f);
                    RefreshUI();
                },
                9
            );

            // Frame selector
            string[] frameNames = { "None", "Simple", "Elegant", "Stars", "Neon" };
            CreateChoiceButton(
                controls.transform,
                "FrameSelector",
                "Frame: " + frameNames[frameStyle],
                new Vector2(180, -553),
                new Vector2(118, 22),
                true,
                () =>
                {
                    frameStyle = (frameStyle + 1) % frameNames.Length;
                    RefreshUI();
                },
                7
            );

            // Title effect selector
            string[] titleEffects = { "None", "Shadow", "Outline", "Glow" };
            CreateChoiceButton(
                controls.transform,
                "TitleEffectSelector",
                "Title FX: " + titleEffects[titleEffect],
                new Vector2(14, -583),
                new Vector2(132, 22),
                true,
                () =>
                {
                    titleEffect = (titleEffect + 1) % titleEffects.Length;
                    RefreshUI();
                },
                7
            );

            // Randomize cover
            CreateChoiceButton(
                controls.transform,
                "RandomizeCover",
                "Randomize Cover",
                new Vector2(154, -583),
                new Vector2(144, 22),
                true,
                () =>
                {
                    RandomizeCover();
                    RefreshUI();
                },
                7
            );

            CreatePanelText(
                controls.transform,
                "CenterHint",
                "Tip: click a member portrait above to choose the center idol.",
                8,
                FontStyle.Italic,
                new Color(0.48f, 0.47f, 0.56f),
                new Vector2(14, -613),
                new Vector2(280, 18),
                TextAnchor.MiddleLeft
            );

            CreatePanelText(
                controls.transform,
                "ScrollHint",
                "Mouse wheel to scroll cover options",
                8,
                FontStyle.Italic,
                new Color(0.48f, 0.47f, 0.56f),
                new Vector2(14, -637),
                new Vector2(280, 18),
                TextAnchor.MiddleLeft
            );
        }


        // The controls use callbacks that redraw the preview.
        // A full RefreshUI is not needed here; this keeps the editor feeling responsive.
        private void RefreshCoverControlVisuals()
        {
            // For this first pass, rebuild only when the player changes pages.
            // The preview updates immediately. Selected outlines will be refreshed
            // the next time DrawCoverDesigner() runs.
        }

        private void DrawLargeCoverPreview()
        {
            if (coverPreviewHolder != null)
            {
                RetireUiObject(coverPreviewHolder);
                coverPreviewHolder = null;
            }

            for (int i = content.transform.childCount - 1; i >= 0; i--)
            {
                Transform existing = content.transform.GetChild(i);
                if (existing.name == "CoverPreview")
                    RetireUiObject(existing.gameObject);
            }

            coverPreviewHolder = BuildAlbumCover(
                content.transform,
                "CoverPreview",
                new Vector2(380, -58),
                350f
            );
        }

        // One renderer for every place the album cover appears.
        // Cover Design and Release now use the exact same background,
        // theme, member formation, font, title color and subtitle.
        private GameObject BuildAlbumCover(
            Transform parent,
            string objectName,
            Vector2 anchoredPosition,
            float size
        )
        {
            GameObject holder = new GameObject(objectName);
            holder.transform.SetParent(parent, false);

            RectTransform holderRect = holder.AddComponent<RectTransform>();
            holderRect.anchorMin = new Vector2(0, 1);
            holderRect.anchorMax = new Vector2(0, 1);
            holderRect.pivot = new Vector2(0, 1);
            holderRect.sizeDelta = new Vector2(size, size);
            holderRect.anchoredPosition = anchoredPosition;

            Image holderImage = holder.AddComponent<Image>();
            holderImage.color = GetThemeFallbackColor();

            if (coverBackgrounds.Count > 0)
            {
                selectedBackground = Mathf.Clamp(
                    selectedBackground,
                    0,
                    coverBackgrounds.Count - 1
                );

                holderImage.sprite = coverBackgrounds[selectedBackground];
                holderImage.type = Image.Type.Simple;
                holderImage.preserveAspect = false;
                holderImage.color = Color.white;
            }

            Outline outer = holder.AddComponent<Outline>();
            outer.effectColor = new Color(0.72f, 0.70f, 0.80f);
            outer.effectDistance = new Vector2(2, -2);

            // Keep full portraits inside the album jacket.
            RectMask2D coverMask = holder.AddComponent<RectMask2D>();
            coverMask.padding = Vector4.zero;

            AddThemeOverlay(holder.transform);
            AddThemeEffects(holder.transform, size);
            DrawMembersOnCover(holder.transform, size);
            AddCoverBottomAtmosphere(holder.transform, size);
            AddDecorativeFrame(holder.transform, size);

            string title = string.IsNullOrWhiteSpace(AlbumTitle)
                ? "ECLIPSE"
                : AlbumTitle.ToUpperInvariant();

            GameObject titleObj = new GameObject("CoverAlbumTitle");
            titleObj.transform.SetParent(holder.transform, false);

            RectTransform tr = titleObj.AddComponent<RectTransform>();
            ApplyTitlePosition(tr);

            Text titleText = titleObj.AddComponent<Text>();
            titleText.font = albumFonts != null && selectedFont < albumFonts.Length
                ? albumFonts[selectedFont]
                : AlbumUiResources.GetGameFont();
            titleText.text = title;
            titleText.alignment = TextAnchor.MiddleCenter;
            int autoTitleSize = GetAutoTitleSize(title, size);
            titleText.fontSize = autoTitleSize;
            titleText.resizeTextForBestFit = true;
            titleText.resizeTextMinSize = Mathf.Max(12, Mathf.RoundToInt(size * 0.040f));
            titleText.resizeTextMaxSize = autoTitleSize;
            titleText.color = titleColors[selectedTextColor];
            titleText.fontStyle = selectedFont == 2
                ? FontStyle.Bold
                : FontStyle.Normal;
            titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            titleText.verticalOverflow = VerticalWrapMode.Overflow;
            titleText.raycastTarget = false;

            ApplyThemeTitleStyle(tr, titleText);

            // Small decorative divider/crown above the album title.
            GameObject ornamentObj = new GameObject("CoverOrnament");
            ornamentObj.transform.SetParent(holder.transform, false);

            RectTransform ornamentRect = ornamentObj.AddComponent<RectTransform>();
            ornamentRect.anchorMin = new Vector2(0.22f, 0.255f);
            ornamentRect.anchorMax = new Vector2(0.78f, 0.315f);
            ornamentRect.offsetMin = Vector2.zero;
            ornamentRect.offsetMax = Vector2.zero;

            Text ornamentText = ornamentObj.AddComponent<Text>();
            ornamentText.font = AlbumUiResources.GetGameFont();
            ornamentText.text = GetOrnamentText();
            ornamentText.fontSize = Mathf.Max(10, Mathf.RoundToInt(size * 0.035f));
            ornamentText.alignment = TextAnchor.MiddleCenter;
            ornamentText.color = titleColors[selectedTextColor];
            ornamentText.raycastTarget = false;

            Shadow ornamentShadow = ornamentObj.AddComponent<Shadow>();
            ornamentShadow.effectColor = new Color(0, 0, 0, 0.35f);
            ornamentShadow.effectDistance = new Vector2(1, -1);

            ApplyTitleEffect(titleObj, titleText);

            // Subtitle stays inside the square instead of being placed below it.
            GameObject subtitleObj = new GameObject("CoverSubtitle");
            subtitleObj.transform.SetParent(holder.transform, false);

            RectTransform sr = subtitleObj.AddComponent<RectTransform>();
            sr.anchorMin = new Vector2(0.10f, 0.025f);
            sr.anchorMax = new Vector2(0.90f, 0.085f);
            sr.offsetMin = Vector2.zero;
            sr.offsetMax = Vector2.zero;

            Text subtitleText = subtitleObj.AddComponent<Text>();
            subtitleText.font = AlbumUiResources.GetGameFont();
            subtitleText.text = showGroupName ? GetCoverSubtitle() : "";
            subtitleText.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * 0.03f));
            subtitleText.fontStyle = FontStyle.Normal;
            subtitleText.color = titleColors[selectedTextColor];
            subtitleText.alignment = TextAnchor.MiddleCenter;
            subtitleText.resizeTextForBestFit = true;
            subtitleText.resizeTextMinSize = 7;
            subtitleText.resizeTextMaxSize = Mathf.Max(10, Mathf.RoundToInt(size * 0.04f));
            subtitleText.raycastTarget = false;

            Shadow subtitleShadow = subtitleObj.AddComponent<Shadow>();
            subtitleShadow.effectColor = new Color(0, 0, 0, 0.50f);
            subtitleShadow.effectDistance = new Vector2(1, -1);

            return holder;
        }

        private void RetireUiObject(GameObject obj)
        {
            if (obj == null)
                return;

            obj.SetActive(false);
            StartCoroutine(DestroyAfterUiDelay(obj));
        }

        private IEnumerator DestroyAfterUiDelay(GameObject obj)
        {
            yield return new WaitForSecondsRealtime(0.6f);
            if (obj != null)
                Destroy(obj);
        }

        private void AddCoverBottomAtmosphere(Transform parent, float size)
        {
            Color baseColor;

            switch (selectedTheme)
            {
                case 1:
                    baseColor = new Color(0.05f, 0.02f, 0.09f, 1f);
                    break;
                case 2:
                    baseColor = new Color(0.22f, 0.03f, 0.40f, 1f);
                    break;
                case 3:
                    baseColor = new Color(0.56f, 0.38f, 0.22f, 1f);
                    break;
                case 4:
                    baseColor = new Color(0.92f, 0.92f, 0.94f, 1f);
                    break;
                default:
                    baseColor = new Color(0.74f, 0.58f, 0.94f, 1f);
                    break;
            }

            // Wide soft haze.
            GameObject haze = new GameObject("WideLegHaze");
            haze.transform.SetParent(parent, false);

            RectTransform hr = haze.AddComponent<RectTransform>();
            hr.anchorMin = new Vector2(0f, 0.18f);
            hr.anchorMax = new Vector2(1f, 0.58f);
            hr.offsetMin = Vector2.zero;
            hr.offsetMax = Vector2.zero;

            Image hazeImage = haze.AddComponent<Image>();
            hazeImage.raycastTarget = false;
            hazeImage.sprite = CreateVerticalFadeSprite(
                baseColor,
                0.00f,
                0.28f * effectsIntensity
            );
            hazeImage.type = Image.Type.Simple;

            // Stronger bottom fade that actually hides cutoffs.
            GameObject bottom = new GameObject("StrongLegFade");
            bottom.transform.SetParent(parent, false);

            RectTransform br = bottom.AddComponent<RectTransform>();
            br.anchorMin = new Vector2(0f, 0.08f);
            br.anchorMax = new Vector2(1f, 0.36f);
            br.offsetMin = Vector2.zero;
            br.offsetMax = Vector2.zero;

            Image bottomImage = bottom.AddComponent<Image>();
            bottomImage.raycastTarget = false;
            bottomImage.sprite = CreateVerticalFadeSprite(
                baseColor,
                0.00f,
                0.72f * effectsIntensity
            );
            bottomImage.type = Image.Type.Simple;
        }

        private Sprite CreateVerticalFadeSprite(
            Color color,
            float topAlpha,
            float bottomAlpha
        )
        {
            const int height = 64;

            Texture2D tex = new Texture2D(
                1,
                height,
                TextureFormat.RGBA32,
                false
            );

            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);

                // Smoothstep avoids the obvious "rectangle" transition.
                float smooth = t * t * (3f - (2f * t));
                float alpha = Mathf.Lerp(bottomAlpha, topAlpha, smooth);

                tex.SetPixel(
                    0,
                    y,
                    new Color(color.r, color.g, color.b, alpha)
                );
            }

            tex.Apply();

            return Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f)
            );
        }

        private void ApplyThemeTitleStyle(RectTransform titleRect, Text titleText)
        {
            switch (selectedTheme)
            {
                case 1: // Dark
                    titleText.fontStyle = FontStyle.Bold;
                    break;

                case 2: // Neon
                    titleText.fontStyle = FontStyle.Bold;
                    break;

                case 3: // Vintage
                    titleText.fontStyle = FontStyle.Normal;
                    break;

                case 4: // Minimal
                    titleText.fontStyle = FontStyle.Normal;
                    break;

                default:
                    break;
            }
        }

        private void ApplyTitlePosition(RectTransform titleRect)
        {
            switch (titlePosition)
            {
                case 0: // Top
                    titleRect.anchorMin = new Vector2(0.08f, 0.73f);
                    titleRect.anchorMax = new Vector2(0.92f, 0.92f);
                    break;

                case 1: // Middle
                    titleRect.anchorMin = new Vector2(0.08f, 0.40f);
                    titleRect.anchorMax = new Vector2(0.92f, 0.59f);
                    break;

                default: // Bottom
                    titleRect.anchorMin = new Vector2(0.08f, 0.075f);
                    titleRect.anchorMax = new Vector2(0.92f, 0.255f);
                    break;
            }

            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
        }

        private int GetAutoTitleSize(string title, float size)
        {
            int length = string.IsNullOrEmpty(title) ? 0 : title.Length;
            float ratio = 0.12f;

            if (length >= 20)
                ratio = 0.070f;
            else if (length >= 16)
                ratio = 0.080f;
            else if (length >= 12)
                ratio = 0.092f;
            else if (length >= 8)
                ratio = 0.105f;

            return Mathf.RoundToInt(size * ratio);
        }

        private string GetOrnamentText()
        {
            switch (ornamentStyle)
            {
                case 1:
                    return "──  ◆  ◇  ◆  ──";
                case 2:
                    return "──  ✦  ★  ✦  ──";
                default:
                    return "────  ♛  ────";
            }
        }

        private void ApplyTitleEffect(GameObject titleObj, Text titleText)
        {
            if (titleEffect == 0)
                return;

            if (titleEffect == 1)
            {
                Shadow shadow = titleObj.AddComponent<Shadow>();
                shadow.effectColor = new Color(0, 0, 0, 0.58f);
                shadow.effectDistance = new Vector2(1.5f, -1.5f);
                return;
            }

            Outline outline = titleObj.AddComponent<Outline>();

            if (titleEffect == 2)
            {
                outline.effectColor = new Color(0f, 0f, 0f, 0.68f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
            else
            {
                outline.effectColor = selectedTheme == 2
                    ? new Color(0.82f, 0.38f, 1f, 0.80f)
                    : new Color(1f, 1f, 1f, 0.65f);
                outline.effectDistance = new Vector2(2.5f, -2.5f);

                Shadow glowShadow = titleObj.AddComponent<Shadow>();
                glowShadow.effectColor = selectedTheme == 2
                    ? new Color(0.55f, 0.10f, 1f, 0.45f)
                    : new Color(0.85f, 0.75f, 1f, 0.35f);
                glowShadow.effectDistance = new Vector2(0.5f, -0.5f);
            }
        }

        private void AddThemeOverlay(Transform parent)
        {
            GameObject overlay = new GameObject("ThemeOverlay");
            overlay.transform.SetParent(parent, false);

            RectTransform r = overlay.AddComponent<RectTransform>();
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;

            Image img = overlay.AddComponent<Image>();
            img.raycastTarget = false;

            switch (selectedTheme)
            {
                case 1: // Dark
                    img.color = new Color(0.05f, 0.02f, 0.10f, 0.34f);
                    break;

                case 2: // Neon
                    img.color = new Color(0.28f, 0.00f, 0.42f, 0.22f);
                    break;

                case 3: // Vintage
                    img.color = new Color(0.65f, 0.43f, 0.20f, 0.20f);
                    break;

                case 4: // Minimal
                    img.color = new Color(1.00f, 1.00f, 1.00f, 0.22f);
                    break;

                default: // Dreamy
                    img.color = new Color(0.55f, 0.45f, 1.00f, 0.10f);
                    break;
            }
        }

        private Color GetThemeFallbackColor()
        {
            switch (selectedTheme)
            {
                case 1:
                    return new Color(0.08f, 0.06f, 0.13f);

                case 2:
                    return new Color(0.11f, 0.04f, 0.18f);

                case 3:
                    return new Color(0.77f, 0.68f, 0.56f);

                case 4:
                    return new Color(0.93f, 0.93f, 0.93f);

                default:
                    return new Color(0.72f, 0.63f, 0.90f);
            }
        }

        private string GetCoverSubtitle()
        {
            string groupName = "";

            try
            {
                groupName = staticVars.PlayerData.GetGroupName();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Album] Could not read group name: " + ex.Message);
            }

            if (string.IsNullOrWhiteSpace(groupName))
                groupName = "GROUP";

            return groupName.ToUpperInvariant();
        }

        private void DrawMembersOnCover(Transform parent, float coverSize)
        {
            if (selectedGirls.Count == 0)
                return;

            int count = Mathf.Min(selectedGirls.Count, 8);
            Vector2[] basePositions = GetLayoutPositions(selectedLayout, count);
            float positionScale = coverSize / 350f;

            data_girls_textures textureManager =
                Camera.main.GetComponent<mainScript>()
                    .Data.GetComponent<data_girls_textures>();

            List<int> renderOrder = Enumerable.Range(0, count)
                .OrderByDescending(i => basePositions[i].y)
                .ThenBy(i => Mathf.Abs(basePositions[i].x - 175f))
                .ToList();

            int centerIndex = selectedCenterGirl != null
                ? selectedGirls.IndexOf(selectedCenterGirl)
                : GetCenterMemberIndex(basePositions);

            if (centerIndex < 0 || centerIndex >= count)
                centerIndex = GetCenterMemberIndex(basePositions);

            foreach (int i in renderOrder)
            {
                data_girls.girls girl = selectedGirls[i];

                float depth =
                    Mathf.InverseLerp(225f, 120f, basePositions[i].y);

                float memberScale =
                    count <= 3 ? 1.00f :
                    count <= 5 ? 0.91f :
                    count <= 6 ? 0.82f :
                    0.73f;

                memberScale *= portraitScale;

                if ((selectedLayout == 2 || selectedLayout == 3) && i == centerIndex)
                    memberScale *= centerEmphasis;

                Vector2 pos = basePositions[i];
                pos.y -= 55f;
                pos.y -= depth * 10f;
                pos.y += portraitYOffset;

                // Rim glow sits behind portrait.
                GameObject glow = new GameObject("CoverGlow_" + i);
                glow.transform.SetParent(parent, false);

                RectTransform gr = glow.AddComponent<RectTransform>();
                gr.anchorMin = new Vector2(0, 0);
                gr.anchorMax = new Vector2(0, 0);
                gr.pivot = new Vector2(0.5f, 0f);
                gr.sizeDelta = new Vector2(
                    170f * memberScale * positionScale,
                    285f * memberScale * positionScale
                );
                gr.anchoredPosition = pos * positionScale;

                Image glowImage = glow.AddComponent<Image>();
                glowImage.sprite = CreateSoftGlowSprite();
                glowImage.type = Image.Type.Sliced;
                glowImage.raycastTarget = false;
                glowImage.color = selectedTheme == 2
                    ? new Color(0.72f, 0.25f, 1f, 0.32f * effectsIntensity)
                    : new Color(0.90f, 0.84f, 1f, 0.22f * effectsIntensity);

                GameObject idol = new GameObject("CoverMember_" + i);
                idol.transform.SetParent(parent, false);

                RectTransform r = idol.AddComponent<RectTransform>();
                r.anchorMin = new Vector2(0, 0);
                r.anchorMax = new Vector2(0, 0);
                r.pivot = new Vector2(0.5f, 0f);
                r.sizeDelta = new Vector2(
                    150f * memberScale * positionScale,
                    265f * memberScale * positionScale
                );
                r.anchoredPosition = pos * positionScale;

                Image image = idol.AddComponent<Image>();
                image.preserveAspect = true;
                image.raycastTarget = false;

                float alpha = Mathf.Lerp(0.91f, 1.00f, depth);
                float brightness = Mathf.Lerp(0.93f, 1.00f, depth);
                image.color = new Color(brightness, brightness, brightness, alpha);

                Shadow shadow = idol.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.20f);
                shadow.effectDistance = new Vector2(
                    1.7f * positionScale,
                    -1.7f * positionScale
                );

                textureManager._setFullPortrait(girl, idol);
            }
        }

        private int GetCenterMemberIndex(Vector2[] positions)
        {
            int index = 0;
            float best = float.MaxValue;

            for (int i = 0; i < positions.Length; i++)
            {
                float score =
                    Mathf.Abs(positions[i].x - 175f) +
                    (positions[i].y * 0.05f);

                if (score < best)
                {
                    best = score;
                    index = i;
                }
            }

            return index;
        }

        private Sprite CreateSoftGlowSprite()
        {
            const int size = 32;
            Texture2D tex = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false
            );

            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Vector2 center = new Vector2((size - 1) / 2f, (size - 1) / 2f);
            float maxDist = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center.x;
                    float dy = y - center.y;
                    float d = Mathf.Sqrt((dx * dx) + (dy * dy)) / maxDist;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            tex.Apply();

            return Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(8, 8, 8, 8)
            );
        }

        private string GetLayoutIcon(int index)
        {
            switch (index)
            {
                case 0:
                    return "●";
                case 1:
                    return "● ●";
                case 2:
                    return "●\n● ●";
                case 3:
                    return "● ●\n● ●";
                default:
                    return "● ●\n ● \n● ●";
            }
        }

        private Vector2[] GetLayoutPositions(int layoutIndex, int count)
        {
            // All presets show every selected member. They only change formation.
            Vector2[] positions = new Vector2[count];

            switch (Mathf.Clamp(layoutIndex, 0, 4))
            {
                // 1 - Centered / hero formation
                case 0:
                    for (int i = 0; i < count; i++)
                    {
                        float x = 175f + ((i - (count - 1) / 2f) * 48f);
                        float y = 175f + (Mathf.Abs(i - (count - 1) / 2f) * 8f);
                        positions[i] = new Vector2(x, y);
                    }
                    break;

                // 2 - Horizontal lineup
                case 1:
                    for (int i = 0; i < count; i++)
                    {
                        float spacing = count <= 5 ? 55f : 42f;
                        float x = 175f + ((i - (count - 1) / 2f) * spacing);
                        positions[i] = new Vector2(x, 178f);
                    }
                    break;

                // 3 - Pyramid / 3 back, rest front
                case 2:
                    {
                        int backCount = Mathf.CeilToInt(count / 2f);
                        int frontCount = count - backCount;

                        for (int i = 0; i < backCount; i++)
                        {
                            float spacing = backCount <= 1 ? 0f : 72f;
                            float x = 175f + ((i - (backCount - 1) / 2f) * spacing);
                            positions[i] = new Vector2(x, 215f);
                        }

                        for (int i = 0; i < frontCount; i++)
                        {
                            float spacing = frontCount <= 1 ? 0f : 74f;
                            float x = 175f + ((i - (frontCount - 1) / 2f) * spacing);
                            positions[backCount + i] = new Vector2(x, 135f);
                        }
                    }
                    break;

                // 4 - V formation
                case 3:
                    if (count == 1)
                    {
                        positions[0] = new Vector2(175f, 155f);
                    }
                    else if (count == 2)
                    {
                        positions[0] = new Vector2(120f, 180f);
                        positions[1] = new Vector2(230f, 180f);
                    }
                    else if (count == 3)
                    {
                        positions[0] = new Vector2(95f, 205f);
                        positions[1] = new Vector2(175f, 145f);
                        positions[2] = new Vector2(255f, 205f);
                    }
                    else if (count == 4)
                    {
                        positions[0] = new Vector2(75f, 210f);
                        positions[1] = new Vector2(135f, 170f);
                        positions[2] = new Vector2(215f, 170f);
                        positions[3] = new Vector2(275f, 210f);
                    }
                    else
                    {
                        // Outer members are back/high, inner members are mid,
                        // center member is clearly in front.
                        positions[0] = new Vector2(62f, 218f);
                        positions[1] = new Vector2(118f, 178f);
                        positions[2] = new Vector2(175f, 135f);
                        positions[3] = new Vector2(232f, 178f);
                        positions[4] = new Vector2(288f, 218f);

                        if (count >= 6)
                            positions[5] = new Vector2(175f, 190f);

                        if (count >= 7)
                            positions[6] = new Vector2(92f, 145f);

                        if (count >= 8)
                            positions[7] = new Vector2(258f, 145f);
                    }
                    break;

                // 5 - Staggered idol-poster formation
                default:
                    {
                        for (int i = 0; i < count; i++)
                        {
                            int row = i % 2;
                            int column = i / 2;

                            float xBase = row == 0 ? 105f : 135f;
                            float x = xBase + (column * 65f);
                            float y = row == 0 ? 205f : 125f;

                            positions[i] = new Vector2(x, y);
                        }
                    }
                    break;
            }

            for (int i = 0; i < positions.Length; i++)
            {
                positions[i].x =
                    175f + ((positions[i].x - 175f) * portraitSpacing);
            }

            return positions;
        }

        private void AddThemeEffects(Transform parent, float size)
        {
            switch (selectedTheme)
            {
                case 0:
                    AddDreamyEffects(parent, size);
                    break;
                case 1:
                    AddDarkEffects(parent, size);
                    break;
                case 2:
                    AddNeonEffects(parent, size);
                    break;
                case 3:
                    AddVintageEffects(parent, size);
                    break;
                case 4:
                    AddMinimalEffects(parent, size);
                    break;
            }
        }

        private void AddDreamyEffects(Transform parent, float size)
        {
            AddSimpleParticleField(
                parent,
                "Petal",
                12,
                new Color(1f, 0.72f, 0.90f, 0.35f * effectsIntensity),
                size,
                6f,
                14f
            );

            AddSimpleParticleField(
                parent,
                "Sparkle",
                18,
                new Color(1f, 1f, 1f, 0.28f * effectsIntensity),
                size,
                2f,
                5f
            );
        }

        private void AddDarkEffects(Transform parent, float size)
        {
            AddVignette(parent, new Color(0f, 0f, 0f, 0.34f * effectsIntensity));
            AddSimpleParticleField(
                parent,
                "Smoke",
                8,
                new Color(0.15f, 0.12f, 0.20f, 0.12f * effectsIntensity),
                size,
                18f,
                35f
            );
        }

        private void AddNeonEffects(Transform parent, float size)
        {
            AddSimpleParticleField(
                parent,
                "NeonParticle",
                22,
                new Color(0.82f, 0.35f, 1f, 0.32f * effectsIntensity),
                size,
                2f,
                7f
            );

            for (int i = 0; i < 5; i++)
            {
                GameObject streak = new GameObject("LightStreak_" + i);
                streak.transform.SetParent(parent, false);

                RectTransform r = streak.AddComponent<RectTransform>();
                r.anchorMin = new Vector2(0.08f + i * 0.17f, 0.58f - i * 0.07f);
                r.anchorMax = new Vector2(0.28f + i * 0.17f, 0.60f - i * 0.07f);
                r.offsetMin = Vector2.zero;
                r.offsetMax = Vector2.zero;
                r.localRotation = Quaternion.Euler(0, 0, 18f);

                Image img = streak.AddComponent<Image>();
                img.raycastTarget = false;
                img.color = new Color(
                    0.70f,
                    0.25f,
                    1f,
                    0.16f * effectsIntensity
                );
            }
        }

        private void AddVintageEffects(Transform parent, float size)
        {
            GameObject warm = new GameObject("WarmFade");
            warm.transform.SetParent(parent, false);

            RectTransform r = warm.AddComponent<RectTransform>();
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;

            Image img = warm.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = new Color(
                0.52f,
                0.30f,
                0.12f,
                0.12f * effectsIntensity
            );

            AddSimpleParticleField(
                parent,
                "Grain",
                35,
                new Color(0.20f, 0.15f, 0.10f, 0.10f * effectsIntensity),
                size,
                1f,
                2f
            );
        }

        private void AddMinimalEffects(Transform parent, float size)
        {
            for (int i = 0; i < 3; i++)
            {
                GameObject line = new GameObject("GeoLine_" + i);
                line.transform.SetParent(parent, false);

                RectTransform r = line.AddComponent<RectTransform>();
                r.anchorMin = new Vector2(0.12f, 0.72f - i * 0.14f);
                r.anchorMax = new Vector2(0.88f, 0.725f - i * 0.14f);
                r.offsetMin = Vector2.zero;
                r.offsetMax = Vector2.zero;

                Image img = line.AddComponent<Image>();
                img.raycastTarget = false;
                img.color = new Color(1f, 1f, 1f, 0.16f * effectsIntensity);
            }
        }

        private void AddSimpleParticleField(
            Transform parent,
            string prefix,
            int count,
            Color color,
            float size,
            float minSize,
            float maxSize
        )
        {
            System.Random rng = new System.Random(
                (selectedTheme + 1) * 113 +
                selectedBackground * 17 +
                prefix.GetHashCode()
            );

            for (int i = 0; i < count; i++)
            {
                GameObject p = new GameObject(prefix + "_" + i);
                p.transform.SetParent(parent, false);

                RectTransform r = p.AddComponent<RectTransform>();

                float x = (float)rng.NextDouble();
                float y = (float)rng.NextDouble();
                float s = Mathf.Lerp(
                    minSize,
                    maxSize,
                    (float)rng.NextDouble()
                );

                r.anchorMin = new Vector2(x, y);
                r.anchorMax = new Vector2(x, y);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(s, s * 0.65f);
                r.anchoredPosition = Vector2.zero;
                r.localRotation = Quaternion.Euler(
                    0,
                    0,
                    Mathf.Lerp(-40f, 40f, (float)rng.NextDouble())
                );

                Image img = p.AddComponent<Image>();
                img.raycastTarget = false;
                img.color = color;
            }
        }

        private void AddVignette(Transform parent, Color color)
        {
            // Four soft edge bands create a lightweight vignette.
            Vector2[] mins =
            {
                new Vector2(0f, 0f),
                new Vector2(0.84f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0.84f)
            };

            Vector2[] maxs =
            {
                new Vector2(0.16f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0.16f),
                new Vector2(1f, 1f)
            };

            for (int i = 0; i < 4; i++)
            {
                GameObject edge = new GameObject("Vignette_" + i);
                edge.transform.SetParent(parent, false);

                RectTransform r = edge.AddComponent<RectTransform>();
                r.anchorMin = mins[i];
                r.anchorMax = maxs[i];
                r.offsetMin = Vector2.zero;
                r.offsetMax = Vector2.zero;

                Image img = edge.AddComponent<Image>();
                img.raycastTarget = false;
                img.color = color;
            }
        }

        private void AddDecorativeFrame(Transform parent, float size)
        {
            if (frameStyle == 0)
                return;

            Color frameColor;

            switch (selectedTheme)
            {
                case 1:
                    frameColor = new Color(0.82f, 0.82f, 0.88f, 0.72f);
                    break;
                case 2:
                    frameColor = new Color(0.92f, 0.48f, 1f, 0.82f);
                    break;
                case 3:
                    frameColor = new Color(0.88f, 0.72f, 0.42f, 0.78f);
                    break;
                case 4:
                    frameColor = new Color(0.85f, 0.85f, 0.90f, 0.62f);
                    break;
                default:
                    frameColor = new Color(1f, 0.78f, 0.96f, 0.72f);
                    break;
            }

            float thickness = Mathf.Max(
                1f,
                size * (frameStyle == 4 ? 0.009f : 0.005f)
            );

            AddFrameLine(parent, "FrameTop",
                new Vector2(0.035f, 0.955f),
                new Vector2(0.965f, 0.955f),
                thickness,
                frameColor);

            AddFrameLine(parent, "FrameBottom",
                new Vector2(0.035f, 0.045f),
                new Vector2(0.965f, 0.045f),
                thickness,
                frameColor);

            AddFrameLine(parent, "FrameLeft",
                new Vector2(0.035f, 0.045f),
                new Vector2(0.035f, 0.955f),
                thickness,
                frameColor);

            AddFrameLine(parent, "FrameRight",
                new Vector2(0.965f, 0.045f),
                new Vector2(0.965f, 0.955f),
                thickness,
                frameColor);

            string corner =
                frameStyle == 2 ? "◆" :
                frameStyle == 3 ? "★" :
                frameStyle == 4 ? "✦" :
                "";
            Vector2[] corners =
            {
                new Vector2(0.055f, 0.935f),
                new Vector2(0.945f, 0.935f),
                new Vector2(0.055f, 0.065f),
                new Vector2(0.945f, 0.065f)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                GameObject obj = new GameObject("FrameCorner_" + i);
                obj.transform.SetParent(parent, false);

                RectTransform r = obj.AddComponent<RectTransform>();
                r.anchorMin = corners[i];
                r.anchorMax = corners[i];
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(24, 24);
                r.anchoredPosition = Vector2.zero;

                Text t = obj.AddComponent<Text>();
                t.font = AlbumUiResources.GetGameFont();
                t.text = corner;
                t.fontSize = Mathf.RoundToInt(size * 0.035f);
                t.alignment = TextAnchor.MiddleCenter;
                t.color = frameColor;
                t.raycastTarget = false;
            }
        }

        private void AddFrameLine(
            Transform parent,
            string name,
            Vector2 start,
            Vector2 end,
            float thickness,
            Color color
        )
        {
            GameObject line = new GameObject(name);
            line.transform.SetParent(parent, false);

            RectTransform r = line.AddComponent<RectTransform>();

            if (Mathf.Abs(start.y - end.y) < 0.001f)
            {
                r.anchorMin = new Vector2(start.x, start.y);
                r.anchorMax = new Vector2(end.x, end.y);
                r.sizeDelta = new Vector2(0, thickness);
            }
            else
            {
                r.anchorMin = new Vector2(start.x, start.y);
                r.anchorMax = new Vector2(end.x, end.y);
                r.sizeDelta = new Vector2(thickness, 0);
            }

            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;

            Image img = line.AddComponent<Image>();
            img.raycastTarget = false;
            img.color = color;
        }

        private void RandomizeCover()
        {
            selectedTheme = UnityEngine.Random.Range(0, themeNames.Length);

            if (coverBackgrounds.Count > 0)
                selectedBackground = UnityEngine.Random.Range(0, coverBackgrounds.Count);

            selectedLayout = UnityEngine.Random.Range(0, 5);
            selectedFont = UnityEngine.Random.Range(0, fontNames.Length);
            selectedTextColor = UnityEngine.Random.Range(0, titleColors.Length);
            titlePosition = UnityEngine.Random.Range(0, 3);
            ornamentStyle = UnityEngine.Random.Range(0, 3);

            portraitScale = UnityEngine.Random.Range(0.90f, 1.15f);
            centerEmphasis = UnityEngine.Random.Range(1.03f, 1.12f);
            effectsIntensity = UnityEngine.Random.Range(0.80f, 1.25f);
            portraitYOffset = UnityEngine.Random.Range(-24f, 24f);
            portraitSpacing = UnityEngine.Random.Range(0.85f, 1.15f);
            frameStyle = UnityEngine.Random.Range(0, 5);
            titleEffect = UnityEngine.Random.Range(0, 4);
        }

        // ---------------------------------------------------------------------
        // RELEASE SUMMARY
        // ---------------------------------------------------------------------

        private void DrawRelease()
        {
            LoadCoverBackgrounds();
            LoadAlbumFonts();

            CreateText(
                "ReleaseHeading",
                "Release Album",
                24,
                FontStyle.Bold,
                new Color(0.39f, 0.34f, 0.72f),
                new Vector2(50, -15),
                new Vector2(420, 40),
                TextAnchor.MiddleLeft
            );

            // Reuse the designed cover.
            DrawReleaseCoverPreview();

            GameObject summary = CreateCard(
                "ReleaseSummary",
                new Vector2(390, -64),
                new Vector2(410, 360),
                Color.white
            );

            string title = string.IsNullOrWhiteSpace(AlbumTitle)
                ? "Untitled Album"
                : AlbumTitle;

            CreatePanelText(
                summary.transform,
                "AlbumTitle",
                title,
                27,
                FontStyle.Bold,
                new Color(0.18f, 0.18f, 0.22f),
                new Vector2(20, -20),
                new Vector2(365, 42),
                TextAnchor.MiddleLeft
            );

            CreatePanelText(
                summary.transform,
                "Theme",
                themeNames[selectedTheme] + " Cover",
                13,
                FontStyle.Normal,
                new Color(0.47f, 0.42f, 0.70f),
                new Vector2(20, -60),
                new Vector2(365, 28),
                TextAnchor.MiddleLeft
            );

            AddSummaryRow(summary.transform, 100, "Members", selectedGirls.Count.ToString());
            AddSummaryRow(summary.transform, 140, "Songs", selectedSongs.Count.ToString());
            AddSummaryRow(summary.transform, 180, "Cover Theme", themeNames[selectedTheme]);
            AddSummaryRow(summary.transform, 220, "Text Style", fontNames[selectedFont]);
            AddSummaryRow(
                summary.transform,
                260,
                "Background",
                coverBackgrounds.Count > 0
                    ? "Background " + (selectedBackground + 1)
                    : "None"
            );

            CreatePanelText(
                summary.transform,
                "Status",
                "Ready to create album",
                13,
                FontStyle.Bold,
                new Color(0.28f, 0.62f, 0.38f),
                new Vector2(20, -315),
                new Vector2(365, 30),
                TextAnchor.MiddleLeft
            );
        }

        private void DrawReleaseCoverPreview()
        {
            BuildAlbumCover(
                content.transform,
                "ReleaseCover",
                new Vector2(50, -64),
                310f
            );
        }

        private void AddSummaryRow(
            Transform parent,
            float y,
            string label,
            string value
        )
        {
            CreatePanelText(
                parent,
                label + "_Label",
                label,
                13,
                FontStyle.Bold,
                new Color(0.35f, 0.34f, 0.40f),
                new Vector2(20, -y),
                new Vector2(170, 30),
                TextAnchor.MiddleLeft
            );

            CreatePanelText(
                parent,
                label + "_Value",
                value,
                13,
                FontStyle.Normal,
                new Color(0.39f, 0.34f, 0.72f),
                new Vector2(205, -y),
                new Vector2(175, 30),
                TextAnchor.MiddleRight
            );

            GameObject line = new GameObject(label + "_Divider");
            line.transform.SetParent(parent, false);

            RectTransform lr = line.AddComponent<RectTransform>();
            lr.anchorMin = new Vector2(0, 1);
            lr.anchorMax = new Vector2(0, 1);
            lr.pivot = new Vector2(0, 1);
            lr.sizeDelta = new Vector2(360, 1);
            lr.anchoredPosition = new Vector2(20, -(y + 32));

            Image li = line.AddComponent<Image>();
            li.color = new Color(0.90f, 0.90f, 0.93f);
        }

        // ---------------------------------------------------------------------
        // ASSET LOADING
        // ---------------------------------------------------------------------

        private void LoadCoverBackgrounds()
        {
            coverBackgrounds.Clear();

            string folder = AlbumPaths.BackgroundsDirectory;

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
                return;
            }

            foreach (string file in Directory.GetFiles(folder, "*.png"))
            {
                Texture2D tex = new Texture2D(2, 2);

                if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(file)))
                    continue;

                Sprite sprite = Sprite.Create(
                    tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f)
                );

                coverBackgrounds.Add(sprite);
            }
        }

        private void LoadAlbumFonts()
        {
            if (albumFonts != null && albumFonts.Length == 4)
                return;

            albumFonts = new Font[4];

            string[] names =
            {
                "Georgia",
                "Times New Roman",
                "Arial Black",
                "Segoe Script"
            };

            for (int i = 0; i < names.Length; i++)
            {
                Font font = null;

                try
                {
                    font = Font.CreateDynamicFontFromOSFont(
                        names[i],
                        32
                    );
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        "[Album] Could not load font " +
                        names[i] +
                        ": " +
                        ex.Message
                    );
                }

                if (font == null)
                {
                    font = AlbumUiResources.GetGameFont();
                }

                albumFonts[i] = font;
            }
        }

        // ---------------------------------------------------------------------
        // NAVIGATION
        // ---------------------------------------------------------------------

        public void Continue()
        {
            if (step == AlbumStep.Info &&
                string.IsNullOrWhiteSpace(AlbumTitle))
            {
                Debug.Log("[Album] Enter an album title first.");
                return;
            }

            if (step == AlbumStep.Info &&
                selectedSongs.Count < 6)
            {
                Debug.Log("[Album] Select at least 6 songs.");
                return;
            }

            if (step == AlbumStep.Members &&
                selectedGirls.Count == 0)
            {
                Debug.Log("[Album] Pick members first.");
                return;
            }

            if (step < AlbumStep.Release)
            {
                step++;
                RefreshUI();
            }
        }

        public void Back()
        {
            if (step > AlbumStep.Info)
            {
                step--;
                RefreshUI();
            }
        }

        // ---------------------------------------------------------------------
        // UI HELPERS
        // ---------------------------------------------------------------------

        private GameObject CreateCardUnder(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            Color color
        )
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            RectTransform r = obj.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(0, 1);
            r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.sizeDelta = size;
            r.anchoredPosition = position;

            Image image = obj.AddComponent<Image>();
            image.color = color;

            return obj;
        }

        private GameObject CreateCard(
            string name,
            Vector2 position,
            Vector2 size,
            Color color
        )
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(content.transform, false);

            RectTransform r = obj.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(0, 1);
            r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.sizeDelta = size;
            r.anchoredPosition = position;

            Image image = obj.AddComponent<Image>();
            image.color = color;

            Outline outline = obj.AddComponent<Outline>();
            outline.effectColor = new Color(0.82f, 0.82f, 0.88f);
            outline.effectDistance = new Vector2(1, -1);

            return obj;
        }

        private GameObject CreateSimpleBox(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            Color color
        )
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            RectTransform r = obj.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(0, 1);
            r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.sizeDelta = size;
            r.anchoredPosition = position;

            Image image = obj.AddComponent<Image>();
            image.color = color;

            Outline outline = obj.AddComponent<Outline>();
            outline.effectColor = new Color(0.82f, 0.82f, 0.88f);
            outline.effectDistance = new Vector2(1, -1);

            return obj;
        }

        private void CreateChoiceButton(
            Transform parent,
            string name,
            string label,
            Vector2 position,
            Vector2 size,
            bool selected,
            UnityEngine.Events.UnityAction click,
            int fontSize = 11
        )
        {
            GameObject obj = AlbumUiResources.InstantiateButton(
                parent,
                name,
                label,
                !selected,
                click
            );
            if (obj == null)
                return;

            RectTransform r = obj.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0, 1);
            r.anchorMax = new Vector2(0, 1);
            r.pivot = new Vector2(0, 1);
            r.sizeDelta = size;
            r.anchoredPosition = position;
            AlbumUiResources.SetButtonFontSize(obj, fontSize);
        }

        private void CreateText(
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
            obj.transform.SetParent(content.transform, false);

            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Text txt = obj.AddComponent<Text>();
            txt.font = AlbumUiResources.GetGameFont();
            txt.text = value;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = anchor;
            txt.raycastTarget = false;
        }

        private void CreatePanelText(
            Transform parent,
            string name,
            string value,
            int fontSize,
            FontStyle style,
            Color color,
            Vector2 position,
            Vector2 size,
            TextAnchor anchor,
            Font customFont = null
        )
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Text txt = obj.AddComponent<Text>();
            txt.font = customFont != null
                ? customFont
                : AlbumUiResources.GetGameFont();
            txt.text = value;
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.color = color;
            txt.alignment = anchor;
            txt.raycastTarget = false;
        }

        private void CreateButton(
            string text,
            float x,
            UnityEngine.Events.UnityAction click,
            bool cancel
        )
        {
            GameObject obj = AlbumUiResources.InstantiateButton(
                panel.transform,
                text,
                text,
                cancel
                    ? AlbumButtonStyle.Destructive
                    : (text == "Continue" || text == "Create")
                        ? AlbumButtonStyle.Confirm
                        : text == "Back"
                            ? AlbumButtonStyle.Back
                            : AlbumButtonStyle.Standard,
                click
            );
            if (obj == null)
                return;

            obj.transform.SetAsLastSibling();
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(160, 32);
            rect.anchoredPosition = new Vector2(x, 10);
        }

        private void ResetAlbumCreation()
        {
            step = AlbumStep.Info;
            coverOptionsScroll = null;
            renderedStep = AlbumStep.Info;
            hasRenderedStep = false;

            AlbumTitle = "";
            albumTitleInput = null;

            selectedMembers.Clear();
            selectedGirls.Clear();
            selectedSongs.Clear();
            memberBoxes.Clear();

            selectedFont = 0;

            selectedTheme = 0;
            selectedBackground = 0;
            selectedLayout = 2;
            selectedTextColor = 0;

            titlePosition = 2;
            showGroupName = true;
            ornamentStyle = 0;

            portraitScale = 1.00f;
            centerEmphasis = 1.08f;
            effectsIntensity = 1.00f;

            selectedCenterGirl = null;
            portraitYOffset = 0f;
            portraitSpacing = 1.00f;

            frameStyle = 1;
            titleEffect = 1;

            coverPreviewHolder = null;
        }
    }
}
