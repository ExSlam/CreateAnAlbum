using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Albummodelite;

namespace CreateAnAlbumGroupRules
{
    internal enum AlbumReleaseKind
    {
        MiniAlbum = 0,
        EP = 1,
        LP = 2
    }

    internal static class AlbumReleaseRules
    {
        internal static int GetMinimumSongs(AlbumReleaseKind kind)
        {
            switch (kind)
            {
                case AlbumReleaseKind.LP: return 10;
                default: return 6;
            }
        }

        internal static int GetMaximumSongs(AlbumReleaseKind kind)
        {
            switch (kind)
            {
                case AlbumReleaseKind.MiniAlbum: return 6;
                case AlbumReleaseKind.EP: return 10;
                case AlbumReleaseKind.LP: return 15;
                default: return 10;
            }
        }

        internal static string GetDisplayName(AlbumReleaseKind kind)
        {
            switch (kind)
            {
                case AlbumReleaseKind.MiniAlbum: return "Mini Album";
                case AlbumReleaseKind.EP: return "EP (Extended Play)";
                case AlbumReleaseKind.LP: return "Album (LP / Long Play)";
                default: return "Album";
            }
        }

        internal static string GetShortLabel(AlbumReleaseKind kind)
        {
            switch (kind)
            {
                case AlbumReleaseKind.MiniAlbum: return "Mini Album";
                case AlbumReleaseKind.EP: return "EP";
                case AlbumReleaseKind.LP: return "LP";
                default: return "Album";
            }
        }

        internal static string GetRangeText(AlbumReleaseKind kind)
        {
            int min = GetMinimumSongs(kind);
            int max = GetMaximumSongs(kind);
            return min == max ? min.ToString() : min + "–" + max;
        }
    }

    [Serializable]
    internal class AlbumProductionProject
    {
        public string SaveId = "";
        public string Title = "";
        public string GroupName = "";
        public int GroupId = -1;
        public int ReleaseKind;
        public List<int> SongIds = new List<int>();
        public List<int> MemberIds = new List<int>();
        public int CenterMemberId = -1;
        public long StartTicks;
        public string Theme = "";
        public int ThemeIndex;
        public int BackgroundIndex;
        public string BackgroundKey = "";
        public int LayoutIndex;
        public int FontIndex;
        public string FontKey = "";
        public int TextColorIndex;
        public int TitlePosition;
        public bool ShowGroupName;
        public int OrnamentStyle;
        public int FrameStyle;
        public int TitleEffect;
        public float PortraitScale = 1f;
        public float CenterEmphasis = 1.08f;
        public float PortraitYOffset;
        public float PortraitSpacing = 1f;
        public float EffectsIntensity = 1f;
    }

    internal static class AlbumProductionManager
    {
        internal const long ProductionCost = 500000L;
        private static readonly int[] StageDurations = { 3, 4, 3, 2 };
        private static readonly string[] StageNames =
        {
            "Pre-Production & Writing",
            "Production & Recording",
            "Post-Production",
            "Release & Distribution"
        };
        private static readonly string[] StageDescriptions =
        {
            "Lyrics • arrangements • album planning",
            "Tracking • vocals • instrumentation",
            "Editing • mixing • mastering",
            "Manufacturing • marketing • distribution"
        };

        private static AlbumProductionProject activeProject;
        private static string loadedSaveId = "";

        internal static bool HasActiveProject
        {
            get { return activeProject != null; }
        }

        internal static AlbumProductionProject GetProjectForSave()
        {
            return activeProject;
        }

        internal static void RebindSaveId(string saveId)
        {
            loadedSaveId = saveId ?? string.Empty;
            if (activeProject != null)
                activeProject.SaveId = loadedSaveId;
        }

        internal static void RestoreFromSave(AlbumProductionProject project, string saveId)
        {
            activeProject = project;
            loadedSaveId = saveId ?? "";
            if (activeProject != null)
            {
                activeProject.SaveId = loadedSaveId;
                Debug.Log("[AlbumProduction] Restored active project: " + activeProject.Title);
            }
        }

        [Serializable]
        private sealed class LegacyProductionFile
        {
            public AlbumProductionProject Project = null;
        }

        internal static bool TryLoadLegacyProject(string legacySaveId, out AlbumProductionProject project)
        {
            project = null;
            if (string.IsNullOrEmpty(legacySaveId))
                return false;

            try
            {
                string gameRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string directory = Path.Combine(gameRoot, "BepInEx", "config", "CreateAnAlbum", "Production");
                if (!Directory.Exists(directory))
                    return false;

                string safeId = legacySaveId;
                foreach (char invalid in Path.GetInvalidFileNameChars())
                    safeId = safeId.Replace(invalid, '_');

                string preferred = Path.Combine(directory, "album_production_" + safeId + ".json");
                string[] candidates = File.Exists(preferred)
                    ? new[] { preferred }
                    : Directory.GetFiles(directory, "album_production_*.json");

                foreach (string path in candidates)
                {
                    LegacyProductionFile file = JsonUtility.FromJson<LegacyProductionFile>(File.ReadAllText(path));
                    if (file == null || file.Project == null)
                        continue;
                    if (!string.Equals(file.Project.SaveId, legacySaveId, StringComparison.Ordinal) &&
                        !string.Equals(path, preferred, StringComparison.OrdinalIgnoreCase))
                        continue;

                    project = file.Project;
                    if (string.IsNullOrEmpty(project.FontKey))
                        project.FontKey = AlbumFontCatalog.GetKey(project.FontIndex);
                    if (string.IsNullOrEmpty(project.BackgroundKey))
                        project.BackgroundKey = AlbumBackgroundCatalog.GetLegacyKey(project.BackgroundIndex);
                    Debug.Log("[AlbumProduction] Migrated legacy per-save production project: " + project.Title);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AlbumProduction] Legacy production migration failed: " + ex.Message);
            }

            return false;
        }

        internal static void Shutdown()
        {
            activeProject = null;
            loadedSaveId = "";
        }

        internal static void Tick()
        {
            if (activeProject != null)
                TryCompleteIfReady();
        }

        internal static bool TryOpenExisting(bool queueBehindCurrentPopup = false)
        {
            if (activeProject == null)
                return false;

            TryCompleteIfReady();
            if (activeProject == null)
                return false;

            return OpenDashboard(queueBehindCurrentPopup);
        }

        internal static bool TryStart(
            AlbumPopup popup,
            Groups._group group,
            AlbumReleaseKind releaseKind,
            List<singles._single> songs,
            List<data_girls.girls> members)
        {
            if (popup == null || group == null || songs == null || members == null)
                return false;

            if (activeProject != null)
            {
                Notify("An album is already in production.", false);
                OpenDashboard(false);
                return false;
            }

            string saveId = AlbumPersistence.CurrentSaveId;
            if (string.IsNullOrEmpty(saveId))
            {
                Notify("The current save is not ready yet.", false);
                return false;
            }

            // Snapshot the complete project before charging funds. If a future cover-field
            // migration or reflection lookup ever fails, the player must not lose ¥500,000
            // without receiving a production project.
            AlbumProductionProject project = new AlbumProductionProject();
            project.SaveId = saveId;
            project.Title = popup.AlbumTitle.Trim();
            project.GroupName = group.Title;
            project.GroupId = group.ID;
            project.ReleaseKind = (int)releaseKind;
            project.StartTicks = staticVars.dateTime.Date.Ticks;

            foreach (singles._single song in songs)
                if (song != null) project.SongIds.Add(song.id);
            foreach (data_girls.girls member in members)
                if (member != null) project.MemberIds.Add(member.id);

            data_girls.girls center = GroupAlbumRules.GetField<data_girls.girls>(popup, "selectedCenterGirl");
            project.CenterMemberId = center != null ? center.id : -1;

            string[] themeNames = GroupAlbumRules.GetField<string[]>(popup, "themeNames");
            int selectedTheme = GroupAlbumRules.GetField<int>(popup, "selectedTheme");
            project.Theme = themeNames != null && selectedTheme >= 0 && selectedTheme < themeNames.Length
                ? themeNames[selectedTheme]
                : "";
            project.ThemeIndex = selectedTheme;
            project.BackgroundIndex = GroupAlbumRules.GetField<int>(popup, "selectedBackground");
            project.BackgroundKey = GroupAlbumRules.GetField<string>(popup, "selectedBackgroundKey");
            if (string.IsNullOrEmpty(project.BackgroundKey))
                project.BackgroundKey = AlbumBackgroundCatalog.GetKey(project.BackgroundIndex);
            project.LayoutIndex = GroupAlbumRules.GetField<int>(popup, "selectedLayout");
            project.FontIndex = GroupAlbumRules.GetField<int>(popup, "selectedFont");
            project.FontKey = AlbumFontCatalog.GetKey(project.FontIndex);
            project.TextColorIndex = GroupAlbumRules.GetField<int>(popup, "selectedTextColor");
            project.TitlePosition = GroupAlbumRules.GetField<int>(popup, "titlePosition");
            project.ShowGroupName = GroupAlbumRules.GetField<bool>(popup, "showGroupName");
            project.OrnamentStyle = GroupAlbumRules.GetField<int>(popup, "ornamentStyle");
            project.FrameStyle = GroupAlbumRules.GetField<int>(popup, "frameStyle");
            project.TitleEffect = GroupAlbumRules.GetField<int>(popup, "titleEffect");
            project.PortraitScale = GroupAlbumRules.GetField<float>(popup, "portraitScale");
            project.CenterEmphasis = GroupAlbumRules.GetField<float>(popup, "centerEmphasis");
            project.PortraitYOffset = GroupAlbumRules.GetField<float>(popup, "portraitYOffset");
            project.PortraitSpacing = GroupAlbumRules.GetField<float>(popup, "portraitSpacing");
            project.EffectsIntensity = GroupAlbumRules.GetField<float>(popup, "effectsIntensity");

            string moneyError;
            if (!GameEconomyBridge.TrySpendMoney(ProductionCost, out moneyError))
            {
                Notify(moneyError, false);
                return false;
            }

            activeProject = project;
            loadedSaveId = saveId;
            AlbumPersistence.MarkDirty();

            BuildDashboard();
            GroupAlbumRules.ResetPopupState(popup);
            if (!AlbumPopupHost.Transition(AlbumPopupKind.Create, AlbumPopupKind.Production))
            {
                AlbumPopupHost.Close(AlbumPopupKind.Create, delegate { OpenDashboard(false); });
            }

            Notify(
                AlbumReleaseRules.GetShortLabel(releaseKind) + " production started: " + project.Title +
                " (¥" + ProductionCost.ToString("N0") + ")",
                true);
            Debug.Log("[AlbumProduction] Started " + project.Title + " for " + project.GroupName + ".");
            return true;
        }

        private static void TryCompleteIfReady()
        {
            if (activeProject == null || activeProject.StartTicks <= 0L)
                return;

            DateTime start;
            try { start = new DateTime(activeProject.StartTicks).Date; }
            catch { return; }

            if ((staticVars.dateTime.Date - start).TotalDays < StageDurations.Sum())
                return;

            ReleaseActiveProject();
        }

        private static void ReleaseActiveProject()
        {
            AlbumProductionProject project = activeProject;
            if (project == null)
                return;

            List<singles._single> songs = new List<singles._single>();
            foreach (int id in project.SongIds)
            {
                try
                {
                    singles._single song = singles.GetSingleByID(id);
                    if (song != null && !songs.Contains(song)) songs.Add(song);
                }
                catch { }
            }

            AlbumReleaseKind kind = (AlbumReleaseKind)project.ReleaseKind;
            int minimum = AlbumReleaseRules.GetMinimumSongs(kind);
            if (songs.Count < minimum)
            {
                Debug.LogWarning("[AlbumProduction] Release ready but only " + songs.Count + "/" + minimum + " tracks restored.");
                return;
            }

            List<data_girls.girls> members = new List<data_girls.girls>();
            foreach (int id in project.MemberIds)
            {
                try
                {
                    data_girls.girls girl = data_girls.GetGirlByID(id);
                    if (girl != null && !members.Contains(girl)) members.Add(girl);
                }
                catch { }
            }
            if (members.Count == 0)
            {
                Debug.LogWarning("[AlbumProduction] Release ready but no saved members restored.");
                return;
            }

            AlbumData album = new AlbumData();
            album.ID = GenerateAlbumId();
            album.Title = project.Title;
            album.GroupName = project.GroupName;
            album.ReleaseDate = staticVars.dateTime;
            album.Released = true;
            album.PlayerAlbum = true;
            album.ReleaseKind = project.ReleaseKind;
            album.Songs = new List<singles._single>(songs);
            album.Members = new List<data_girls.girls>(members);
            album.Profit = -ProductionCost;
            album.Theme = project.Theme;
            album.ThemeIndex = project.ThemeIndex;
            album.BackgroundIndex = project.BackgroundIndex;
            album.BackgroundKey = project.BackgroundKey;
            album.LayoutIndex = project.LayoutIndex;
            album.FontIndex = project.FontIndex;
            album.FontKey = project.FontKey;
            album.TextColorIndex = project.TextColorIndex;
            album.TitlePosition = project.TitlePosition;
            album.ShowGroupName = project.ShowGroupName;
            album.OrnamentStyle = project.OrnamentStyle;
            album.FrameStyle = project.FrameStyle;
            album.TitleEffect = project.TitleEffect;
            album.PortraitScale = project.PortraitScale <= 0f ? 1f : project.PortraitScale;
            album.CenterEmphasis = project.CenterEmphasis <= 0f ? 1.08f : project.CenterEmphasis;
            album.PortraitYOffset = project.PortraitYOffset;
            album.PortraitSpacing = project.PortraitSpacing <= 0f ? 1f : project.PortraitSpacing;
            album.EffectsIntensity = project.EffectsIntensity <= 0f ? 1f : project.EffectsIntensity;
            album.CenterMemberIndex = members.FindIndex(g => g != null && g.id == project.CenterMemberId);

            Albums.AddAlbum(album);
            AlbumSalesManager.RegisterNewAlbum(album);
            AlbumDebutRewards.TryAward(album);

            activeProject = null;
            AlbumPersistence.MarkDirty();
            AlbumPopupHost.Close(AlbumPopupKind.Production);
            Notify("Released: " + album.Title + " • debut sales " + album.WeeklySales.ToString("N0"), true);
            Debug.Log("[AlbumProduction] RELEASED: " + album.Title + " | " + AlbumReleaseRules.GetDisplayName(kind));
        }

        private static int GenerateAlbumId()
        {
            int highest = 0;
            if (Albums.AlbumList != null)
            {
                foreach (AlbumData album in Albums.AlbumList)
                    if (album != null && album.ID > highest) highest = album.ID;
            }
            return highest + 1;
        }

        private static bool OpenDashboard(bool queueBehindCurrentPopup)
        {
            if (activeProject == null)
                return false;
            if (!BuildDashboard())
                return false;
            return AlbumPopupHost.Open(AlbumPopupKind.Production, queueBehindCurrentPopup);
        }

        private static bool BuildDashboard()
        {
            if (activeProject == null)
                return false;

            GameObject root = AlbumPopupHost.Prepare(AlbumPopupKind.Production);
            if (root == null)
                return AlbumPopupHost.IsOpenQueuedOrClosing(AlbumPopupKind.Production);

            GameObject panel = new GameObject("AlbumProductionDashboard");
            panel.transform.SetParent(root.transform, false);
            RectTransform pr = panel.AddComponent<RectTransform>();
            pr.anchorMin = new Vector2(0.5f, 0.5f);
            pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(700f, 520f);
            pr.anchoredPosition = Vector2.zero;
            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.97f, 0.97f, 0.985f, 1f);
            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.76f, 0.74f, 0.86f);
            outline.effectDistance = new Vector2(2f, -2f);

            CreateText(panel.transform, "Heading", "ALBUM PRODUCTION", 23, FontStyle.Bold,
                new Color(0.39f, 0.34f, 0.72f), new Vector2(32f, -20f), new Vector2(450f, 38f), TextAnchor.MiddleLeft);
            CreateText(panel.transform, "Project", activeProject.Title, 19, FontStyle.Bold,
                new Color(0.20f, 0.20f, 0.24f), new Vector2(32f, -66f), new Vector2(500f, 32f), TextAnchor.MiddleLeft);

            AlbumReleaseKind kind = (AlbumReleaseKind)activeProject.ReleaseKind;
            CreateText(panel.transform, "Info",
                activeProject.GroupName + "  •  " + AlbumReleaseRules.GetDisplayName(kind) + "  •  " + activeProject.SongIds.Count + " tracks",
                11, FontStyle.Bold, new Color(0.44f, 0.42f, 0.65f), new Vector2(32f, -98f), new Vector2(600f, 24f), TextAnchor.MiddleLeft);
            CreateText(panel.transform, "Cost", "Production Cost  ¥500,000", 12, FontStyle.Bold,
                new Color(0.34f, 0.58f, 0.40f), new Vector2(480f, -24f), new Vector2(185f, 30f), TextAnchor.MiddleRight);

            DateTime start = new DateTime(activeProject.StartTicks).Date;
            double elapsed = Math.Max(0d, (staticVars.dateTime.Date - start).TotalDays);
            int cumulative = 0;
            for (int i = 0; i < StageNames.Length; i++)
            {
                int duration = StageDurations[i];
                double stageElapsed = elapsed - cumulative;
                float progress = stageElapsed >= duration ? 1f : stageElapsed >= 0d ? Mathf.Clamp01((float)(stageElapsed / duration)) : 0f;
                string status = progress >= 1f ? "COMPLETE" : stageElapsed >= 0d ? "IN PROGRESS" : "UP NEXT";
                DrawStage(panel.transform, i, StageNames[i], StageDescriptions[i], status, progress);
                cumulative += duration;
            }

            DateTime expected = start.AddDays(StageDurations.Sum());
            CreateText(panel.transform, "Expected", "Expected release: " + expected.ToString("MMM d, yyyy"),
                11, FontStyle.Bold, new Color(0.45f, 0.46f, 0.68f), new Vector2(32f, -458f), new Vector2(360f, 25f), TextAnchor.MiddleLeft);

            GameObject close = AlbumUiResources.InstantiateButton(
                panel.transform,
                "Close",
                "Close",
                AlbumButtonStyle.Destructive,
                delegate { AlbumPopupHost.Close(AlbumPopupKind.Production); });
            if (close != null)
            {
                RectTransform cr = close.GetComponent<RectTransform>() ?? close.AddComponent<RectTransform>();
                cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(0f, 1f);
                cr.sizeDelta = new Vector2(150f, 32f);
                cr.anchoredPosition = new Vector2(515f, -452f);
            }

            return true;
        }

        private static void DrawStage(Transform parent, int index, string title, string subtitle, string status, float progress)
        {
            GameObject card = new GameObject("Stage_" + index);
            card.transform.SetParent(parent, false);
            RectTransform r = card.AddComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            r.sizeDelta = new Vector2(636f, 66f);
            r.anchoredPosition = new Vector2(32f, -132f - index * 76f);
            Image bg = card.AddComponent<Image>();
            bg.color = Color.white;
            Outline outline = card.AddComponent<Outline>();
            outline.effectColor = new Color(0.83f, 0.82f, 0.89f);

            CreateText(card.transform, "Number", (index + 1).ToString(), 17, FontStyle.Bold,
                new Color(0.43f, 0.40f, 0.73f), new Vector2(14f, -9f), new Vector2(35f, 34f), TextAnchor.MiddleCenter);
            CreateText(card.transform, "Title", title, 12, FontStyle.Bold,
                new Color(0.27f, 0.27f, 0.32f), new Vector2(58f, -8f), new Vector2(350f, 20f), TextAnchor.MiddleLeft);
            CreateText(card.transform, "Subtitle", subtitle, 9, FontStyle.Normal,
                new Color(0.52f, 0.51f, 0.58f), new Vector2(58f, -31f), new Vector2(360f, 18f), TextAnchor.MiddleLeft);
            CreateText(card.transform, "Status", status, 9, FontStyle.Bold,
                progress >= 1f ? new Color(0.28f, 0.65f, 0.39f) : new Color(0.43f, 0.40f, 0.73f),
                new Vector2(465f, -8f), new Vector2(140f, 20f), TextAnchor.MiddleRight);

            GameObject bar = new GameObject("ProgressBar");
            bar.transform.SetParent(card.transform, false);
            RectTransform br = bar.AddComponent<RectTransform>();
            br.anchorMin = br.anchorMax = br.pivot = new Vector2(0f, 1f);
            br.sizeDelta = new Vector2(140f, 8f);
            br.anchoredPosition = new Vector2(465f, -39f);
            bar.AddComponent<Image>().color = new Color(0.86f, 0.85f, 0.90f);

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(bar.transform, false);
            RectTransform fr = fill.AddComponent<RectTransform>();
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = new Vector2(progress, 1f);
            fr.offsetMin = fr.offsetMax = Vector2.zero;
            fill.AddComponent<Image>().color = progress >= 1f
                ? new Color(0.32f, 0.72f, 0.43f)
                : new Color(0.43f, 0.45f, 0.80f);
        }

        private static void CreateText(Transform parent, string name, string value, int size, FontStyle style,
            Color color, Vector2 position, Vector2 dimensions, TextAnchor alignment)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform r = obj.AddComponent<RectTransform>();
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = position;
            r.sizeDelta = dimensions;
            Text text = obj.AddComponent<Text>();
            text.font = AlbumUiResources.GetGameFont();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
        }

        private static void Notify(string message, bool positive)
        {
            try
            {
                NotificationManager.AddNotification(
                    message,
                    positive ? mainScript.green32 : mainScript.red32,
                    NotificationManager._notification._type.other);
            }
            catch { Debug.Log("[AlbumProduction] " + message); }
        }
    }

    internal static class GameEconomyBridge
    {
        internal static bool TrySpendMoney(long amount, out string message)
        {
            message = "";
            try
            {
                long balance = resources.Money();
                if (balance < amount)
                {
                    message = "Production costs ¥" + amount.ToString("N0") + ". Current funds: ¥" + balance.ToString("N0") + ".";
                    return false;
                }

                mainScript main = Camera.main != null ? Camera.main.GetComponent<mainScript>() : null;
                resources economy = main != null && main.Data != null ? main.Data.GetComponent<resources>() : null;
                if (economy == null)
                {
                    message = "Idol Manager's economy system is not ready.";
                    return false;
                }
                economy.AddMoney(-amount);
                return true;
            }
            catch (Exception ex)
            {
                message = "Could not charge the production cost: " + ex.Message;
                return false;
            }
        }
    }
}
