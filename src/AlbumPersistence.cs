using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Albummodelite
{
    public static class AlbumPersistence
    {
        private static bool initialized;
        private static bool dirty;

        [Serializable]
        private class AlbumSaveFile
        {
            public int Version = 1;
            public List<AlbumSaveEntry> Albums =
                new List<AlbumSaveEntry>();
        }

        [Serializable]
        private class AlbumSaveEntry
        {
            public int ID;
            public string Title = "";
            public string GroupName = "";

            public long ReleaseTicks;
            public bool Released;

            public bool PlayerAlbum;
            public string RivalName = "";
            public int RivalGroupId = -1;

            public List<int> MemberIds =
                new List<int>();

            public List<int> SongIds =
                new List<int>();

            public long Sales;
            public long WeeklySales;
            public long Profit;

            public int ChartPosition;
            public int PreviousChartPosition;
            public int PeakChartPosition;
            public int WeeksOnChart;

            public string CoverPath = "";
            public string Theme = "";

            public int ThemeIndex;
            public int BackgroundIndex;
            public int LayoutIndex;
            public int FontIndex;
            public int TextColorIndex;
            public int TitlePosition;

            public bool ShowGroupName;

            public int OrnamentStyle;
            public int FrameStyle;
            public int TitleEffect;

            public float PortraitScale;
            public float CenterEmphasis;
            public float PortraitYOffset;
            public float PortraitSpacing;
            public float EffectsIntensity;

            public int CenterMemberIndex;
        }

        private static bool pendingLoad;
        private static string pendingCandidateId = "";
        private static int pendingStableFrames;
        private static string loadedSaveId = "";

        public static string CurrentSaveId
        {
            get
            {
                if (!initialized)
                    return "";

                return GetSaveId();
            }
        }

        public static bool IsCurrentSaveLoaded
        {
            get
            {
                if (!initialized)
                    return false;

                string current = GetSaveId();

                return
                    !string.IsNullOrEmpty(current) &&
                    string.Equals(
                        loadedSaveId,
                        current,
                        StringComparison.Ordinal
                    );
            }
        }

        public static void Initialize()
        {
            Debug.Log(
                "[AlbumSave] *** PERSISTENCE v1.5 INITIALIZED ***"
            );

            SaveManager.SaveEvent -= OnGameSave;
            SaveManager.LoadEvent -= OnGameLoad;

            SaveManager.SaveEvent += OnGameSave;
            SaveManager.LoadEvent += OnGameLoad;

            initialized = true;
            dirty = false;
            pendingLoad = false;
            pendingCandidateId = "";
            pendingStableFrames = 0;
            loadedSaveId = "";

            Debug.Log(
                "[AlbumSave] Save/load events registered."
            );
        }

        public static void Shutdown()
        {
            if (!initialized)
                return;

            SaveManager.SaveEvent -= OnGameSave;
            SaveManager.LoadEvent -= OnGameLoad;

            initialized = false;
            dirty = false;
            pendingLoad = false;
            pendingCandidateId = "";
            pendingStableFrames = 0;
            loadedSaveId = "";
        }

        private static void OnGameSave()
        {
            Debug.Log(
                "[AlbumSave] Idol Manager save detected."
            );

            FlushDirty();
        }

        private static void OnGameLoad()
        {
            if (!initialized)
                return;

            pendingLoad = true;
            pendingCandidateId = "";
            pendingStableFrames = 0;
            loadedSaveId = "";

            Debug.Log(
                "[AlbumSave] Idol Manager load detected. " +
                "Waiting for the selected save context to stabilize."
            );
        }

        public static void Tick()
        {
            if (!initialized || !pendingLoad)
                return;

            string candidate = GetSaveId();

            if (string.IsNullOrEmpty(candidate))
                return;

            if (candidate == pendingCandidateId)
            {
                pendingStableFrames++;
            }
            else
            {
                pendingCandidateId = candidate;
                pendingStableFrames = 1;
            }

            if (pendingStableFrames < 10)
                return;

            Debug.Log(
                "[AlbumSave] Stable save context: " +
                candidate
            );

            LoadForSaveId(candidate);

            pendingLoad = false;
            pendingCandidateId = "";
            pendingStableFrames = 0;
        }

        public static bool EnsureCurrentSaveLoaded()
        {
            if (!initialized)
                return false;

            string current = GetSaveId();

            if (string.IsNullOrEmpty(current))
                return false;

            if (string.Equals(
                    loadedSaveId,
                    current,
                    StringComparison.Ordinal))
            {
                Albums.DeduplicateInPlace();
                return true;
            }

            bool result =
                LoadForSaveId(current);

            if (result)
            {
                pendingLoad = false;
                pendingCandidateId = "";
                pendingStableFrames = 0;
            }

            return result;
        }

        public static void MarkDirty()
        {
            if (initialized)
                dirty = true;
        }

        // Compatibility surface for older Album code. A direct Save request now marks the
        // sidecar dirty; only Idol Manager's real SaveEvent commits it to disk.
        public static void Save()
        {
            MarkDirty();
        }

        private static void FlushDirty()
        {
            if (!initialized || !dirty)
                return;

            try
            {
                string saveId = GetSaveId();

                if (string.IsNullOrEmpty(saveId))
                {
                    Debug.LogWarning(
                        "[AlbumSave] Save blocked: no stable save ID."
                    );
                    return;
                }

                if (pendingLoad)
                {
                    Debug.LogWarning(
                        "[AlbumSave] Save deferred during load transition."
                    );
                    return;
                }

                if (!string.Equals(
                        loadedSaveId,
                        saveId,
                        StringComparison.Ordinal))
                {
                    Debug.LogWarning(
                        "[AlbumSave] Save blocked because album data for " +
                        saveId +
                        " has not been loaded yet."
                    );
                    return;
                }

                Albums.DeduplicateInPlace();

                string path =
                    GetPathForSaveId(saveId);

                if (Albums.AlbumList.Count == 0 &&
                    ExistingFileHasAlbums(path))
                {
                    Debug.LogError(
                        "[AlbumSave] EMPTY SAVE BLOCKED. " +
                        "Existing album data was preserved: " +
                        path
                    );
                    return;
                }

                AlbumSaveFile file =
                    new AlbumSaveFile();

                foreach (AlbumData album in Albums.AlbumList)
                {
                    if (album == null)
                        continue;

                    file.Albums.Add(
                        ToSaveEntry(album)
                    );
                }

                string json =
                    JsonUtility.ToJson(
                        file,
                        true
                    );

                WriteAllTextAtomic(path, json);
                dirty = false;

                Debug.Log(
                    "[AlbumSave] Saved " +
                    file.Albums.Count +
                    " unique album(s)."
                );

                Debug.Log(
                    "[AlbumSave] File: " +
                    path
                );
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[AlbumSave] Save failed:\n" +
                    ex
                );
            }
        }

        public static void Load()
        {
            if (!initialized)
                return;

            LoadForSaveId(
                GetSaveId()
            );
        }

        private static bool LoadForSaveId(
            string saveId
        )
        {
            if (!initialized ||
                string.IsNullOrEmpty(saveId))
            {
                return false;
            }

            try
            {
                string path =
                    GetPathForSaveId(saveId);

                TryMigrateLegacySidecar(saveId, path);

                if (!File.Exists(path))
                {
                    Albums.AlbumList.Clear();
                    loadedSaveId = saveId;
                    dirty = false;
                    RivalAlbumManager.RebuildIdAllocator();

                    Debug.Log(
                        "[AlbumSave] No album save found for " +
                        saveId +
                        ". Starting with 0 albums."
                    );

                    Debug.Log(
                        "[AlbumSave] Expected file: " +
                        path
                    );

                    return true;
                }

                string json =
                    File.ReadAllText(path);

                AlbumSaveFile file =
                    JsonUtility.FromJson<AlbumSaveFile>(
                        json
                    );

                if (file == null ||
                    file.Albums == null)
                {
                    Debug.LogWarning(
                        "[AlbumSave] Save file was empty or invalid."
                    );

                    Albums.AlbumList.Clear();
                    loadedSaveId = saveId;
                    dirty = false;
                    RivalAlbumManager.RebuildIdAllocator();
                    return true;
                }

                List<AlbumData> restoredAlbums =
                    new List<AlbumData>();

                HashSet<string> seen =
                    new HashSet<string>();

                int duplicatesSkipped = 0;

                foreach (AlbumSaveEntry entry in file.Albums)
                {
                    if (entry == null)
                        continue;

                    string key =
                        GetSaveEntryIdentityKey(entry);

                    if (!seen.Add(key))
                    {
                        duplicatesSkipped++;
                        continue;
                    }

                    AlbumData album =
                        FromSaveEntry(entry);

                    if (album != null)
                    {
                        restoredAlbums.Add(
                            album
                        );
                    }
                }

                Albums.AlbumList.Clear();
                Albums.AlbumList.AddRange(
                    restoredAlbums
                );

                Albums.DeduplicateInPlace();

                loadedSaveId = saveId;
                dirty = false;
                RivalAlbumManager.RebuildIdAllocator();

                Debug.Log(
                    "[AlbumSave] Loaded " +
                    Albums.AlbumList.Count +
                    " unique album(s) from " +
                    saveId +
                    "."
                );

                if (duplicatesSkipped > 0)
                {
                    Debug.LogWarning(
                        "[AlbumSave] Skipped " +
                        duplicatesSkipped +
                        " duplicate saved album entr" +
                        (duplicatesSkipped == 1
                            ? "y."
                            : "ies.")
                    );

                    MarkDirty();
                }

                Debug.Log(
                    "[AlbumSave] File: " +
                    path
                );

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[AlbumSave] Load failed:\n" +
                    ex
                );

                return false;
            }
        }

        private static string GetPathForSaveId(
            string saveId
        )
        {
            return Path.Combine(
                GetStorageDirectory(),
                "albums_" +
                saveId +
                ".json"
            );
        }

        private static bool ExistingFileHasAlbums(
            string path
        )
        {
            try
            {
                if (!File.Exists(path))
                    return false;

                string json =
                    File.ReadAllText(path);

                AlbumSaveFile file =
                    JsonUtility.FromJson<AlbumSaveFile>(
                        json
                    );

                return
                    file != null &&
                    file.Albums != null &&
                    file.Albums.Count > 0;
            }
            catch
            {
                return true;
            }
        }

        private static string GetSaveEntryIdentityKey(
            AlbumSaveEntry entry
        )
        {
            if (entry == null)
                return "";

            if (entry.ID != 0)
            {
                return
                    (entry.PlayerAlbum ? "P:" : "R:") +
                    entry.ID;
            }

            string owner =
                entry.PlayerAlbum
                    ? (entry.GroupName ?? "")
                    : (!string.IsNullOrEmpty(entry.RivalName)
                        ? entry.RivalName
                        : (entry.GroupName ?? ""));

            return
                (entry.PlayerAlbum ? "P:" : "R:") +
                owner + "|" +
                (entry.Title ?? "") + "|" +
                entry.ReleaseTicks;
        }

        private static AlbumSaveEntry ToSaveEntry(
            AlbumData album
        )
        {
            AlbumSaveEntry entry =
                new AlbumSaveEntry();

            entry.ID = album.ID;
            entry.Title = album.Title ?? "";
            entry.GroupName =
                album.GroupName ?? "";

            entry.ReleaseTicks =
                album.ReleaseDate.Ticks;

            entry.Released =
                album.Released;

            entry.PlayerAlbum =
                album.PlayerAlbum;

            entry.RivalName =
                album.RivalName ?? "";

            entry.RivalGroupId =
                album.RivalGroupId;

            if (album.Members != null)
            {
                foreach (
                    data_girls.girls girl
                    in album.Members
                )
                {
                    if (girl != null)
                    {
                        entry.MemberIds.Add(
                            girl.id
                        );
                    }
                }
            }

            if (album.Songs != null)
            {
                foreach (
                    singles._single single
                    in album.Songs
                )
                {
                    if (single != null)
                    {
                        entry.SongIds.Add(
                            single.id
                        );
                    }
                }
            }

            entry.Sales =
                album.Sales;

            entry.WeeklySales =
                album.WeeklySales;

            entry.Profit =
                album.Profit;

            entry.ChartPosition =
                album.ChartPosition;

            entry.PreviousChartPosition =
                album.PreviousChartPosition;

            entry.PeakChartPosition =
                album.PeakChartPosition;

            entry.WeeksOnChart =
                album.WeeksOnChart;

            entry.CoverPath =
                album.CoverPath ?? "";

            entry.Theme =
                album.Theme ?? "";

            entry.ThemeIndex =
                album.ThemeIndex;

            entry.BackgroundIndex =
                album.BackgroundIndex;

            entry.LayoutIndex =
                album.LayoutIndex;

            entry.FontIndex =
                album.FontIndex;

            entry.TextColorIndex =
                album.TextColorIndex;

            entry.TitlePosition =
                album.TitlePosition;

            entry.ShowGroupName =
                album.ShowGroupName;

            entry.OrnamentStyle =
                album.OrnamentStyle;

            entry.FrameStyle =
                album.FrameStyle;

            entry.TitleEffect =
                album.TitleEffect;

            entry.PortraitScale =
                album.PortraitScale;

            entry.CenterEmphasis =
                album.CenterEmphasis;

            entry.PortraitYOffset =
                album.PortraitYOffset;

            entry.PortraitSpacing =
                album.PortraitSpacing;

            entry.EffectsIntensity =
                album.EffectsIntensity;

            entry.CenterMemberIndex =
                album.CenterMemberIndex;

            return entry;
        }

        private static AlbumData FromSaveEntry(
            AlbumSaveEntry entry
        )
        {
            AlbumData album =
                new AlbumData();

            album.ID = entry.ID;
            album.Title =
                entry.Title ?? "";

            album.GroupName =
                entry.GroupName ?? "";

            if (entry.ReleaseTicks > 0L)
            {
                try
                {
                    album.ReleaseDate =
                        new DateTime(
                            entry.ReleaseTicks
                        );
                }
                catch
                {
                    album.ReleaseDate =
                        staticVars.dateTime;
                }
            }
            else
            {
                album.ReleaseDate =
                    staticVars.dateTime;
            }

            album.Released =
                entry.Released;

            album.PlayerAlbum =
                entry.PlayerAlbum;

            album.RivalName =
                entry.RivalName ?? "";

            album.RivalGroupId =
                entry.RivalGroupId;

            album.Members =
                RestoreMembers(
                    entry.MemberIds
                );

            album.Songs =
                RestoreSongs(
                    entry.SongIds
                );

            album.Sales =
                entry.Sales;

            album.WeeklySales =
                entry.WeeklySales;

            album.Profit =
                entry.Profit;

            album.ChartPosition =
                entry.ChartPosition;

            album.PreviousChartPosition =
                entry.PreviousChartPosition;

            album.PeakChartPosition =
                entry.PeakChartPosition;

            album.WeeksOnChart =
                entry.WeeksOnChart;

            album.CoverPath =
                entry.CoverPath ?? "";

            album.Theme =
                entry.Theme ?? "";

            album.ThemeIndex =
                entry.ThemeIndex;

            album.BackgroundIndex =
                entry.BackgroundIndex;

            album.LayoutIndex =
                entry.LayoutIndex;

            album.FontIndex =
                entry.FontIndex;

            album.TextColorIndex =
                entry.TextColorIndex;

            album.TitlePosition =
                entry.TitlePosition;

            album.ShowGroupName =
                entry.ShowGroupName;

            album.OrnamentStyle =
                entry.OrnamentStyle;

            album.FrameStyle =
                entry.FrameStyle;

            album.TitleEffect =
                entry.TitleEffect;

            album.PortraitScale =
                entry.PortraitScale <= 0f
                    ? 1f
                    : entry.PortraitScale;

            album.CenterEmphasis =
                entry.CenterEmphasis <= 0f
                    ? 1.08f
                    : entry.CenterEmphasis;

            album.PortraitYOffset =
                entry.PortraitYOffset;

            album.PortraitSpacing =
                entry.PortraitSpacing <= 0f
                    ? 1f
                    : entry.PortraitSpacing;

            album.EffectsIntensity =
                entry.EffectsIntensity <= 0f
                    ? 1f
                    : entry.EffectsIntensity;

            album.CenterMemberIndex =
                entry.CenterMemberIndex;

            return album;
        }

        private static List<data_girls.girls>
            RestoreMembers(
                List<int> ids
            )
        {
            List<data_girls.girls> result =
                new List<data_girls.girls>();

            if (ids == null)
                return result;

            foreach (int id in ids)
            {
                data_girls.girls girl = null;

                try
                {
                    // Normal player idol.
                    if (id > -1000000)
                    {
                        girl =
                            data_girls
                                .GetGirlByID(id);
                    }
                    else
                    {
                        // Rivals Reborn display idol.
                        girl = RivalsRebornIntegration.TryGetDisplayGirlById(id);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        "[AlbumSave] Could not restore member " +
                        id +
                        ": " +
                        ex.Message
                    );
                }

                if (girl != null)
                {
                    result.Add(girl);
                }
            }

            return result;
        }

        private static List<singles._single>
            RestoreSongs(
                List<int> ids
            )
        {
            List<singles._single> result =
                new List<singles._single>();

            if (ids == null)
                return result;

            foreach (int id in ids)
            {
                try
                {
                    singles._single single =
                        singles.GetSingleByID(id);

                    if (single != null)
                    {
                        result.Add(single);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        "[AlbumSave] Could not restore single " +
                        id +
                        ": " +
                        ex.Message
                    );
                }
            }

            return result;
        }

        private static string GetSavePath()
        {
            return Path.Combine(
                GetStorageDirectory(),
                "albums_" +
                GetSaveId() +
                ".json"
            );
        }

        private static string GetStorageDirectory()
        {
            string directory =
                Path.Combine(
                    Application.persistentDataPath,
                    "CreateAnAlbum"
                );

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(
                    directory
                );
            }

            return directory;
        }

        private static string GetSaveId()
        {
            try
            {
                string folder = "";

                if (staticVars.PlayerData != null)
                {
                    try
                    {
                        folder =
                            staticVars.PlayerData
                                .GetSaveFolderName();
                    }
                    catch
                    {
                    }
                }

                if (string.IsNullOrEmpty(folder))
                {
                    return staticVars.IsStoryMode()
                        ? "story_default"
                        : "freeplay_default";
                }

                // Preserve Idol Manager's complete save-folder identity, including its
                // unique suffix, so same-named campaigns cannot share one Album sidecar.

                string prefix =
                    staticVars.IsStoryMode()
                        ? "story_"
                        : "freeplay_";

                return SanitizeFileName(
                    prefix + folder
                );
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumSave] Could not determine save ID: " +
                    ex.Message
                );

                return staticVars.IsStoryMode()
                    ? "story_default"
                    : "freeplay_default";
            }
        }

        private static void WriteAllTextAtomic(string path, string contents)
        {
            string tempPath = path + ".tmp";
            string backupPath = path + ".bak";
            File.WriteAllText(tempPath, contents);

            if (!File.Exists(path))
            {
                File.Move(tempPath, path);
                return;
            }

            try
            {
                File.Replace(tempPath, path, backupPath);
            }
            catch
            {
                File.Copy(tempPath, path, true);
                File.Delete(tempPath);
            }
        }

        private static void TryMigrateLegacySidecar(string exactSaveId, string exactPath)
        {
            if (string.IsNullOrEmpty(exactSaveId) || File.Exists(exactPath))
                return;

            int underscore = exactSaveId.LastIndexOf('_');
            if (underscore <= 0 || underscore >= exactSaveId.Length - 1)
                return;

            string suffix = exactSaveId.Substring(underscore + 1);
            if (suffix.Length != 8 || !IsHexString(suffix))
                return;

            string legacySaveId = exactSaveId.Substring(0, underscore);
            string legacyPath = GetPathForSaveId(legacySaveId);
            if (!File.Exists(legacyPath))
                return;

            try
            {
                // Copy, do not move: old Album builds may have ambiguously shared the legacy
                // sidecar between same-named campaigns. The exact save gets its own copy now.
                File.Copy(legacyPath, exactPath, false);
                Debug.Log("[AlbumSave] Migrated legacy sidecar to exact save identity: " + exactSaveId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AlbumSave] Legacy sidecar migration failed: " + ex.Message);
            }
        }

        private static bool IsHexString(
            string value
        )
        {
            if (string.IsNullOrEmpty(value))
                return false;

            for (int i = 0;
                 i < value.Length;
                 i++)
            {
                char c = value[i];

                bool valid =
                    (c >= '0' && c <= '9') ||
                    (c >= 'a' && c <= 'f') ||
                    (c >= 'A' && c <= 'F');

                if (!valid)
                    return false;
            }

            return true;
        }

        private static string SanitizeFileName(
            string value
        )
        {
            if (string.IsNullOrEmpty(value))
                return "default";

            foreach (
                char c
                in Path.GetInvalidFileNameChars()
            )
            {
                value =
                    value.Replace(
                        c,
                        '_'
                    );
            }

            return value;
        }
    }
}
