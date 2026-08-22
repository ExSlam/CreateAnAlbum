using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Albummodelite;

namespace CreateAnAlbumGroupRules
{
    internal static class GroupAlbumRules
    {
        private const int RequiredReleasedSingles = 6;
        private const int MaximumAlbumSongs = 15;
        private const int MaximumMembers = 8;

        // Store the chosen group by group ID rather than retaining a stale
        // Groups._group reference across save reloads.
        private static readonly Dictionary<int, int> SelectedGroupIds =
            new Dictionary<int, int>();

        private static readonly Dictionary<int, AlbumReleaseKind> SelectedReleaseKinds =
            new Dictionary<int, AlbumReleaseKind>();

        private static readonly BindingFlags InstanceFlags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        // ---------------------------------------------------------------------
        // GROUP LOOKUP
        // ---------------------------------------------------------------------

        internal static AlbumReleaseKind GetSelectedReleaseKind(
            AlbumPopup popup)
        {
            if (popup == null)
                return AlbumReleaseKind.MiniAlbum;

            int key = popup.GetInstanceID();

            AlbumReleaseKind kind;

            if (!SelectedReleaseKinds.TryGetValue(
                    key,
                    out kind))
            {
                kind = AlbumReleaseKind.MiniAlbum;
                SelectedReleaseKinds[key] = kind;
            }

            return kind;
        }

        internal static void SetSelectedReleaseKind(
            AlbumPopup popup,
            AlbumReleaseKind kind)
        {
            if (popup == null)
                return;

            SelectedReleaseKinds[
                popup.GetInstanceID()
            ] = kind;

            List<singles._single> selectedSongs =
                GetField<List<singles._single>>(
                    popup,
                    "selectedSongs"
                );

            int max =
                AlbumReleaseRules.GetMaximumSongs(kind);

            if (selectedSongs != null &&
                selectedSongs.Count > max)
            {
                selectedSongs.RemoveRange(
                    max,
                    selectedSongs.Count - max
                );
            }

            RefreshPopup(popup);
        }

        internal static int GetMinimumSongs(
            AlbumPopup popup)
        {
            return AlbumReleaseRules.GetMinimumSongs(
                GetSelectedReleaseKind(popup)
            );
        }

        internal static int GetMaximumSongs(
            AlbumPopup popup)
        {
            return AlbumReleaseRules.GetMaximumSongs(
                GetSelectedReleaseKind(popup)
            );
        }

        private static void MoveChild(
            Transform parent,
            string name,
            Vector2 position)
        {
            if (parent == null)
                return;

            Transform child =
                parent.Find(name);

            if (child == null)
                return;

            RectTransform rect =
                child.GetComponent<RectTransform>();

            if (rect != null)
                rect.anchoredPosition = position;
        }

        private static void RepositionInfoFields(
            GameObject content)
        {
            if (content == null)
                return;

            Transform root = content.transform;

            MoveChild(
                root,
                "AlbumTitleLabel",
                new Vector2(120, -148)
            );

            MoveChild(
                root,
                "AlbumTitleInput",
                new Vector2(120, -174)
            );

            MoveChild(
                root,
                "SongsHeading",
                new Vector2(120, -232)
            );

            MoveChild(
                root,
                "SongsCount",
                new Vector2(430, -232)
            );

            // AlbumPopup.DrawSongSelector is Harmony-replaced below by the group-specific
            // selector, whose root is SongSelectorScrollRoot. Move that actual live control,
            // not the unused vanilla SongSelectorScroll object name.
            MoveChild(
                root,
                "SongSelectorScrollRoot",
                new Vector2(120, -264)
            );

            Transform selector =
                root.Find(
                    "SongSelectorScrollRoot"
                );

            if (selector != null)
            {
                RectTransform rect =
                    selector.GetComponent<RectTransform>();

                if (rect != null)
                    rect.sizeDelta =
                        new Vector2(
                            680,
                            170
                        );
            }

            MoveChild(
                root,
                "InfoTip",
                new Vector2(120, -442)
            );
        }

        private static void DrawReleaseTypeSelector(
            AlbumPopup popup,
            GameObject content)
        {
            if (popup == null ||
                content == null)
            {
                return;
            }

            AlbumReleaseKind selected =
                GetSelectedReleaseKind(popup);

            CreateText(
                content.transform,
                "ReleaseTypeLabel",
                "Release Type",
                12,
                FontStyle.Bold,
                new Color(
                    0.39f,
                    0.34f,
                    0.72f
                ),
                new Vector2(
                    120,
                    -55
                ),
                new Vector2(
                    250,
                    24
                ),
                TextAnchor.MiddleLeft
            );

            DrawReleaseTypeButton(
                popup,
                content.transform,
                AlbumReleaseKind.MiniAlbum,
                "Mini Album",
                "6 songs",
                new Vector2(
                    120,
                    -81
                ),
                selected ==
                    AlbumReleaseKind.MiniAlbum
            );

            DrawReleaseTypeButton(
                popup,
                content.transform,
                AlbumReleaseKind.EP,
                "EP",
                "6–10 songs",
                new Vector2(
                    325,
                    -81
                ),
                selected ==
                    AlbumReleaseKind.EP
            );

            DrawReleaseTypeButton(
                popup,
                content.transform,
                AlbumReleaseKind.LP,
                "Album / LP",
                "10–15 songs",
                new Vector2(
                    530,
                    -81
                ),
                selected ==
                    AlbumReleaseKind.LP
            );
        }

        private static void DrawReleaseTypeButton(
            AlbumPopup popup,
            Transform parent,
            AlbumReleaseKind kind,
            string title,
            string subtitle,
            Vector2 position,
            bool selected)
        {
            GameObject obj =
                new GameObject(
                    "ReleaseType_" +
                    kind
                );

            obj.transform.SetParent(
                parent,
                false
            );

            RectTransform rect =
                obj.AddComponent<RectTransform>();

            rect.anchorMin =
                new Vector2(0, 1);

            rect.anchorMax =
                new Vector2(0, 1);

            rect.pivot =
                new Vector2(0, 1);

            rect.sizeDelta =
                new Vector2(
                    190,
                    39
                );

            rect.anchoredPosition =
                position;

            Image bg =
                obj.AddComponent<Image>();

            bg.color =
                selected
                    ? new Color(
                        0.91f,
                        0.99f,
                        0.93f
                    )
                    : Color.white;

            Outline outline =
                obj.AddComponent<Outline>();

            outline.effectColor =
                selected
                    ? new Color(
                        0.28f,
                        0.67f,
                        0.39f
                    )
                    : new Color(
                        0.76f,
                        0.75f,
                        0.84f
                    );

            outline.effectDistance =
                new Vector2(1, -1);

            Button button =
                obj.AddComponent<Button>();

            button.targetGraphic = bg;

            button.onClick.AddListener(
                delegate
                {
                    SetSelectedReleaseKind(
                        popup,
                        kind
                    );
                }
            );

            CreateText(
                obj.transform,
                "Title",
                title,
                11,
                FontStyle.Bold,
                selected
                    ? new Color(
                        0.25f,
                        0.58f,
                        0.34f
                    )
                    : new Color(
                        0.39f,
                        0.34f,
                        0.72f
                    ),
                new Vector2(
                    8,
                    -2
                ),
                new Vector2(
                    174,
                    19
                ),
                TextAnchor.MiddleCenter
            );

            CreateText(
                obj.transform,
                "Subtitle",
                subtitle,
                8,
                FontStyle.Normal,
                new Color(
                    0.50f,
                    0.49f,
                    0.58f
                ),
                new Vector2(
                    8,
                    -19
                ),
                new Vector2(
                    174,
                    17
                ),
                TextAnchor.MiddleCenter
            );
        }

        internal static List<Groups._group> GetPlayerGroups()
        {
            List<Groups._group> result = new List<Groups._group>();

            if (Groups.Groups_ == null)
                return result;

            foreach (Groups._group group in Groups.Groups_)
            {
                if (group == null)
                    continue;

                if (group.Status != Groups._group._status.normal)
                    continue;

                if (!group.Showing)
                    continue;

                if (string.IsNullOrWhiteSpace(group.Title))
                    continue;

                result.Add(group);
            }

            return result;
        }

        internal static Groups._group GetSelectedGroup(AlbumPopup popup)
        {
            if (popup == null)
                return null;

            List<Groups._group> groups = GetPlayerGroups();
            if (groups.Count == 0)
                return null;

            int popupKey = popup.GetInstanceID();
            int selectedId;

            if (SelectedGroupIds.TryGetValue(popupKey, out selectedId))
            {
                Groups._group existing = groups.FirstOrDefault(g => g.ID == selectedId);
                if (existing != null)
                    return existing;
            }

            Groups._group main = null;

            try
            {
                main = Groups.GetMainGroup();
            }
            catch
            {
                // Fall back to the first visible normal player group.
            }

            if (main == null || !groups.Contains(main))
                main = groups[0];

            SelectedGroupIds[popupKey] = main.ID;
            return main;
        }

        internal static void ChangeGroup(AlbumPopup popup, int direction)
        {
            if (popup == null)
                return;

            List<Groups._group> groups = GetPlayerGroups();
            if (groups.Count == 0)
                return;

            Groups._group current = GetSelectedGroup(popup);
            int index = current != null ? groups.IndexOf(current) : 0;

            if (index < 0)
                index = 0;

            index += direction;

            if (index < 0)
                index = groups.Count - 1;
            else if (index >= groups.Count)
                index = 0;

            SelectedGroupIds[popup.GetInstanceID()] = groups[index].ID;

            ClearAlbumSelections(popup);
            RefreshPopup(popup);
        }

        internal static List<singles._single> GetReleasedSingles(Groups._group group)
        {
            if (group == null)
                return new List<singles._single>();

            List<singles._single> singlesForGroup;

            try
            {
                singlesForGroup = group.GetSingles();
            }
            catch
            {
                singlesForGroup = group.Singles ?? new List<singles._single>();
            }

            if (singlesForGroup == null)
                return new List<singles._single>();

            return singlesForGroup
                .Where(s =>
                    s != null &&
                    s.status == singles._single._status.released &&
                    !string.IsNullOrWhiteSpace(s.title))
                .Distinct()
                .ToList();
        }

        internal static List<data_girls.girls> GetGroupMembers(Groups._group group)
        {
            if (group == null)
                return new List<data_girls.girls>();

            List<data_girls.girls> members;

            try
            {
                members = group.GetGirls();
            }
            catch
            {
                members = group.Girls ?? new List<data_girls.girls>();
            }

            if (members == null)
                return new List<data_girls.girls>();

            return members
                .Where(g =>
                    g != null &&
                    g.status != data_girls._status.graduated)
                .Distinct()
                .ToList();
        }

        internal static bool GroupCanMakeAlbum(Groups._group group)
        {
            return GetReleasedSingles(group).Count >= RequiredReleasedSingles;
        }

        // ---------------------------------------------------------------------
        // REFLECTION HELPERS FOR THE EXISTING WORKING ALBUM DLL
        // ---------------------------------------------------------------------

        private static FieldInfo FindField(Type type, string name)
        {
            FieldInfo field = type.GetField(name, InstanceFlags);

            if (field == null)
                throw new MissingFieldException(type.FullName, name);

            return field;
        }

        internal static T GetField<T>(AlbumPopup popup, string name)
        {
            object value = FindField(typeof(AlbumPopup), name).GetValue(popup);

            if (value == null)
                return default(T);

            return (T)value;
        }

        internal static void SetField(AlbumPopup popup, string name, object value)
        {
            FindField(typeof(AlbumPopup), name).SetValue(popup, value);
        }

        internal static void RefreshPopup(AlbumPopup popup)
        {
            MethodInfo method =
                typeof(AlbumPopup).GetMethod("RefreshUI", InstanceFlags);

            if (method != null)
                method.Invoke(popup, null);
        }

        internal static GameObject GetContent(AlbumPopup popup)
        {
            return GetField<GameObject>(popup, "content");
        }

        internal static void ClearAlbumSelections(AlbumPopup popup)
        {
            List<singles._single> selectedSongs =
                GetField<List<singles._single>>(popup, "selectedSongs");

            List<int> selectedMembers =
                GetField<List<int>>(popup, "selectedMembers");

            List<data_girls.girls> selectedGirls =
                GetField<List<data_girls.girls>>(popup, "selectedGirls");

            if (selectedSongs != null)
                selectedSongs.Clear();

            if (selectedMembers != null)
                selectedMembers.Clear();

            if (selectedGirls != null)
                selectedGirls.Clear();

            SetField(popup, "selectedCenterGirl", null);
        }

        internal static void ClearAllPopupState()
        {
            SelectedGroupIds.Clear();
            SelectedReleaseKinds.Clear();
        }

        internal static void ForgetPopupState(AlbumPopup popup)
        {
            if (popup == null)
                return;

            int key = popup.GetInstanceID();
            SelectedGroupIds.Remove(key);
            SelectedReleaseKinds.Remove(key);
        }

        internal static void ResetPopupState(AlbumPopup popup)
        {
            if (popup == null)
                return;

            ForgetPopupState(popup);
            SetField(popup, "panel", null);

            MethodInfo reset = typeof(AlbumPopup).GetMethod("ResetAlbumCreation", InstanceFlags);
            if (reset != null)
                reset.Invoke(popup, null);
        }

        // ---------------------------------------------------------------------
        // INFO PAGE GROUP PICKER
        // ---------------------------------------------------------------------

        internal static void DrawGroupSelector(AlbumPopup popup)
        {
            GameObject content = GetContent(popup);
            if (content == null)
                return;

            Groups._group group = GetSelectedGroup(popup);
            List<Groups._group> groups = GetPlayerGroups();

            // AlbumPopup.DrawInfo now owns the release-aware vertical geometry directly.
            // This postfix only adds the release selector/group context, so changing release
            // type cannot rebuild the old compact 6–10-song layout underneath it.
            popup.EnsureContentHeight(510f);
            DrawReleaseTypeSelector(
                popup,
                content
            );

            if (group == null)
            {
                CreateText(
                    content.transform,
                    "AlbumGroupMissing",
                    "No active player group found.",
                    11,
                    FontStyle.Bold,
                    new Color(0.72f, 0.25f, 0.25f),
                    new Vector2(505, -16),
                    new Vector2(300, 34),
                    TextAnchor.MiddleRight
                );

                return;
            }

            int released = GetReleasedSingles(group).Count;
            int minimumSongs =
                GetMinimumSongs(popup);
            int maximumSongs =
                GetMaximumSongs(popup);
            AlbumReleaseKind releaseKind =
                GetSelectedReleaseKind(popup);

            bool ready =
                released >= minimumSongs;

            GameObject card = new GameObject("AlbumGroupSelector");
            card.transform.SetParent(content.transform, false);

            RectTransform cr = card.AddComponent<RectTransform>();
            cr.anchorMin = new Vector2(0, 1);
            cr.anchorMax = new Vector2(0, 1);
            cr.pivot = new Vector2(0, 1);
            cr.sizeDelta = new Vector2(330, 42);
            cr.anchoredPosition = new Vector2(480, -12);

            Image cardBg = card.AddComponent<Image>();
            cardBg.color = Color.white;

            Outline outline = card.AddComponent<Outline>();
            outline.effectColor = ready
                ? new Color(0.38f, 0.72f, 0.48f)
                : new Color(0.80f, 0.60f, 0.32f);
            outline.effectDistance = new Vector2(1, -1);

            if (groups.Count > 1)
            {
                CreateMiniButton(
                    card.transform,
                    "PreviousGroup",
                    "‹",
                    new Vector2(4, -4),
                    new Vector2(34, 34),
                    () => ChangeGroup(popup, -1)
                );

                CreateMiniButton(
                    card.transform,
                    "NextGroup",
                    "›",
                    new Vector2(292, -4),
                    new Vector2(34, 34),
                    () => ChangeGroup(popup, 1)
                );
            }

            string groupText =
                group.Title.ToUpperInvariant() +
                "\n" +
                released +
                " / " +
                minimumSongs +
                " released singles" +
                (ready ? "  ✓" : "  🔒");

            CreateText(
                card.transform,
                "SelectedGroupText",
                groupText,
                11,
                FontStyle.Bold,
                ready
                    ? new Color(0.28f, 0.58f, 0.36f)
                    : new Color(0.65f, 0.43f, 0.24f),
                new Vector2(42, -2),
                new Vector2(246, 38),
                TextAnchor.MiddleCenter
            );

            // Re-label the existing song section so it is obvious which group's
            // singles are being displayed.
            Transform songsHeading = content.transform.Find("SongsHeading");
            if (songsHeading != null)
            {
                Text text = songsHeading.GetComponent<Text>();
                if (text != null)
                    text.text = group.Title + " Singles";
            }

            Transform songsCount = content.transform.Find("SongsCount");
            if (songsCount != null)
            {
                Text text = songsCount.GetComponent<Text>();
                List<singles._single> selectedSongs =
                    GetField<List<singles._single>>(popup, "selectedSongs");

                int selectedCount = selectedSongs != null ? selectedSongs.Count : 0;

                if (text != null)
                {
                    text.text =
                        "Selected: " +
                        selectedCount +
                        " / " +
                        maximumSongs +
                        "   •   Minimum " +
                        minimumSongs +
                        " songs";

                    text.color =
                        ready &&
                        selectedCount >= minimumSongs &&
                        selectedCount <= maximumSongs
                            ? new Color(0.30f, 0.62f, 0.40f)
                            : new Color(0.65f, 0.42f, 0.42f);
                }
            }

            Transform infoTip = content.transform.Find("InfoTip");
            if (infoTip != null)
            {
                Text text = infoTip.GetComponent<Text>();

                if (text != null)
                {
                    if (!ready)
                    {
                        int needed = minimumSongs - released;

                        text.text =
                            group.Title +
                            " needs " +
                            needed +
                            " more released single" +
                            (needed == 1 ? "" : "s") +
                            " before it can make an album.";
                    }
                    else
                    {
                        text.text =
                            "Choose " +
                            AlbumReleaseRules.GetRangeText(
                                releaseKind
                            ) +
                            " released singles by " +
                            group.Title +
                            ". Production cost: ¥500,000.";
                    }
                }
            }
        }

        // ---------------------------------------------------------------------
        // GROUP-SPECIFIC SONG LIST
        // ---------------------------------------------------------------------

        internal static void DrawSongSelector(AlbumPopup popup)
        {
            GameObject content = GetContent(popup);
            if (content == null)
                return;

            Groups._group group = GetSelectedGroup(popup);
            List<singles._single> songs = GetReleasedSingles(group);
            List<singles._single> selectedSongs =
                GetField<List<singles._single>>(popup, "selectedSongs");

            GameObject scrollRoot = new GameObject("SongSelectorScrollRoot");
            scrollRoot.transform.SetParent(content.transform, false);

            RectTransform scrollRootRect = scrollRoot.AddComponent<RectTransform>();
            scrollRootRect.anchorMin = new Vector2(0, 1);
            scrollRootRect.anchorMax = new Vector2(0, 1);
            scrollRootRect.pivot = new Vector2(0, 1);
            scrollRootRect.sizeDelta = new Vector2(680, 170);
            scrollRootRect.anchoredPosition = new Vector2(120, -264);

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

            GameObject viewport = new GameObject("SongSelectorViewport");
            viewport.transform.SetParent(scrollRoot.transform, false);

            RectTransform vr = viewport.AddComponent<RectTransform>();
            vr.anchorMin = Vector2.zero;
            vr.anchorMax = Vector2.one;
            vr.offsetMin = Vector2.zero;
            vr.offsetMax = Vector2.zero;

            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = Color.clear;
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
                CreateText(
                    listObj.transform,
                    "NoSongs",
                    group == null
                        ? "No group is selected."
                        : group.Title + " has no released singles yet.",
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
                bool selected =
                    selectedSongs != null &&
                    selectedSongs.Contains(song);

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
                button.onClick.AddListener(
                    () => ToggleSong(popup, capturedSong)
                );

                CreateText(
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
                    CreateText(
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

        internal static void ToggleSong(AlbumPopup popup, singles._single song)
        {
            if (popup == null || song == null)
                return;

            Groups._group group = GetSelectedGroup(popup);
            List<singles._single> validSongs = GetReleasedSingles(group);

            if (!validSongs.Contains(song))
            {
                Debug.LogWarning(
                    "[AlbumGroupRules] Rejected song because it does not belong to the selected group."
                );
                return;
            }

            List<singles._single> selectedSongs =
                GetField<List<singles._single>>(popup, "selectedSongs");

            if (selectedSongs == null)
                return;

            if (selectedSongs.Contains(song))
            {
                selectedSongs.Remove(song);
            }
            else
            {
                int maximumSongs =
                    GetMaximumSongs(popup);

                if (selectedSongs.Count >= maximumSongs)
                {
                    Debug.Log(
                        "[Album] Maximum " +
                        maximumSongs +
                        " songs for this release type."
                    );
                    return;
                }

                selectedSongs.Add(song);
            }

            RefreshPopup(popup);
        }

        // ---------------------------------------------------------------------
        // GROUP-SPECIFIC MEMBER LIST
        // ---------------------------------------------------------------------

        internal static void DrawMembers(AlbumPopup popup)
        {
            GameObject content = GetContent(popup);
            if (content == null)
                return;

            Groups._group group = GetSelectedGroup(popup);
            List<data_girls.girls> members = GetGroupMembers(group);
            int memberRows = Mathf.Max(1, Mathf.CeilToInt(members.Count / 2f));
            popup.EnsureContentHeight(
                Mathf.Max(470f, 90f + memberRows * 72f)
            );

            Dictionary<int, Image> memberBoxes =
                GetField<Dictionary<int, Image>>(popup, "memberBoxes");

            List<int> selectedMembers =
                GetField<List<int>>(popup, "selectedMembers");

            List<data_girls.girls> selectedGirls =
                GetField<List<data_girls.girls>>(popup, "selectedGirls");

            if (memberBoxes != null)
                memberBoxes.Clear();

            CreateText(
                content.transform,
                "MembersHeading",
                group == null
                    ? "Select Members"
                    : "Select " + group.Title + " Members",
                22,
                FontStyle.Bold,
                new Color(0.18f, 0.18f, 0.22f),
                new Vector2(55, -20),
                new Vector2(500, 40),
                TextAnchor.MiddleLeft
            );

            CreateText(
                content.transform,
                "MembersCount",
                "Selected: " +
                (selectedGirls != null ? selectedGirls.Count : 0) +
                " / " +
                MaximumMembers,
                13,
                FontStyle.Bold,
                new Color(0.39f, 0.34f, 0.72f),
                new Vector2(620, -20),
                new Vector2(180, 40),
                TextAnchor.MiddleRight
            );

            if (members.Count == 0)
            {
                CreateText(
                    content.transform,
                    "NoMembers",
                    group == null
                        ? "No group members are available."
                        : group.Title + " has no available members.",
                    13,
                    FontStyle.Italic,
                    new Color(0.50f, 0.49f, 0.56f),
                    new Vector2(60, -80),
                    new Vector2(650, 40),
                    TextAnchor.MiddleLeft
                );

                return;
            }

            for (int i = 0; i < members.Count; i++)
            {
                data_girls.girls girl = members[i];

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

                bool selected =
                    selectedGirls != null &&
                    selectedGirls.Contains(girl);

                Image bg = box.AddComponent<Image>();
                bg.color = selected
                    ? new Color(0.96f, 1f, 0.97f)
                    : Color.white;

                if (memberBoxes != null)
                    memberBoxes[i] = bg;

                Outline outline = box.AddComponent<Outline>();
                outline.effectColor = selected
                    ? new Color(0.38f, 0.76f, 0.50f)
                    : new Color(0.80f, 0.80f, 0.86f);
                outline.effectDistance = new Vector2(1.2f, -1.2f);

                Button btn = box.AddComponent<Button>();
                int index = i;
                btn.onClick.AddListener(
                    () => SelectMember(popup, index)
                );

                GameObject portrait = new GameObject("Portrait");
                portrait.transform.SetParent(box.transform, false);

                RectTransform pr = portrait.AddComponent<RectTransform>();
                pr.anchorMin = new Vector2(0, 0.5f);
                pr.anchorMax = new Vector2(0, 0.5f);
                pr.pivot = new Vector2(0, 0.5f);
                pr.sizeDelta = new Vector2(58, 58);
                pr.anchoredPosition = new Vector2(8, 0);

                Image portraitImage = portrait.AddComponent<Image>();

                try
                {
                    portraitImage.sprite = girl.texture.middle;
                }
                catch
                {
                    portraitImage.sprite = null;
                }

                portraitImage.preserveAspect = true;
                portraitImage.color = Color.white;

                CreateText(
                    box.transform,
                    "Name",
                    girl.GetName(true),
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
                    CreateText(
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

        internal static void SelectMember(AlbumPopup popup, int index)
        {
            if (popup == null)
                return;

            Groups._group group = GetSelectedGroup(popup);
            List<data_girls.girls> members = GetGroupMembers(group);

            if (index < 0 || index >= members.Count)
                return;

            data_girls.girls girl = members[index];

            List<int> selectedMembers =
                GetField<List<int>>(popup, "selectedMembers");

            List<data_girls.girls> selectedGirls =
                GetField<List<data_girls.girls>>(popup, "selectedGirls");

            data_girls.girls selectedCenterGirl =
                GetField<data_girls.girls>(popup, "selectedCenterGirl");

            if (selectedMembers == null || selectedGirls == null)
                return;

            if (selectedGirls.Contains(girl))
            {
                selectedGirls.Remove(girl);
                selectedMembers.Remove(index);

                if (selectedCenterGirl == girl)
                    SetField(popup, "selectedCenterGirl", null);
            }
            else
            {
                if (selectedGirls.Count >= MaximumMembers)
                {
                    Debug.Log("[Album] Maximum 8 members.");
                    return;
                }

                selectedGirls.Add(girl);

                if (!selectedMembers.Contains(index))
                    selectedMembers.Add(index);
            }

            RefreshPopup(popup);
        }

        // ---------------------------------------------------------------------
        // VALIDATION
        // ---------------------------------------------------------------------

        internal static bool ValidateInfoStep(
            AlbumPopup popup)
        {
            Groups._group group =
                GetSelectedGroup(popup);

            if (group == null)
            {
                Debug.LogWarning(
                    "[AlbumGroupRules] Select a group first."
                );

                return false;
            }

            AlbumReleaseKind kind =
                GetSelectedReleaseKind(popup);

            int minimum =
                AlbumReleaseRules.GetMinimumSongs(kind);

            int maximum =
                AlbumReleaseRules.GetMaximumSongs(kind);

            List<singles._single> released =
                GetReleasedSingles(group);

            if (released.Count < minimum)
            {
                int needed =
                    minimum - released.Count;

                Debug.LogWarning(
                    "[AlbumGroupRules] " +
                    group.Title +
                    " needs " +
                    needed +
                    " more released single" +
                    (needed == 1 ? "" : "s") +
                    " before making a " +
                    AlbumReleaseRules.GetDisplayName(
                        kind
                    ) +
                    "."
                );

                return false;
            }

            List<singles._single> selectedSongs =
                GetField<List<singles._single>>(
                    popup,
                    "selectedSongs"
                );

            int selectedCount =
                selectedSongs != null
                    ? selectedSongs.Count
                    : 0;

            if (selectedCount < minimum ||
                selectedCount > maximum)
            {
                Debug.LogWarning(
                    "[AlbumGroupRules] " +
                    AlbumReleaseRules.GetDisplayName(
                        kind
                    ) +
                    " requires " +
                    AlbumReleaseRules.GetRangeText(
                        kind
                    ) +
                    " songs."
                );

                return false;
            }

            foreach (singles._single song
                     in selectedSongs)
            {
                if (!released.Contains(song))
                {
                    Debug.LogWarning(
                        "[AlbumGroupRules] Every album song must be a released single by " +
                        group.Title +
                        "."
                    );

                    return false;
                }
            }

            return true;
        }

        // ---------------------------------------------------------------------
        // SAVE / RELEASE
        // ---------------------------------------------------------------------

        internal static bool SaveAlbum(
            AlbumPopup popup)
        {
            if (popup == null)
                return false;

            Groups._group group =
                GetSelectedGroup(popup);

            if (group == null)
            {
                Debug.LogWarning(
                    "[AlbumGroupRules] Album needs a group."
                );

                return false;
            }

            string albumTitle =
                popup.AlbumTitle;

            if (string.IsNullOrWhiteSpace(
                    albumTitle))
            {
                Debug.LogWarning(
                    "[Album] Album needs a title."
                );

                return false;
            }

            AlbumReleaseKind releaseKind =
                GetSelectedReleaseKind(popup);

            List<singles._single> selectedSongs =
                GetField<List<singles._single>>(
                    popup,
                    "selectedSongs"
                );

            List<data_girls.girls> selectedGirls =
                GetField<List<data_girls.girls>>(
                    popup,
                    "selectedGirls"
                );

            List<singles._single> releasedSingles =
                GetReleasedSingles(group);

            List<data_girls.girls> groupMembers =
                GetGroupMembers(group);

            int minimum =
                AlbumReleaseRules.GetMinimumSongs(
                    releaseKind
                );

            int maximum =
                AlbumReleaseRules.GetMaximumSongs(
                    releaseKind
                );

            if (releasedSingles.Count < minimum)
            {
                Debug.LogWarning(
                    "[AlbumGroupRules] " +
                    group.Title +
                    " does not have enough released singles for " +
                    AlbumReleaseRules.GetDisplayName(
                        releaseKind
                    ) +
                    "."
                );

                return false;
            }

            if (selectedSongs == null ||
                selectedSongs.Count < minimum ||
                selectedSongs.Count > maximum)
            {
                Debug.LogWarning(
                    "[Album] " +
                    AlbumReleaseRules.GetDisplayName(
                        releaseKind
                    ) +
                    " requires " +
                    AlbumReleaseRules.GetRangeText(
                        releaseKind
                    ) +
                    " songs."
                );

                return false;
            }

            foreach (singles._single song
                     in selectedSongs)
            {
                if (!releasedSingles.Contains(song))
                {
                    Debug.LogWarning(
                        "[AlbumGroupRules] Album contains a single that was not released by " +
                        group.Title +
                        "."
                    );

                    return false;
                }
            }

            if (selectedGirls == null ||
                selectedGirls.Count == 0)
            {
                Debug.LogWarning(
                    "[Album] Album needs at least one member."
                );

                return false;
            }

            foreach (data_girls.girls girl
                     in selectedGirls)
            {
                if (!groupMembers.Contains(girl))
                {
                    Debug.LogWarning(
                        "[AlbumGroupRules] Every selected member must belong to " +
                        group.Title +
                        "."
                    );

                    return false;
                }
            }

            return AlbumProductionManager.TryStart(
                popup,
                group,
                releaseKind,
                selectedSongs,
                selectedGirls
            );
        }

        // ---------------------------------------------------------------------
        // SMALL UI HELPERS
        // ---------------------------------------------------------------------

        internal static void CreateText(
            Transform parent,
            string name,
            string value,
            int fontSize,
            FontStyle fontStyle,
            Color color,
            Vector2 position,
            Vector2 size,
            TextAnchor alignment
        )
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            Text text = obj.AddComponent<Text>();
            text.font = AlbumUiResources.GetGameFont();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
        }

        private static void CreateMiniButton(
            Transform parent,
            string name,
            string label,
            Vector2 position,
            Vector2 size,
            UnityEngine.Events.UnityAction action
        )
        {
            GameObject obj = AlbumUiResources.InstantiateButton(
                parent, name, label, false, action);
            if (obj == null)
                return;

            RectTransform rect = obj.GetComponent<RectTransform>();
            if (rect == null)
                rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }
    }

    // =========================================================================
    // HARMONY PATCHES
    // =========================================================================

    [HarmonyPatch(typeof(AlbumPopup), "Open")]
    internal static class AlbumPopupOpenPatch
    {
        private static void Postfix(AlbumPopup __instance)
        {
            // The same method closes the popup if it is already open.
            GameObject panel =
                GroupAlbumRules.GetField<GameObject>(__instance, "panel");

            if (panel != null)
            {
                GroupAlbumRules.GetSelectedGroup(__instance);
                GroupAlbumRules.GetSelectedReleaseKind(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(AlbumPopup), "DrawInfo")]
    internal static class AlbumPopupDrawInfoPatch
    {
        private static void Postfix(AlbumPopup __instance)
        {
            GroupAlbumRules.DrawGroupSelector(__instance);
        }
    }

    [HarmonyPatch(typeof(AlbumPopup), "DrawSongSelector")]
    internal static class AlbumPopupDrawSongSelectorPatch
    {
        private static bool Prefix(AlbumPopup __instance)
        {
            GroupAlbumRules.DrawSongSelector(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(AlbumPopup), "DrawMembers")]
    internal static class AlbumPopupDrawMembersPatch
    {
        private static bool Prefix(AlbumPopup __instance)
        {
            GroupAlbumRules.DrawMembers(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(AlbumPopup), "SelectMember")]
    internal static class AlbumPopupSelectMemberPatch
    {
        private static bool Prefix(AlbumPopup __instance, int index)
        {
            GroupAlbumRules.SelectMember(__instance, index);
            return false;
        }
    }

    [HarmonyPatch(typeof(AlbumPopup), "GetCoverSubtitle")]
    internal static class AlbumPopupCoverSubtitlePatch
    {
        private static bool Prefix(
            AlbumPopup __instance,
            ref string __result
        )
        {
            Groups._group group =
                GroupAlbumRules.GetSelectedGroup(__instance);

            if (group == null ||
                string.IsNullOrWhiteSpace(group.Title))
            {
                __result = "GROUP";
            }
            else
            {
                __result = group.Title.ToUpperInvariant();
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(AlbumPopup), "Continue")]
    internal static class AlbumPopupContinuePatch
    {
        private static bool Prefix(AlbumPopup __instance)
        {
            object step =
                AccessTools.Field(typeof(AlbumPopup), "step")
                    .GetValue(__instance);

            // AlbumStep.Info is enum value 0.
            if (step != null &&
                Convert.ToInt32(step) == 0)
            {
                return GroupAlbumRules.ValidateInfoStep(__instance);
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(AlbumPopup), "DrawRelease")]
    internal static class AlbumPopupDrawReleasePatch
    {
        private static void Postfix(AlbumPopup __instance)
        {
            GameObject content =
                GroupAlbumRules.GetContent(__instance);

            Groups._group group =
                GroupAlbumRules.GetSelectedGroup(__instance);

            if (content == null || group == null)
                return;

            Transform summary = content.transform.Find("ReleaseSummary");

            if (summary != null)
            {
                Transform theme = summary.Find("Theme");

                if (theme != null)
                {
                    Text text = theme.GetComponent<Text>();

                    if (text != null)
                    {
                        AlbumReleaseKind kind =
                            GroupAlbumRules.GetSelectedReleaseKind(__instance);
                        text.text =
                            group.Title + "  •  " +
                            AlbumReleaseRules.GetShortLabel(kind) + "  •  " +
                            text.text;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(AlbumPopup), "DrawButtons")]
    internal static class AlbumPopupDrawButtonsProductionPatch
    {
        private static void Postfix(AlbumPopup __instance)
        {
            GameObject panel =
                GroupAlbumRules.GetField<GameObject>(__instance, "panel");
            if (panel == null)
                return;

            Transform create = panel.transform.Find("Create");
            if (create != null)
                AlbumUiResources.SetButtonLabel(create.gameObject, "Start Production");
        }
    }

    [HarmonyPatch(typeof(AlbumPopup), "Save")]
    internal static class AlbumPopupSavePatch
    {
        private static bool Prefix(AlbumPopup __instance)
        {
            // Fully replace the original Save method so the main group's name
            // can never overwrite the selected subgroup's name.
            GroupAlbumRules.SaveAlbum(__instance);
            return false;
        }
    }
}
