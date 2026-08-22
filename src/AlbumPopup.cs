using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CreateAnAlbumGroupRules;

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
        private ScrollRect memberPickerScroll;
        private int coverScrollRestoreGeneration;
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

        private string[] fontNames = new string[0];

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
        private string selectedBackgroundKey = "";
        private float backgroundScrollPosition = 0f;
        private float memberScrollPosition = 0f;
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
                    delegate { GroupAlbumRules.ResetPopupState(this); }
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
            if (preserveScroll && step == AlbumStep.Cover && memberPickerScroll != null)
            {
                memberScrollPosition = Mathf.Clamp01(
                    memberPickerScroll.horizontalNormalizedPosition);
            }
            coverOptionsScroll = null;
            memberPickerScroll = null;

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

            // Footer navigation is created before step content so a preview/font/layout
            // exception can never strand the player on Cover Design without Back/Continue.
            DrawButtons();

            try
            {
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
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[CreateAlbum/UI] Failed to render " + step +
                    " step content; footer navigation was kept available.\n" + ex);
            }

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

        private void RefreshCoverDesignerPreservingScroll()
        {
            float outerPosition = contentScroll != null
                ? Mathf.Clamp01(contentScroll.verticalNormalizedPosition)
                : 1f;
            float optionsPosition = coverOptionsScroll != null
                ? Mathf.Clamp01(coverOptionsScroll.verticalNormalizedPosition)
                : 1f;
            float backgroundPosition = Mathf.Clamp01(backgroundScrollPosition);
            float memberPosition = memberPickerScroll != null
                ? Mathf.Clamp01(memberPickerScroll.horizontalNormalizedPosition)
                : Mathf.Clamp01(memberScrollPosition);

            int generation = ++coverScrollRestoreGeneration;
            RefreshUI();

            // Unity may recalculate nested ScrollRect bounds one or two frames after the
            // controls are rebuilt. Reapply the captured positions after those layout passes
            // so clicking a choice never snaps any part of the cover editor back to an edge.
            StartCoroutine(RestoreCoverScrollAfterRefresh(
                generation, outerPosition, optionsPosition, backgroundPosition, memberPosition));
        }

        private IEnumerator RestoreCoverScrollAfterRefresh(
            int generation,
            float outerPosition,
            float optionsPosition,
            float backgroundPosition,
            float memberPosition)
        {
            for (int frame = 0; frame < 3; frame++)
            {
                if (generation != coverScrollRestoreGeneration || step != AlbumStep.Cover)
                    yield break;

                yield return null;
                Canvas.ForceUpdateCanvases();

                if (contentScroll != null)
                {
                    contentScroll.StopMovement();
                    contentScroll.verticalNormalizedPosition = outerPosition;
                }

                if (coverOptionsScroll != null)
                {
                    coverOptionsScroll.StopMovement();
                    coverOptionsScroll.verticalNormalizedPosition = optionsPosition;
                }

                backgroundScrollPosition = backgroundPosition;
                memberScrollPosition = memberPosition;

                if (memberPickerScroll != null)
                {
                    memberPickerScroll.StopMovement();
                    memberPickerScroll.horizontalNormalizedPosition = memberPosition;
                }
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
                delegate { GroupAlbumRules.ResetPopupState(this); }
            );
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(AlbumTitle))
            {
                Debug.LogWarning("[Album] Album needs a title.");
                return;
            }

            int minimumSongs = GroupAlbumRules.GetMinimumSongs(this);
            int maximumSongs = GroupAlbumRules.GetMaximumSongs(this);
            if (selectedSongs.Count < minimumSongs || selectedSongs.Count > maximumSongs)
            {
                Debug.LogWarning(
                    "[Album] This release type requires " + minimumSongs +
                    " to " + maximumSongs + " songs.");
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
            AlbumMemberRepair.CaptureAndGetSnapshots(album);
            album.Songs = new List<singles._single>(selectedSongs);

            album.Sales = 0L;
            album.WeeklySales = 0L;
            album.Profit = 0L;

            album.ChartPosition = 0;
            album.PreviousChartPosition = 0;
            album.PeakChartPosition = 0;
            album.WeeksOnChart = 0;

            // Preserve every cover setting through the same mapper used by the live
            // preview. This keeps the editor and persisted renderer on one data contract.
            ApplyCurrentCoverSettings(album);

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
                delegate { GroupAlbumRules.ResetPopupState(this); }
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
                new Vector2(120, -148),
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
                ir.anchoredPosition = new Vector2(120, -174);
            }

            CreateText(
                "SongsHeading",
                "Choose Songs",
                16,
                FontStyle.Bold,
                new Color(0.39f, 0.34f, 0.72f),
                new Vector2(120, -232),
                new Vector2(300, 28),
                TextAnchor.MiddleLeft
            );

            AlbumReleaseKind releaseKind =
                CreateAnAlbumGroupRules.GroupAlbumRules.GetSelectedReleaseKind(this);
            int minimumSongs = AlbumReleaseRules.GetMinimumSongs(releaseKind);
            int maximumSongs = AlbumReleaseRules.GetMaximumSongs(releaseKind);
            bool validSongCount = selectedSongs.Count >= minimumSongs &&
                selectedSongs.Count <= maximumSongs;

            CreateText(
                "SongsCount",
                "Selected: " + selectedSongs.Count + " / " + maximumSongs +
                "   •   Minimum " + minimumSongs + " songs",
                11,
                FontStyle.Bold,
                validSongCount
                    ? new Color(0.30f, 0.62f, 0.40f)
                    : new Color(0.65f, 0.42f, 0.42f),
                new Vector2(430, -232),
                new Vector2(360, 28),
                TextAnchor.MiddleRight
            );

            DrawSongSelector();

            CreateText(
                "InfoTip",
                "Choose " + AlbumReleaseRules.GetRangeText(releaseKind) +
                " songs for this " + AlbumReleaseRules.GetShortLabel(releaseKind) +
                ". You can only continue after selecting at least " + minimumSongs + ".",
                11,
                FontStyle.Normal,
                new Color(0.46f, 0.45f, 0.52f),
                new Vector2(120, -442),
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
                int maximumSongs = GroupAlbumRules.GetMaximumSongs(this);
                if (selectedSongs.Count >= maximumSongs)
                {
                    Debug.Log("[Album] Maximum " + maximumSongs + " songs for this release type.");
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
            const int fontColumns = 4;
            const float fontRowPitch = 34f;
            const float backgroundExtraHeight = 34f;
            int fontRows = Mathf.Max(1, Mathf.CeilToInt((fontNames != null ? fontNames.Length : 0) / (float)fontColumns));
            float fontExtraHeight = Mathf.Max(0, fontRows - 1) * fontRowPitch;

            GameObject controls = CreateCardUnder(
                parent,
                "CoverControls",
                new Vector2(0, 0),
                new Vector2(317, 675 + fontExtraHeight + backgroundExtraHeight),
                new Color(0.975f, 0.975f, 0.985f)
            );

            RectTransform scrollContentRect =
                parent != null ? parent.GetComponent<RectTransform>() : null;
            if (scrollContentRect != null)
                scrollContentRect.sizeDelta = new Vector2(
                    scrollContentRect.sizeDelta.x,
                    690 + fontExtraHeight + backgroundExtraHeight);

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

            CreateMemberPicker(controls.transform);

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
                        RefreshCoverDesignerPreservingScroll();
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
                        RefreshCoverDesignerPreservingScroll();
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

            CreateBackgroundPicker(controls.transform);

            // Text style
            CreatePanelText(
                controls.transform,
                "TextStyleLabel",
                "Text Style",
                11,
                FontStyle.Bold,
                new Color(0.39f, 0.34f, 0.72f),
                new Vector2(14, -308 - backgroundExtraHeight),
                new Vector2(270, 18),
                TextAnchor.MiddleLeft
            );

            for (int i = 0; i < fontNames.Length; i++)
            {
                int index = i;
                int fontRow = i / fontColumns;
                int fontColumn = i % fontColumns;

                CreateChoiceButton(
                    controls.transform,
                    "Font_" + i,
                    fontNames[i],
                    new Vector2(
                        14 + (fontColumn * 76),
                        -328 - backgroundExtraHeight - (fontRow * fontRowPitch)),
                    new Vector2(70, 28),
                    i == selectedFont,
                    () =>
                    {
                        selectedFont = index;
                        RefreshCoverDesignerPreservingScroll();
                    },
                    8
                );
            }

            // Everything after the variable-length font grid lives under one shifted root,
            // so adding custom fonts cannot overlap or push controls outside the scrollable area.
            GameObject afterFontRoot = new GameObject("AfterFontControls");
            afterFontRoot.transform.SetParent(controls.transform, false);
            RectTransform afterFontRect = afterFontRoot.AddComponent<RectTransform>();
            afterFontRect.anchorMin = new Vector2(0f, 1f);
            afterFontRect.anchorMax = new Vector2(0f, 1f);
            afterFontRect.pivot = new Vector2(0f, 1f);
            afterFontRect.anchoredPosition = new Vector2(
                0f,
                -(fontExtraHeight + backgroundExtraHeight));
            afterFontRect.sizeDelta = new Vector2(317f, 675f);
            Transform afterFont = afterFontRoot.transform;

            // Text color
            CreatePanelText(
                afterFont,
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
                swatch.transform.SetParent(afterFont, false);

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
                    RefreshCoverDesignerPreservingScroll();
                });
            }


            // Advanced adjustments
            CreatePanelText(
                afterFont,
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
                afterFont,
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
                    afterFont,
                    "TitlePos_" + i,
                    titlePosLabels[i],
                    new Vector2(14 + (i * 49), -463),
                    new Vector2(44, 22),
                    i == titlePosition,
                    () =>
                    {
                        titlePosition = idx;
                        RefreshCoverDesignerPreservingScroll();
                    },
                    7
                );
            }

            CreateChoiceButton(
                afterFont,
                "GroupNameToggle",
                showGroupName ? "Group ON" : "Group OFF",
                new Vector2(165, -463),
                new Vector2(66, 22),
                showGroupName,
                () =>
                {
                    showGroupName = !showGroupName;
                    RefreshCoverDesignerPreservingScroll();
                },
                7
            );

            string[] ornaments = { "Crown", "Diamond", "Stars" };
            CreateChoiceButton(
                afterFont,
                "Ornament",
                ornaments[ornamentStyle],
                new Vector2(236, -463),
                new Vector2(66, 22),
                true,
                () =>
                {
                    ornamentStyle = (ornamentStyle + 1) % ornaments.Length;
                    RefreshCoverDesignerPreservingScroll();
                },
                7
            );

            // Portrait scale
            CreatePanelText(
                afterFont,
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
                afterFont,
                "PortraitMinus",
                "-",
                new Vector2(110, -493),
                new Vector2(24, 22),
                false,
                () =>
                {
                    portraitScale = Mathf.Clamp(portraitScale - 0.05f, 0.75f, 1.30f);
                    RefreshCoverDesignerPreservingScroll();
                },
                10
            );

            CreatePanelText(
                afterFont,
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
                afterFont,
                "PortraitPlus",
                "+",
                new Vector2(181, -493),
                new Vector2(24, 22),
                false,
                () =>
                {
                    portraitScale = Mathf.Clamp(portraitScale + 0.05f, 0.75f, 1.30f);
                    RefreshCoverDesignerPreservingScroll();
                },
                10
            );

            // Center emphasis
            CreatePanelText(
                afterFont,
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
                afterFont,
                "CenterMinus",
                "-",
                new Vector2(255, -493),
                new Vector2(20, 22),
                false,
                () =>
                {
                    centerEmphasis = Mathf.Clamp(centerEmphasis - 0.03f, 1.00f, 1.25f);
                    RefreshCoverDesignerPreservingScroll();
                },
                9
            );

            CreateChoiceButton(
                afterFont,
                "CenterPlus",
                "+",
                new Vector2(279, -493),
                new Vector2(20, 22),
                false,
                () =>
                {
                    centerEmphasis = Mathf.Clamp(centerEmphasis + 0.03f, 1.00f, 1.25f);
                    RefreshCoverDesignerPreservingScroll();
                },
                9
            );

            // Portrait Y
            CreatePanelText(
                afterFont,
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
                afterFont,
                "PortraitYMinus",
                "-",
                new Vector2(98, -523),
                new Vector2(24, 22),
                false,
                () =>
                {
                    portraitYOffset = Mathf.Clamp(portraitYOffset - 8f, -80f, 80f);
                    RefreshCoverDesignerPreservingScroll();
                },
                9
            );

            CreatePanelText(
                afterFont,
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
                afterFont,
                "PortraitYPlus",
                "+",
                new Vector2(168, -523),
                new Vector2(24, 22),
                false,
                () =>
                {
                    portraitYOffset = Mathf.Clamp(portraitYOffset + 8f, -80f, 80f);
                    RefreshCoverDesignerPreservingScroll();
                },
                9
            );

            // Spacing
            CreatePanelText(
                afterFont,
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
                afterFont,
                "SpacingMinus",
                "-",
                new Vector2(253, -523),
                new Vector2(20, 22),
                false,
                () =>
                {
                    portraitSpacing = Mathf.Clamp(portraitSpacing - 0.05f, 0.70f, 1.35f);
                    RefreshCoverDesignerPreservingScroll();
                },
                9
            );

            CreateChoiceButton(
                afterFont,
                "SpacingPlus",
                "+",
                new Vector2(277, -523),
                new Vector2(20, 22),
                false,
                () =>
                {
                    portraitSpacing = Mathf.Clamp(portraitSpacing + 0.05f, 0.70f, 1.35f);
                    RefreshCoverDesignerPreservingScroll();
                },
                9
            );

            // FX intensity
            CreatePanelText(
                afterFont,
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
                afterFont,
                "EffectsMinus",
                "-",
                new Vector2(74, -553),
                new Vector2(24, 22),
                false,
                () =>
                {
                    effectsIntensity = Mathf.Clamp(effectsIntensity - 0.10f, 0.40f, 1.50f);
                    RefreshCoverDesignerPreservingScroll();
                },
                9
            );

            CreatePanelText(
                afterFont,
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
                afterFont,
                "EffectsPlus",
                "+",
                new Vector2(147, -553),
                new Vector2(24, 22),
                false,
                () =>
                {
                    effectsIntensity = Mathf.Clamp(effectsIntensity + 0.10f, 0.40f, 1.50f);
                    RefreshCoverDesignerPreservingScroll();
                },
                9
            );

            // Frame selector
            string[] frameNames = { "None", "Simple", "Elegant", "Stars", "Neon" };
            CreateChoiceButton(
                afterFont,
                "FrameSelector",
                "Frame: " + frameNames[frameStyle],
                new Vector2(180, -553),
                new Vector2(118, 22),
                true,
                () =>
                {
                    frameStyle = (frameStyle + 1) % frameNames.Length;
                    RefreshCoverDesignerPreservingScroll();
                },
                7
            );

            // Title effect selector
            string[] titleEffects = { "None", "Shadow", "Outline", "Glow" };
            CreateChoiceButton(
                afterFont,
                "TitleEffectSelector",
                "Title FX: " + titleEffects[titleEffect],
                new Vector2(14, -583),
                new Vector2(132, 22),
                true,
                () =>
                {
                    titleEffect = (titleEffect + 1) % titleEffects.Length;
                    RefreshCoverDesignerPreservingScroll();
                },
                7
            );

            // Randomize cover
            CreateChoiceButton(
                afterFont,
                "RandomizeCover",
                "Randomize Cover",
                new Vector2(154, -583),
                new Vector2(144, 22),
                true,
                () =>
                {
                    RandomizeCover();
                    RefreshCoverDesignerPreservingScroll();
                },
                7
            );

            CreatePanelText(
                afterFont,
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
                afterFont,
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

        private void CreateMemberPicker(Transform parent)
        {
            if (parent == null)
                return;

            // Keep this strip within the original 38px member-control band so the rest of
            // the cover controls do not move. Seven/eight-member albums simply scroll.
            const float rootWidth = 289f;
            const float rootHeight = 38f;
            const float viewportWidth = 279f;
            const float viewportHeight = 30f;
            const float thumbWidth = 30f;
            const float thumbHeight = 30f;
            const float thumbPitch = 40f;
            const float sidePadding = 4f;

            GameObject root = new GameObject("MemberPickerScroll");
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.sizeDelta = new Vector2(rootWidth, rootHeight);
            rootRect.anchoredPosition = new Vector2(12f, -82f);

            Image rootImage = root.AddComponent<Image>();
            rootImage.color = new Color(0.95f, 0.95f, 0.97f, 0.55f);

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(root.transform, false);
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0f, 1f);
            viewportRect.anchorMax = new Vector2(0f, 1f);
            viewportRect.pivot = new Vector2(0f, 1f);
            viewportRect.sizeDelta = new Vector2(viewportWidth, viewportHeight);
            viewportRect.anchoredPosition = new Vector2(5f, -1f);
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = Color.clear;
            viewportImage.raycastTarget = true;
            viewport.AddComponent<RectMask2D>();

            GameObject contentObject = new GameObject("MemberContent");
            contentObject.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = contentObject.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
            float requiredWidth = selectedGirls.Count > 0
                ? sidePadding * 2f + selectedGirls.Count * thumbPitch -
                    (thumbPitch - thumbWidth)
                : viewportWidth;
            contentRect.sizeDelta = new Vector2(
                Mathf.Max(viewportWidth, requiredWidth),
                viewportHeight);
            contentRect.anchoredPosition = Vector2.zero;

            for (int i = 0; i < selectedGirls.Count; i++)
            {
                data_girls.girls girl = selectedGirls[i];
                if (girl == null)
                    continue;

                GameObject thumb = new GameObject("MemberThumb_" + i);
                thumb.transform.SetParent(contentObject.transform, false);

                RectTransform thumbRect = thumb.AddComponent<RectTransform>();
                thumbRect.anchorMin = new Vector2(0f, 1f);
                thumbRect.anchorMax = new Vector2(0f, 1f);
                thumbRect.pivot = new Vector2(0f, 1f);
                thumbRect.sizeDelta = new Vector2(thumbWidth, thumbHeight);
                thumbRect.anchoredPosition = new Vector2(
                    sidePadding + i * thumbPitch,
                    0f);

                Image image = thumb.AddComponent<Image>();
                image.sprite = girl.texture.middle;
                image.preserveAspect = true;

                bool isCenter = selectedCenterGirl == girl;
                Outline outline = thumb.AddComponent<Outline>();
                outline.effectColor = isCenter
                    ? new Color(0.95f, 0.72f, 0.20f)
                    : new Color(0.75f, 0.72f, 0.88f);
                outline.effectDistance = isCenter
                    ? new Vector2(2f, -2f)
                    : new Vector2(1f, -1f);

                Button centerButton = thumb.AddComponent<Button>();
                centerButton.targetGraphic = image;
                data_girls.girls centerGirl = girl;
                centerButton.onClick.AddListener(() =>
                {
                    selectedCenterGirl = centerGirl;
                    RefreshCoverDesignerPreservingScroll();
                });

                if (isCenter)
                {
                    CreatePanelText(
                        thumb.transform,
                        "CenterCrown",
                        "★",
                        8,
                        FontStyle.Bold,
                        new Color(1f, 0.78f, 0.20f),
                        new Vector2(19f, 0f),
                        new Vector2(11f, 11f),
                        TextAnchor.MiddleCenter
                    );
                }
            }

            GameObject scrollbarObject = new GameObject("MemberScrollbar");
            scrollbarObject.transform.SetParent(root.transform, false);
            RectTransform scrollbarRect = scrollbarObject.AddComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(0f, 1f);
            scrollbarRect.anchorMax = new Vector2(0f, 1f);
            scrollbarRect.pivot = new Vector2(0f, 1f);
            scrollbarRect.sizeDelta = new Vector2(viewportWidth - 8f, 6f);
            scrollbarRect.anchoredPosition = new Vector2(9f, -31f);
            Image scrollbarTrack = scrollbarObject.AddComponent<Image>();
            scrollbarTrack.color = new Color(0.82f, 0.82f, 0.87f, 0.90f);

            GameObject slidingArea = new GameObject("SlidingArea");
            slidingArea.transform.SetParent(scrollbarObject.transform, false);
            RectTransform slidingRect = slidingArea.AddComponent<RectTransform>();
            slidingRect.anchorMin = Vector2.zero;
            slidingRect.anchorMax = Vector2.one;
            slidingRect.offsetMin = new Vector2(1f, 1f);
            slidingRect.offsetMax = new Vector2(-1f, -1f);

            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(slidingArea.transform, false);
            RectTransform handleRect = handle.AddComponent<RectTransform>();
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;
            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color(0.43f, 0.37f, 0.72f, 0.95f);

            Scrollbar scrollbar = scrollbarObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.LeftToRight;
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handleImage;

            ScrollRect scroll = root.AddComponent<ScrollRect>();
            memberPickerScroll = scroll;
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 24f;
            scroll.horizontalScrollbar = scrollbar;
            scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scroll.horizontalScrollbarSpacing = 0f;
            scroll.horizontalNormalizedPosition = Mathf.Clamp01(memberScrollPosition);
            scroll.onValueChanged.AddListener(value =>
            {
                memberScrollPosition = Mathf.Clamp01(value.x);
            });
        }

        private void CreateBackgroundPicker(Transform parent)
        {
            if (parent == null)
                return;

            const float rootWidth = 289f;
            const float rootHeight = 64f;
            const float viewportWidth = 279f;
            const float viewportHeight = 40f;
            const float thumbWidth = 42f;
            const float thumbHeight = 36f;
            const float thumbPitch = 48f;
            const float sidePadding = 5f;

            CreatePanelText(
                parent,
                "BackgroundSelected",
                coverBackgrounds.Count > 0
                    ? AlbumBackgroundCatalog.GetDisplayName(selectedBackgroundKey, selectedBackground)
                    : "No images found",
                8,
                FontStyle.Normal,
                new Color(0.42f, 0.40f, 0.55f),
                new Vector2(92, -247),
                new Vector2(207, 18),
                TextAnchor.MiddleRight
            );

            GameObject root = new GameObject("BackgroundPickerScroll");
            root.transform.SetParent(parent, false);
            RectTransform rootRect = root.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(0f, 1f);
            rootRect.pivot = new Vector2(0f, 1f);
            rootRect.sizeDelta = new Vector2(rootWidth, rootHeight);
            rootRect.anchoredPosition = new Vector2(12f, -267f);

            Image rootImage = root.AddComponent<Image>();
            rootImage.color = new Color(0.95f, 0.95f, 0.97f, 0.55f);

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(root.transform, false);
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0f, 1f);
            viewportRect.anchorMax = new Vector2(0f, 1f);
            viewportRect.pivot = new Vector2(0f, 1f);
            viewportRect.sizeDelta = new Vector2(viewportWidth, viewportHeight);
            viewportRect.anchoredPosition = new Vector2(5f, -2f);
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = Color.clear;
            viewportImage.raycastTarget = true;
            viewport.AddComponent<RectMask2D>();

            GameObject contentObject = new GameObject("BackgroundContent");
            contentObject.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = contentObject.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(0f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
            float requiredWidth = coverBackgrounds.Count > 0
                ? sidePadding * 2f + coverBackgrounds.Count * thumbPitch - (thumbPitch - thumbWidth)
                : viewportWidth;
            contentRect.sizeDelta = new Vector2(Mathf.Max(viewportWidth, requiredWidth), viewportHeight);
            contentRect.anchoredPosition = Vector2.zero;

            for (int i = 0; i < coverBackgrounds.Count; i++)
            {
                int index = i;
                GameObject thumb = new GameObject("Background_" + i);
                thumb.transform.SetParent(contentObject.transform, false);

                RectTransform thumbRect = thumb.AddComponent<RectTransform>();
                thumbRect.anchorMin = new Vector2(0f, 1f);
                thumbRect.anchorMax = new Vector2(0f, 1f);
                thumbRect.pivot = new Vector2(0f, 1f);
                thumbRect.sizeDelta = new Vector2(thumbWidth, thumbHeight);
                // Side padding keeps the first selected outline fully inside the mask instead
                // of shaving pixels off the left edge.
                thumbRect.anchoredPosition = new Vector2(sidePadding + i * thumbPitch, -2f);

                Image image = thumb.AddComponent<Image>();
                image.sprite = coverBackgrounds[i];
                image.preserveAspect = true;
                AlbumBackgroundCatalog.TrackUsage(thumb, coverBackgrounds[i]);

                Outline outline = thumb.AddComponent<Outline>();
                outline.effectColor = i == selectedBackground
                    ? new Color(0.42f, 0.32f, 0.90f)
                    : new Color(0.80f, 0.80f, 0.86f);
                outline.effectDistance = i == selectedBackground
                    ? new Vector2(2f, -2f)
                    : new Vector2(1f, -1f);

                Button button = thumb.AddComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() =>
                {
                    selectedBackground = index;
                    selectedBackgroundKey = AlbumBackgroundCatalog.GetKey(index);
                    RefreshCoverDesignerPreservingScroll();
                });
            }

            GameObject scrollbarObject = new GameObject("BackgroundScrollbar");
            scrollbarObject.transform.SetParent(root.transform, false);
            RectTransform scrollbarRect = scrollbarObject.AddComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(0f, 1f);
            scrollbarRect.anchorMax = new Vector2(0f, 1f);
            scrollbarRect.pivot = new Vector2(0f, 1f);
            scrollbarRect.sizeDelta = new Vector2(viewportWidth - 8f, 10f);
            scrollbarRect.anchoredPosition = new Vector2(9f, -49f);
            Image scrollbarTrack = scrollbarObject.AddComponent<Image>();
            scrollbarTrack.color = new Color(0.82f, 0.82f, 0.87f, 0.90f);

            GameObject slidingArea = new GameObject("SlidingArea");
            slidingArea.transform.SetParent(scrollbarObject.transform, false);
            RectTransform slidingRect = slidingArea.AddComponent<RectTransform>();
            slidingRect.anchorMin = Vector2.zero;
            slidingRect.anchorMax = Vector2.one;
            slidingRect.offsetMin = new Vector2(2f, 2f);
            slidingRect.offsetMax = new Vector2(-2f, -2f);

            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(slidingArea.transform, false);
            RectTransform handleRect = handle.AddComponent<RectTransform>();
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;
            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color(0.43f, 0.37f, 0.72f, 0.95f);

            Scrollbar scrollbar = scrollbarObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.LeftToRight;
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handleImage;

            ScrollRect scroll = root.AddComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 24f;
            scroll.horizontalScrollbar = scrollbar;
            scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scroll.horizontalScrollbarSpacing = 0f;
            scroll.horizontalNormalizedPosition = Mathf.Clamp01(backgroundScrollPosition);
            scroll.onValueChanged.AddListener(value =>
            {
                backgroundScrollPosition = Mathf.Clamp01(value.x);
            });
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
            AlbumData previewAlbum = CreateCurrentCoverAlbumData();
            return AlbumCoverRenderer.Build(
                parent,
                previewAlbum,
                objectName,
                anchoredPosition,
                size);
        }

        private AlbumData CreateCurrentCoverAlbumData()
        {
            AlbumData album = new AlbumData();
            album.Title = string.IsNullOrWhiteSpace(AlbumTitle)
                ? "ECLIPSE"
                : AlbumTitle.Trim();
            album.GroupName = GetCoverSubtitle();
            album.Members = new List<data_girls.girls>(selectedGirls);

            ApplyCurrentCoverSettings(album);
            return album;
        }

        private void ApplyCurrentCoverSettings(AlbumData album)
        {
            if (album == null)
                return;

            album.Theme = themeNames[selectedTheme];
            album.ThemeIndex = selectedTheme;
            album.BackgroundIndex = selectedBackground;
            album.BackgroundKey = string.IsNullOrEmpty(selectedBackgroundKey)
                ? AlbumBackgroundCatalog.GetKey(selectedBackground)
                : selectedBackgroundKey;
            album.LayoutIndex = selectedLayout;
            album.FontIndex = selectedFont;
            album.FontKey = AlbumFontCatalog.GetKey(selectedFont);
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
            album.HasCenterMemberId = selectedCenterGirl != null;
            album.CenterMemberId = selectedCenterGirl != null
                ? selectedCenterGirl.id
                : -1;
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

        private void RandomizeCover()
        {
            selectedTheme = UnityEngine.Random.Range(0, themeNames.Length);

            if (coverBackgrounds.Count > 0)
            {
                selectedBackground = UnityEngine.Random.Range(0, coverBackgrounds.Count);
                selectedBackgroundKey = AlbumBackgroundCatalog.GetKey(selectedBackground);
            }

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
                    ? AlbumBackgroundCatalog.GetDisplayName(selectedBackgroundKey, selectedBackground)
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
            // Throttled signature refresh lets users drop new images into AlbumBackgrounds
            // while the game is running without rescanning the filesystem on every button click.
            AlbumBackgroundCatalog.Refresh(false);
            coverBackgrounds.Clear();
            coverBackgrounds.AddRange(AlbumBackgroundCatalog.GetSprites());

            if (coverBackgrounds.Count == 0)
            {
                selectedBackground = 0;
                return;
            }

            selectedBackground = AlbumBackgroundCatalog.GetIndex(
                selectedBackgroundKey,
                selectedBackground);
            if (string.IsNullOrEmpty(selectedBackgroundKey))
                selectedBackgroundKey = AlbumBackgroundCatalog.GetKey(selectedBackground);
        }

        private void LoadAlbumFonts()
        {
            AlbumFontCatalog.EnsureLoaded();
            fontNames = AlbumFontCatalog.GetDisplayNames();
            albumFonts = AlbumFontCatalog.GetFonts();

            if (albumFonts == null || albumFonts.Length == 0)
            {
                albumFonts = new[] { AlbumUiResources.GetGameFont() };
                fontNames = new[] { "Game Font" };
            }

            selectedFont = Mathf.Clamp(selectedFont, 0, albumFonts.Length - 1);
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

            if (step == AlbumStep.Info)
            {
                int minimumSongs = GroupAlbumRules.GetMinimumSongs(this);
                int maximumSongs = GroupAlbumRules.GetMaximumSongs(this);
                if (selectedSongs.Count < minimumSongs || selectedSongs.Count > maximumSongs)
                {
                    Debug.Log(
                        "[Album] Select " + minimumSongs + " to " + maximumSongs +
                        " songs for this release type.");
                    return;
                }
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
            GroupAlbumRules.ForgetPopupState(this);
            step = AlbumStep.Info;
            coverScrollRestoreGeneration++;
            coverOptionsScroll = null;
            memberPickerScroll = null;
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
            selectedBackgroundKey = "";
            backgroundScrollPosition = 0f;
            memberScrollPosition = 0f;
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
