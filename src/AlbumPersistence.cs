using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using CreateAnAlbumGroupRules;

namespace Albummodelite
{
    public static class AlbumPersistence
    {
        private static bool initialized;
        private static bool dirty;
        private static string stagedSaveJson = "";
        private static int stagedAlbumCount;

        [Serializable]
        private class AlbumSaveFile
        {
            public int Version = 3;
            public long WrittenUtcTicks;
            public long LastChartProcessedTicks;
            public AlbumProductionProject ProductionProject;
            public bool LegacyProductionMigrationCompleted;
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
            public string BackgroundKey = "";
            public int LayoutIndex;
            public int FontIndex;
            public string FontKey = "";
            public int ReleaseKind;
            public bool DebutFanRewardGranted;
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
        private static int pendingRetryFrames;
        private static int pendingImDataCoreWaitFrames;
        private const int MaxImDataCoreBootstrapWaitFrames = 300;
        private static string loadedSaveId = "";
        private static long lastChartProcessedTicks;
        private static int loadGeneration;
        private static int loadingFileVersion = 3;
        private static bool legacyProductionMigrationCompleted;

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

        internal static int LoadGeneration
        {
            get { return loadGeneration; }
        }

        internal static DateTime LastChartProcessedDate
        {
            get
            {
                if (lastChartProcessedTicks <= 0L)
                    return DateTime.MinValue;

                try
                {
                    return new DateTime(lastChartProcessedTicks).Date;
                }
                catch
                {
                    return DateTime.MinValue;
                }
            }
        }

        internal static void SetLastChartProcessedDate(DateTime value)
        {
            long ticks = value == DateTime.MinValue ? 0L : value.Date.Ticks;
            if (lastChartProcessedTicks == ticks)
                return;

            lastChartProcessedTicks = ticks;
            MarkDirty();
        }

        public static void Initialize()
        {
            Debug.Log(
                "[AlbumSave] *** PERSISTENCE v4.1.2 / SCHEMA v3 INITIALIZED ***"
            );

            IMDataCoreIntegration.BeginGameplaySession();

            SaveManager.SaveEvent -= OnGameSave;
            SaveManager.LoadEvent -= OnGameLoad;

            SaveManager.SaveEvent += OnGameSave;
            SaveManager.LoadEvent += OnGameLoad;

            initialized = true;
            dirty = false;
            stagedSaveJson = "";
            stagedAlbumCount = 0;
            // mainScript.Start can run after the vanilla SavedData assignment/LoadEvent on
            // some scene-load paths. Always schedule one stabilized initial read so album,
            // production, and chart state do not depend on the player opening F2/F3/F8 first.
            pendingLoad = true;
            pendingCandidateId = "";
            pendingStableFrames = 0;
            pendingRetryFrames = 0;
            pendingImDataCoreWaitFrames = 0;
            loadedSaveId = "";
            lastChartProcessedTicks = 0L;
            loadGeneration = 0;
            loadingFileVersion = 3;
            legacyProductionMigrationCompleted = false;

            Debug.Log(
                "[AlbumSave] Save/load events registered; initial save-state load scheduled."
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
            stagedSaveJson = "";
            stagedAlbumCount = 0;
            pendingLoad = false;
            pendingCandidateId = "";
            pendingStableFrames = 0;
            pendingRetryFrames = 0;
            pendingImDataCoreWaitFrames = 0;
            loadedSaveId = "";
            lastChartProcessedTicks = 0L;
            loadGeneration = 0;
            loadingFileVersion = 3;
            legacyProductionMigrationCompleted = false;
            AlbumSaveIdentity.Reset();
            AlbumProductionManager.Shutdown();
        }

        private static void OnGameSave()
        {
            Debug.Log("[AlbumSave] Idol Manager save detected.");

            if (!initialized || pendingLoad)
            {
                Debug.LogWarning("[AlbumSave] Save snapshot skipped during load transition.");
                return;
            }

            try
            {
                // SaveEvent fires before every real vanilla SavedData write. Capture the live
                // CAA state here, but do not choose/rebind a save identity yet. Autosave, Save As,
                // and Story new-save destinations are only authoritative at the concrete
                // DataSaver<SavedData> call site.
                Albums.DeduplicateInPlace();
                bool wasDirty = dirty;
                AlbumSaveFile file = BuildSaveFile();
                stagedSaveJson = JsonUtility.ToJson(file, true);
                stagedAlbumCount = file.Albums.Count;
                Debug.Log(
                    "[AlbumSave] Staged " + stagedAlbumCount +
                    " album(s), chart state, and production state for the pending vanilla checkpoint" +
                    (wasDirty ? " (dirty runtime state)." : " (checkpoint copy)."));
            }
            catch (Exception ex)
            {
                stagedSaveJson = "";
                stagedAlbumCount = 0;
                Debug.LogError("[AlbumSave] Could not stage save state:\n" + ex);
            }
        }

        private static void OnGameLoad()
        {
            if (!initialized)
                return;

            AlbumPopupHost.Reset();
            AlbumChartUpdatePopup.ResetForSaveLoad();
            AlbumProductionManager.Shutdown();
            stagedSaveJson = "";
            stagedAlbumCount = 0;
            pendingCandidateId = "";
            pendingStableFrames = 0;
            pendingRetryFrames = 0;
            pendingImDataCoreWaitFrames = 0;
            loadedSaveId = "";

            string candidate = GetSaveId();
            if (IMDataCoreIntegration.IsReady &&
                !string.IsNullOrEmpty(candidate) &&
                LoadForSaveId(candidate))
            {
                pendingLoad = false;
                Debug.Log("[AlbumSave] Loaded checkpoint-aware state from IM Data Core during vanilla LoadEvent.");
                return;
            }

            pendingLoad = true;
            Debug.Log(
                "[AlbumSave] Idol Manager load detected. " +
                "Waiting for the concrete save context to stabilize."
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
                pendingRetryFrames = 0;
            }

            if (IMDataCoreIntegration.IsBootstrapping)
            {
                pendingImDataCoreWaitFrames++;
                if (pendingImDataCoreWaitFrames < MaxImDataCoreBootstrapWaitFrames)
                    return;

                IMDataCoreIntegration.StopRetryingForSession(
                    "IM Data Core did not report ready during the initial load window.");
                pendingImDataCoreWaitFrames = 0;
            }
            else
            {
                pendingImDataCoreWaitFrames = 0;
            }

            if (pendingRetryFrames > 0)
            {
                pendingRetryFrames--;
                return;
            }

            if (pendingStableFrames < 10)
                return;

            Debug.Log(
                "[AlbumSave] Stable save context: " +
                candidate
            );

            if (!LoadForSaveId(candidate))
            {
                pendingRetryFrames = 60;
                return;
            }

            pendingLoad = false;
            pendingCandidateId = "";
            pendingStableFrames = 0;
            pendingRetryFrames = 0;
            pendingImDataCoreWaitFrames = 0;
        }

        public static bool EnsureCurrentSaveLoaded()
        {
            if (!initialized)
                return false;

            string current = GetSaveId();

            if (string.IsNullOrEmpty(current))
                return false;

            // Do not let an early F2/F3/F8 open force a standalone load while an installed
            // IM Data Core is still bringing its checkpoint store online. Tick() owns the
            // bounded wait and will deliberately latch to fallback if IMDC never becomes ready.
            if (IMDataCoreIntegration.IsBootstrapping)
            {
                pendingLoad = true;
                return false;
            }

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
                pendingRetryFrames = 0;
                pendingImDataCoreWaitFrames = 0;
            }
            else
            {
                pendingLoad = true;
                pendingCandidateId = current;
                pendingStableFrames = 0;
                pendingRetryFrames = 60;
                pendingImDataCoreWaitFrames = 0;
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

        /// <summary>
        /// Commits the SaveEvent snapshot to the exact physical vanilla save target immediately
        /// before IM Data Core prepares that same SavedData checkpoint. The caller-level Harmony
        /// transpiler runs before IMDC and Save Write Ordering Fix, so all three systems observe
        /// one concrete branch/path. Writing a checkpoint never rebinds loadedSaveId; only loading
        /// a save may change the live session's loaded identity.
        /// </summary>
        internal static void CaptureConcreteSaveWriteTarget(string dataFileName)
        {
            if (!initialized || string.IsNullOrEmpty(dataFileName))
                return;

            // Never checkpoint a not-yet-restored CAA runtime. Vanilla can autosave during
            // scene/load transitions; skipping our supplemental write is safer than turning
            // a transient empty runtime into an authoritative empty album checkpoint.
            if (pendingLoad)
            {
                stagedSaveJson = "";
                stagedAlbumCount = 0;
                Debug.LogWarning(
                    "[AlbumSave] Supplemental save skipped because album state is still loading.");
                return;
            }

            string targetId = AlbumSaveIdentity.GetIdentityForPath(dataFileName);
            if (string.IsNullOrEmpty(targetId))
            {
                Debug.LogWarning("[AlbumSave] Concrete save commit skipped: target path could not be normalized.");
                return;
            }

            try
            {
                AlbumSaveIdentity.CaptureSaveTarget(dataFileName);

                string json = stagedSaveJson;
                int albumCount = stagedAlbumCount;
                if (string.IsNullOrEmpty(json))
                {
                    // Defensive fallback for a third-party caller that reaches DataSaver without
                    // invoking vanilla SaveEvent. This must still snapshot the live state rather
                    // than reuse some earlier checkpoint.
                    Albums.DeduplicateInPlace();
                    AlbumSaveFile fallbackFile = BuildSaveFile();
                    json = JsonUtility.ToJson(fallbackFile, true);
                    albumCount = fallbackFile.Albums.Count;
                }

                bool committed = false;

                // This call intentionally occurs BEFORE IMDC's PrepareVanillaSaveWrite injection.
                // IMDC therefore forks/persists the branch only after CAA's custom JSON mutation
                // is present, including for random manual-save paths created after SaveEvent.
                if (IMDataCoreIntegration.IsReady)
                    committed = IMDataCoreIntegration.TrySetState(json);

                try
                {
                    string fallbackPath = GetPathForSaveId(targetId);
                    WriteAllTextAtomic(fallbackPath, json);
                    committed = true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[AlbumSave] Standalone checkpoint mirror failed: " + ex.Message);
                }

                if (committed)
                {
                    dirty = false;
                    Debug.Log(
                        "[AlbumSave] Committed " + albumCount +
                        " album(s), chart state, and production state to concrete target " +
                        targetId + ".");
                }
                else
                {
                    dirty = true;
                    Debug.LogWarning(
                        "[AlbumSave] Supplemental checkpoint could not be committed; state remains dirty.");
                }
            }
            catch (Exception ex)
            {
                dirty = true;
                Debug.LogError("[AlbumSave] Concrete save commit failed:\n" + ex);
            }
            finally
            {
                stagedSaveJson = "";
                stagedAlbumCount = 0;
                AlbumSaveIdentity.ClearPendingSaveTarget();
            }
        }

        private static AlbumSaveFile BuildSaveFile()
        {
            AlbumSaveFile file = new AlbumSaveFile();
            file.Version = 3;
            file.WrittenUtcTicks = DateTime.UtcNow.Ticks;
            file.LastChartProcessedTicks = lastChartProcessedTicks;
            file.ProductionProject = AlbumProductionManager.GetProjectForSave();
            file.LegacyProductionMigrationCompleted = legacyProductionMigrationCompleted;
            foreach (AlbumData album in Albums.AlbumList)
            {
                if (album != null)
                    file.Albums.Add(ToSaveEntry(album));
            }
            return file;
        }

        public static void Load()
        {
            if (!initialized)
                return;

            LoadForSaveId(
                GetSaveId()
            );
        }

        private static bool LoadForSaveId(string saveId)
        {
            if (!initialized || string.IsNullOrEmpty(saveId))
                return false;

            try
            {
                string json = string.Empty;
                string source = string.Empty;
                string imdcJson = string.Empty;
                string fallbackJson = string.Empty;
                string fallbackSource = string.Empty;

                if (IMDataCoreIntegration.IsReady)
                    IMDataCoreIntegration.TryGetState(out imdcJson);

                string fallbackId = GetFallbackSaveId();
                string path = GetPathForSaveId(!string.IsNullOrEmpty(fallbackId) ? fallbackId : saveId);
                TryMigrateLegacySidecars(path);
                if (File.Exists(path))
                {
                    fallbackJson = File.ReadAllText(path);
                    fallbackSource = path;
                }
                else
                {
                    // Last migration chance from the v4 campaign-level sidecar. This is cloned
                    // into each physical slot on first use, after which the slots diverge safely.
                    string campaignPath = GetPathForSaveId(GetLegacyCampaignSaveId());
                    if (File.Exists(campaignPath))
                    {
                        fallbackJson = File.ReadAllText(campaignPath);
                        fallbackSource = campaignPath + " (legacy campaign migration)";
                    }
                }

                if (!string.IsNullOrEmpty(imdcJson) && !string.IsNullOrEmpty(fallbackJson))
                {
                    long imdcWritten = GetWrittenUtcTicks(imdcJson);
                    long fallbackWritten = GetWrittenUtcTicks(fallbackJson);
                    if (fallbackWritten > imdcWritten)
                    {
                        json = fallbackJson;
                        source = fallbackSource + " (newer than IM Data Core copy)";
                    }
                    else
                    {
                        json = imdcJson;
                        source = "IM Data Core";
                    }
                }
                else if (!string.IsNullOrEmpty(imdcJson))
                {
                    json = imdcJson;
                    source = "IM Data Core";
                }
                else if (!string.IsNullOrEmpty(fallbackJson))
                {
                    json = fallbackJson;
                    source = fallbackSource;
                }

                if (string.IsNullOrEmpty(json))
                {
                    Albums.AlbumList.Clear();
                    CompleteLoadContext(saveId, 0L, null, false);
                    RivalAlbumManager.RebuildIdAllocator();
                    Debug.Log("[AlbumSave] No album state found for " + saveId + ". Starting with 0 albums.");
                    return true;
                }

                AlbumSaveFile file = JsonUtility.FromJson<AlbumSaveFile>(json);
                if (file == null || file.Albums == null)
                {
                    Debug.LogWarning("[AlbumSave] Save document was empty or invalid; existing runtime state was not overwritten.");
                    return false;
                }

                loadingFileVersion = file.Version <= 0 ? 1 : file.Version;
                bool needsBackgroundKeyMigration =
                    file.Albums.Any(entry => entry != null && string.IsNullOrEmpty(entry.BackgroundKey)) ||
                    (file.ProductionProject != null && string.IsNullOrEmpty(file.ProductionProject.BackgroundKey));
                List<AlbumData> restoredAlbums = new List<AlbumData>();
                HashSet<string> seen = new HashSet<string>();
                int duplicatesSkipped = 0;

                foreach (AlbumSaveEntry entry in file.Albums)
                {
                    if (entry == null)
                        continue;
                    string key = GetSaveEntryIdentityKey(entry);
                    if (!seen.Add(key))
                    {
                        duplicatesSkipped++;
                        continue;
                    }
                    AlbumData album = FromSaveEntry(entry);
                    if (album != null)
                        restoredAlbums.Add(album);
                }

                Albums.AlbumList.Clear();
                Albums.AlbumList.AddRange(restoredAlbums);
                Albums.DeduplicateInPlace();
                CompleteLoadContext(
                    saveId,
                    file.LastChartProcessedTicks,
                    file.ProductionProject,
                    file.LegacyProductionMigrationCompleted);
                RivalAlbumManager.RebuildIdAllocator();

                // Keep the two persistence backends mirrored to the checkpoint that was
                // actually selected, but copy the committed source document verbatim. Do not
                // serialize the just-restored runtime here: schema/legacy-production migrations
                // are intentionally dirty until the player's next real Idol Manager save.
                if (IMDataCoreIntegration.IsReady && source != "IM Data Core")
                {
                    // Do not create a custom-data mutation while vanilla is still inside its
                    // load boundary. Mark the restored fallback state dirty so the next actual
                    // vanilla save commits it through the concrete checkpoint pipeline.
                    MarkDirty();
                }
                else if (source == "IM Data Core" && !string.IsNullOrEmpty(fallbackId))
                {
                    WriteAllTextAtomic(path, json);
                }

                if (loadingFileVersion < 3)
                    MarkDirty();
                if (needsBackgroundKeyMigration)
                    MarkDirty();
                if (duplicatesSkipped > 0)
                    MarkDirty();

                Debug.Log("[AlbumSave] Loaded " + Albums.AlbumList.Count + " unique album(s) from " + source + ".");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[AlbumSave] Load failed:\n" + ex);
                return false;
            }
        }

        private static long GetWrittenUtcTicks(string json)
        {
            if (string.IsNullOrEmpty(json))
                return long.MinValue;
            try
            {
                AlbumSaveFile file = JsonUtility.FromJson<AlbumSaveFile>(json);
                return file != null ? file.WrittenUtcTicks : long.MinValue;
            }
            catch
            {
                return long.MinValue;
            }
        }

        private static void CompleteLoadContext(
            string saveId,
            long chartProcessedTicks,
            AlbumProductionProject productionProject,
            bool productionMigrationCompleted)
        {
            loadedSaveId = saveId;
            lastChartProcessedTicks = chartProcessedTicks > 0L
                ? chartProcessedTicks
                : 0L;
            dirty = false;
            loadGeneration++;

            AlbumProductionProject restoredProject = productionProject;
            legacyProductionMigrationCompleted =
                productionMigrationCompleted || restoredProject != null;

            if (restoredProject == null && !legacyProductionMigrationCompleted)
            {
                string legacyProductionId = GetLegacyProductionSaveId();
                bool migratedLegacyProduction =
                    AlbumProductionManager.TryLoadLegacyProject(legacyProductionId, out restoredProject);

                // Some later standalone builds may have been paired with the exact-folder
                // v4 save identity instead. Accept that form too without conflating it with
                // the older suffix-stripped production filename.
                string exactCampaignId = GetLegacyCampaignSaveId();
                if (!migratedLegacyProduction &&
                    !string.Equals(legacyProductionId, exactCampaignId, StringComparison.Ordinal))
                {
                    migratedLegacyProduction =
                        AlbumProductionManager.TryLoadLegacyProject(exactCampaignId, out restoredProject);
                }

                if (migratedLegacyProduction)
                {
                    // Persist the marker together with the migrated project. We intentionally do
                    // not delete/rename the legacy file here: if the player quits without saving,
                    // the migration should roll back just like the rest of the checkpoint.
                    legacyProductionMigrationCompleted = true;
                    dirty = true;
                }
            }
            if (restoredProject != null && string.IsNullOrEmpty(restoredProject.BackgroundKey))
                restoredProject.BackgroundKey = AlbumBackgroundCatalog.GetLegacyKey(restoredProject.BackgroundIndex);

            AlbumProductionManager.RestoreFromSave(restoredProject, saveId);
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

            entry.BackgroundKey =
                album.BackgroundKey ?? "";

            entry.LayoutIndex =
                album.LayoutIndex;

            entry.FontIndex =
                album.FontIndex;

            entry.FontKey = album.FontKey ?? "";
            entry.ReleaseKind = album.ReleaseKind;
            entry.DebutFanRewardGranted = album.DebutFanRewardGranted;

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

            album.BackgroundKey = string.IsNullOrEmpty(entry.BackgroundKey)
                ? AlbumBackgroundCatalog.GetLegacyKey(entry.BackgroundIndex)
                : entry.BackgroundKey;

            album.LayoutIndex =
                entry.LayoutIndex;

            album.FontIndex =
                entry.FontIndex;

            album.FontKey = string.IsNullOrEmpty(entry.FontKey)
                ? AlbumFontCatalog.GetKey(entry.FontIndex)
                : entry.FontKey;
            if (loadingFileVersion < 3)
            {
                int restoredSongCount = album.Songs != null ? album.Songs.Count : 0;
                album.ReleaseKind = restoredSongCount > 10
                    ? (int)AlbumReleaseKind.LP
                    : (restoredSongCount > 6 ? (int)AlbumReleaseKind.EP : (int)AlbumReleaseKind.MiniAlbum);
            }
            else
            {
                album.ReleaseKind = entry.ReleaseKind;
            }

            // v1/v2 releases predate the restored debut-reward marker. Treat historical
            // albums as already consumed so upgrading never grants retroactive fan windfalls.
            album.DebutFanRewardGranted = loadingFileVersion < 3
                ? true
                : entry.DebutFanRewardGranted;

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
            // Keep Create An Album's in-memory identity tied to the concrete vanilla save
            // target, not to which optional persistence backend happens to be ready this frame.
            // IM Data Core can bootstrap slightly after mainScript.Start; using its active key
            // here would make the identity change mid-session and could reload over dirty state.
            string fallback = AlbumSaveIdentity.GetActiveFallbackIdentity();
            return !string.IsNullOrEmpty(fallback)
                ? fallback
                : GetLegacyCampaignSaveId();
        }

        private static string GetFallbackSaveId()
        {
            string id = AlbumSaveIdentity.GetActiveFallbackIdentity();
            if (!string.IsNullOrEmpty(id))
                return id;

            string storyAlias = AlbumSaveIdentity.GetLogicalNewStoryAlias();
            return storyAlias ?? string.Empty;
        }

        private static string GetLegacyCampaignSaveId()
        {
            try
            {
                string folder = string.Empty;
                if (staticVars.PlayerData != null)
                {
                    try { folder = staticVars.PlayerData.GetSaveFolderName(); }
                    catch { }
                }
                if (string.IsNullOrEmpty(folder))
                    return staticVars.IsStoryMode() ? "story_default" : "freeplay_default";

                string prefix = staticVars.IsStoryMode() ? "story_" : "freeplay_";
                return SanitizeFileName(prefix + folder);
            }
            catch
            {
                return staticVars.IsStoryMode() ? "story_default" : "freeplay_default";
            }
        }

        private static string GetLegacyProductionSaveId()
        {
            try
            {
                string folder = string.Empty;
                if (staticVars.PlayerData != null)
                {
                    try { folder = staticVars.PlayerData.GetSaveFolderName(); }
                    catch { }
                }

                if (string.IsNullOrEmpty(folder))
                    return staticVars.IsStoryMode() ? "story_default" : "freeplay_default";

                // The production addon in the supplied legacy archive reflected the old
                // AlbumPersistence.GetSaveId(), which stripped an 8-character hexadecimal
                // suffix from the campaign folder before building its project filename.
                int underscore = folder.LastIndexOf('_');
                if (underscore > 0 && underscore < folder.Length - 1)
                {
                    string suffix = folder.Substring(underscore + 1);
                    if (suffix.Length == 8 && IsHexString(suffix))
                        folder = folder.Substring(0, underscore);
                }

                string prefix = staticVars.IsStoryMode() ? "story_" : "freeplay_";
                return SanitizeFileName(prefix + folder);
            }
            catch
            {
                return staticVars.IsStoryMode() ? "story_default" : "freeplay_default";
            }
        }

        private static void TryMigrateLegacySidecars(string exactPath)
        {
            if (File.Exists(exactPath))
                return;

            string campaignPath = GetPathForSaveId(GetLegacyCampaignSaveId());
            if (File.Exists(campaignPath) && !string.Equals(campaignPath, exactPath, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Copy(campaignPath, exactPath, false);
                    Debug.Log("[AlbumSave] Migrated v4 campaign sidecar into the concrete save slot.");
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[AlbumSave] Campaign sidecar migration failed: " + ex.Message);
                }
            }

            // Pre-v4 builds stripped Idol Manager's trailing eight-character campaign suffix.
            // Physical v4.1 save IDs are hashes, so that old filename cannot be inferred from
            // exactPath itself; compute the legacy campaign ID explicitly instead.
            string suffixStrippedPath = GetPathForSaveId(GetLegacyProductionSaveId());
            if (File.Exists(suffixStrippedPath) &&
                !string.Equals(suffixStrippedPath, exactPath, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(suffixStrippedPath, campaignPath, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Copy(suffixStrippedPath, exactPath, false);
                    Debug.Log("[AlbumSave] Migrated suffix-stripped legacy album sidecar into the concrete save slot.");
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[AlbumSave] Suffix-stripped sidecar migration failed: " + ex.Message);
                }
            }

            string exactId = Path.GetFileNameWithoutExtension(exactPath);
            if (exactId != null && exactId.StartsWith("albums_", StringComparison.Ordinal))
                TryMigrateLegacySidecar(exactId.Substring(7), exactPath);
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
