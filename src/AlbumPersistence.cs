using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using CreateAnAlbumGroupRules;
using CreateAnAlbumChartTrackEnhancements;

namespace Albummodelite
{
    public static class AlbumPersistence
    {
        private static bool initialized;
        private static bool dirty;
        private static int dirtyGeneration;
        private static string stagedSaveJson = "";
        private static int stagedAlbumCount;
        private static int stagedDirtyGeneration;

        private const int CheckpointBindingVersion = 2;
        private const int MaxPendingLoadFailures = 10;
        private const int CommitValidationRetryFrames = 30;
        private const int CommitValidationMaxAttempts = 60;
        private static readonly TimeSpan LegacyMirrorTimestampTolerance = TimeSpan.FromMinutes(5);

        private sealed class PendingCommitValidation
        {
            internal string VanillaPath = string.Empty;
            internal string TargetId = string.Empty;
            internal string ExpectedFingerprint = string.Empty;
            internal int DirtyGeneration;
            internal int RetryFrames;
            internal int AttemptsRemaining;
        }

        private static readonly List<PendingCommitValidation> pendingCommitValidations =
            new List<PendingCommitValidation>();

        [Serializable]
        private class AlbumSaveFile
        {
            public int Version = 4;
            public long WrittenUtcTicks;
            public int CheckpointBindingVersion;
            public AlbumVanillaCheckpointStamp VanillaCheckpoint;
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

            public List<AlbumMemberSnapshot> MemberSnapshots =
                new List<AlbumMemberSnapshot>();

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

            // CenterMemberIndex is retained for legacy documents. CenterMemberId is the
            // stable identity used by current saves; the explicit flag distinguishes an old
            // document (where JsonUtility supplies 0 for the new int field) from a real ID.
            public int CenterMemberIndex;
            public int CenterMemberId = -1;
            public bool HasCenterMemberId;
        }

        private static bool pendingLoad;
        private static string pendingCandidateId = "";
        private static int pendingStableFrames;
        private static int pendingRetryFrames;
        private static int pendingImDataCoreWaitFrames;
        private static int pendingLoadFailureCount;
        private const int MaxImDataCoreBootstrapWaitFrames = 300;
        private static string loadedSaveId = "";
        private static bool supplementalWriteBlocked;
        private static string supplementalWriteBlockReason = "";
        private static long lastChartProcessedTicks;
        private static int loadGeneration;
        private static int loadingFileVersion = 4;
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
                "[AlbumSave] *** PERSISTENCE v4.2.0 / SCHEMA v4 INITIALIZED ***"
            );

            IMDataCoreIntegration.BeginGameplaySession();

            SaveManager.SaveEvent -= OnGameSave;
            SaveManager.LoadEvent -= OnGameLoad;

            SaveManager.SaveEvent += OnGameSave;
            SaveManager.LoadEvent += OnGameLoad;

            initialized = true;
            dirty = false;
            dirtyGeneration = 0;
            stagedSaveJson = "";
            stagedAlbumCount = 0;
            stagedDirtyGeneration = 0;
            pendingCommitValidations.Clear();
            // mainScript.Start can run after the vanilla SavedData assignment/LoadEvent on
            // some scene-load paths. Always schedule one stabilized initial read so album,
            // production, and chart state do not depend on the player opening F2/F3/F8 first.
            pendingLoad = true;
            pendingCandidateId = "";
            pendingStableFrames = 0;
            pendingRetryFrames = 0;
            pendingImDataCoreWaitFrames = 0;
            pendingLoadFailureCount = 0;
            loadedSaveId = "";
            supplementalWriteBlocked = false;
            supplementalWriteBlockReason = "";
            lastChartProcessedTicks = 0L;
            loadGeneration = 0;
            loadingFileVersion = 4;
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
            dirtyGeneration = 0;
            stagedSaveJson = "";
            stagedAlbumCount = 0;
            stagedDirtyGeneration = 0;
            pendingCommitValidations.Clear();
            pendingLoad = false;
            pendingCandidateId = "";
            pendingStableFrames = 0;
            pendingRetryFrames = 0;
            pendingImDataCoreWaitFrames = 0;
            pendingLoadFailureCount = 0;
            loadedSaveId = "";
            supplementalWriteBlocked = false;
            supplementalWriteBlockReason = "";
            lastChartProcessedTicks = 0L;
            loadGeneration = 0;
            loadingFileVersion = 4;
            legacyProductionMigrationCompleted = false;
            AlbumSaveIdentity.Reset();
            AlbumProductionManager.Shutdown();
            AlbumTrackRepair.ResetForSaveContext(string.Empty);
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
                stagedDirtyGeneration = dirtyGeneration;
                Debug.Log(
                    "[AlbumSave] Staged " + stagedAlbumCount +
                    " album(s), chart state, and production state for the pending vanilla checkpoint" +
                    (wasDirty ? " (dirty runtime state)." : " (checkpoint copy)."));
            }
            catch (Exception ex)
            {
                stagedSaveJson = "";
                stagedAlbumCount = 0;
                stagedDirtyGeneration = dirtyGeneration;
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
            stagedDirtyGeneration = 0;
            pendingCommitValidations.Clear();
            pendingCandidateId = "";
            pendingStableFrames = 0;
            pendingRetryFrames = 0;
            pendingImDataCoreWaitFrames = 0;
            pendingLoadFailureCount = 0;
            loadedSaveId = "";

            string candidate = GetSaveId();
            if (IMDataCoreIntegration.IsReady &&
                !string.IsNullOrEmpty(candidate) &&
                LoadForSaveId(candidate))
            {
                pendingLoad = false;
                Debug.Log("[AlbumSave] Loaded checkpoint-compatible supplemental state during vanilla LoadEvent.");
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
            if (!initialized)
                return;

            TickPendingCommitValidations();
            // Member registries and portrait assets can become available after the save document.
            // Keep retrying detached/live member reconstruction without touching disk.
            AlbumMemberRepair.RepairAll();

            if (!pendingLoad)
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
                pendingLoadFailureCount = 0;
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
                pendingLoadFailureCount++;
                if (pendingLoadFailureCount >= MaxPendingLoadFailures)
                {
                    RecoverFromPersistentLoadFailure(candidate);
                    pendingLoad = false;
                    pendingCandidateId = "";
                    pendingStableFrames = 0;
                    pendingRetryFrames = 0;
                    pendingImDataCoreWaitFrames = 0;
                    pendingLoadFailureCount = 0;
                    return;
                }

                pendingRetryFrames = 60;
                return;
            }

            pendingLoad = false;
            pendingCandidateId = "";
            pendingStableFrames = 0;
            pendingRetryFrames = 0;
            pendingImDataCoreWaitFrames = 0;
            pendingLoadFailureCount = 0;
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
                pendingLoadFailureCount = 0;
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
            if (!initialized)
                return;

            dirty = true;
            unchecked { dirtyGeneration++; }
        }

        // Compatibility surface for older Album code. A direct Save request now marks the
        // sidecar dirty; only Idol Manager's real SaveEvent commits it to disk.
        public static void Save()
        {
            MarkDirty();
        }

        /// <summary>
        /// Binds the staged CAA document to the exact SavedData payload and physical vanilla
        /// target immediately before IM Data Core and the vanilla writer see that checkpoint.
        /// The standalone mirror is written with a one-generation backup, but dirty state is
        /// cleared only after the physical vanilla file is observed with the same fingerprint.
        /// </summary>
        internal static void CaptureConcreteSaveWriteTarget(
            SaveManager.SavedData dataToSave,
            string dataFileName,
            bool isJson,
            bool fullPath)
        {
            if (!initialized || dataToSave == null || string.IsNullOrEmpty(dataFileName))
                return;

            if (supplementalWriteBlocked)
            {
                dirty = true;
                Debug.LogError(
                    "[AlbumSave] Supplemental write blocked to protect an existing unresolved sidecar: " +
                    supplementalWriteBlockReason);
                return;
            }

            if (pendingLoad)
            {
                stagedSaveJson = "";
                stagedAlbumCount = 0;
                stagedDirtyGeneration = dirtyGeneration;
                Debug.LogWarning(
                    "[AlbumSave] Supplemental save skipped because album state is still loading.");
                return;
            }

            string targetId = AlbumSaveIdentity.GetIdentityForWriteTarget(dataFileName, fullPath);
            if (string.IsNullOrEmpty(targetId))
            {
                Debug.LogWarning(
                    "[AlbumSave] Concrete save commit skipped: target path could not be normalized.");
                return;
            }

            try
            {
                AlbumSaveIdentity.CaptureSaveTarget(dataFileName, fullPath);

                AlbumVanillaCheckpointStamp checkpoint;
                string stampError;
                if (!AlbumSaveIdentity.TryCreateCheckpointStampForWrite(
                        dataToSave,
                        dataFileName,
                        fullPath,
                        out checkpoint,
                        out stampError))
                {
                    dirty = true;
                    Debug.LogWarning(
                        "[AlbumSave] Supplemental checkpoint was not written because vanilla " +
                        "checkpoint identity could not be frozen: " + stampError);
                    return;
                }

                string json = stagedSaveJson;
                int albumCount = stagedAlbumCount;
                int snapshotDirtyGeneration = stagedDirtyGeneration;

                AlbumSaveFile file;
                if (string.IsNullOrEmpty(json))
                {
                    Albums.DeduplicateInPlace();
                    file = BuildSaveFile();
                    albumCount = file.Albums.Count;
                    snapshotDirtyGeneration = dirtyGeneration;
                }
                else
                {
                    file = JsonUtility.FromJson<AlbumSaveFile>(json);
                    if (file == null || file.Albums == null)
                        throw new InvalidDataException("The staged album save document could not be parsed.");
                }

                file.CheckpointBindingVersion = CheckpointBindingVersion;
                file.VanillaCheckpoint = checkpoint;
                json = JsonUtility.ToJson(file, true);

                // This mutation must stay before IMDC's concrete-save preparation so its branch
                // receives the same checkpoint-bound CAA document. IMDC is recovery storage,
                // though: only a successful exact-slot standalone write is allowed to make this
                // save generation eligible for commit confirmation.
                if (IMDataCoreIntegration.IsReady)
                    IMDataCoreIntegration.TrySetState(json);

                bool standaloneWritten = false;
                try
                {
                    string fallbackPath = AlbumSaveIdentity.GetSidecarPathForWriteTarget(dataFileName, fullPath);
                    if (string.IsNullOrEmpty(fallbackPath))
                        throw new InvalidDataException("The concrete save target is outside Idol Manager's stable data tree.");
                    WriteAllTextAtomic(fallbackPath, json);
                    standaloneWritten = true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        "[AlbumSave] Standalone checkpoint mirror failed: " + ex.Message);
                }

                if (!standaloneWritten)
                {
                    dirty = true;
                    Debug.LogWarning(
                        "[AlbumSave] Exact-slot supplemental checkpoint could not be written; " +
                        "state remains dirty even if IM Data Core accepted a recovery copy.");
                    return;
                }

                string physicalTargetPath = AlbumSaveIdentity.ResolveWritePhysicalPath(dataFileName, fullPath);
                bool confirmed = QueueOrConfirmCommitValidation(
                    physicalTargetPath,
                    targetId,
                    checkpoint.ContentFingerprint,
                    snapshotDirtyGeneration);

                Debug.Log(
                    "[AlbumSave] Wrote " + albumCount +
                    " album(s), chart state, and production state for concrete target " +
                    targetId +
                    (confirmed
                        ? " and confirmed the matching vanilla checkpoint."
                        : "; awaiting confirmation from the asynchronous vanilla writer."));
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
                stagedDirtyGeneration = dirtyGeneration;
                AlbumSaveIdentity.ClearPendingSaveTarget();
            }
        }

        private static AlbumSaveFile BuildSaveFile()
        {
            AlbumSaveFile file = new AlbumSaveFile();
            file.Version = 4;
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

            // AlbumTrackRepair keeps a transient ID bridge for singles that are not ready yet.
            // Never let those IDs cross a real save/checkpoint load. A retry for the new context
            // also starts clean, preventing a partially failed restore from seeding the next try.
            if (!string.Equals(loadedSaveId, saveId, StringComparison.Ordinal))
                AlbumTrackRepair.ResetForSaveContext(saveId);

            try
            {
                string imdcJson = string.Empty;
                if (IMDataCoreIntegration.IsReady)
                    IMDataCoreIntegration.TryGetState(out imdcJson);

                string fallbackId = GetFallbackSaveId();
                string path = AlbumSaveIdentity.GetActiveSidecarPath();
                if (string.IsNullOrEmpty(path))
                {
                    // Compatibility fallback for a transient/new session before a concrete
                    // vanilla load path has been observed. Real save writes always use the
                    // mirrored relative-slot path.
                    path = GetPathForSaveId(
                        !string.IsNullOrEmpty(fallbackId) ? fallbackId : saveId);
                }
                TryMigrateLegacySidecars(path);

                string fallbackJson = ReadTextIfPresent(path);
                string backupPath = path + ".bak";
                string backupJson = ReadTextIfPresent(backupPath);
                string fallbackSource = path;

                if (string.IsNullOrEmpty(fallbackJson))
                {
                    string campaignPath = GetPathForSaveId(GetLegacyCampaignSaveId());
                    if (File.Exists(campaignPath))
                    {
                        fallbackJson = File.ReadAllText(campaignPath);
                        fallbackSource = campaignPath + " (legacy campaign migration)";
                    }
                }

                bool hasAnySupplementalCandidate =
                    File.Exists(path) ||
                    File.Exists(backupPath) ||
                    !string.IsNullOrEmpty(imdcJson);

                AlbumVanillaCheckpointStamp currentCheckpoint = null;
                string currentCheckpointError = string.Empty;
                string currentVanillaPath = AlbumSaveIdentity.GetActiveLoadTarget();
                if (hasAnySupplementalCandidate &&
                    !TryGetCurrentCheckpointStamp(
                        out currentCheckpoint,
                        out currentCheckpointError))
                {
                    Debug.LogWarning(
                        "[AlbumSave] Waiting to validate supplemental state against the loaded " +
                        "vanilla checkpoint: " + currentCheckpointError);
                    return false;
                }

                string json = string.Empty;
                string source = string.Empty;
                AlbumSaveFile file = null;
                bool selectedExact = false;
                bool selectedBackup = false;
                bool selectedImdc = false;
                bool selectedLegacyUnbound = false;
                string rejectionReason;
                bool legacyUnbound;

                // Bound exact-slot data stays authoritative. Legacy standalone documents are
                // deliberately deferred until after any checkpoint-aware IMDC recovery copy; a
                // wall-clock-only legacy migration must never outrank stronger checkpoint identity.
                AlbumSaveFile deferredLegacyExactFile = null;
                string deferredLegacyExactJson = string.Empty;
                AlbumSaveFile deferredLegacyBackupFile = null;
                string deferredLegacyBackupJson = string.Empty;

                AlbumSaveFile parsedFile;
                if (TryParseCompatibleCandidate(
                        fallbackJson,
                        false,
                        currentCheckpoint,
                        currentVanillaPath,
                        out parsedFile,
                        out legacyUnbound,
                        out rejectionReason))
                {
                    if (legacyUnbound)
                    {
                        deferredLegacyExactFile = parsedFile;
                        deferredLegacyExactJson = fallbackJson;
                    }
                    else
                    {
                        file = parsedFile;
                        json = fallbackJson;
                        source = fallbackSource + " (checkpoint-matched exact-slot mirror)";
                        selectedExact = true;
                    }
                }
                else if (!string.IsNullOrEmpty(fallbackJson) && !string.IsNullOrEmpty(rejectionReason))
                {
                    Debug.LogWarning(
                        "[AlbumSave] Exact-slot mirror was not used: " + rejectionReason);
                }

                if (string.IsNullOrEmpty(json) &&
                    TryParseCompatibleCandidate(
                        backupJson,
                        false,
                        currentCheckpoint,
                        currentVanillaPath,
                        out parsedFile,
                        out legacyUnbound,
                        out rejectionReason))
                {
                    if (legacyUnbound)
                    {
                        deferredLegacyBackupFile = parsedFile;
                        deferredLegacyBackupJson = backupJson;
                    }
                    else
                    {
                        file = parsedFile;
                        json = backupJson;
                        source = backupPath + " (checkpoint-matched previous mirror)";
                        selectedBackup = true;
                    }
                }
                else if (string.IsNullOrEmpty(json) &&
                         !string.IsNullOrEmpty(backupJson) &&
                         !string.IsNullOrEmpty(rejectionReason))
                {
                    Debug.LogWarning(
                        "[AlbumSave] Previous exact-slot mirror was not used: " + rejectionReason);
                }

                if (string.IsNullOrEmpty(json) &&
                    TryParseCompatibleCandidate(
                        imdcJson,
                        true,
                        currentCheckpoint,
                        currentVanillaPath,
                        out parsedFile,
                        out legacyUnbound,
                        out rejectionReason))
                {
                    file = parsedFile;
                    json = imdcJson;
                    source = "IM Data Core checkpoint recovery copy";
                    selectedImdc = true;
                    selectedLegacyUnbound = legacyUnbound;
                }
                else if (string.IsNullOrEmpty(json) &&
                         !string.IsNullOrEmpty(imdcJson) &&
                         !string.IsNullOrEmpty(rejectionReason))
                {
                    Debug.LogWarning(
                        "[AlbumSave] IM Data Core copy was not used: " + rejectionReason);
                }

                if (string.IsNullOrEmpty(json) && deferredLegacyExactFile != null)
                {
                    file = deferredLegacyExactFile;
                    json = deferredLegacyExactJson;
                    source = fallbackSource + " (legacy exact-slot migration)";
                    selectedExact = true;
                    selectedLegacyUnbound = true;
                }
                else if (string.IsNullOrEmpty(json) && deferredLegacyBackupFile != null)
                {
                    file = deferredLegacyBackupFile;
                    json = deferredLegacyBackupJson;
                    source = backupPath + " (legacy previous-mirror migration)";
                    selectedBackup = true;
                    selectedLegacyUnbound = true;
                }

                if (string.IsNullOrEmpty(json) || file == null)
                {
                    if (!hasAnySupplementalCandidate)
                    {
                        // A truly new slot has no supplemental files at all. Only that case is
                        // allowed to initialize a writable empty collection.
                        Albums.AlbumList.Clear();
                        CompleteLoadContext(saveId, 0L, null, false);
                        RivalAlbumManager.RebuildIdAllocator();
                        Debug.Log(
                            "[AlbumSave] No supplemental state exists for " +
                            saveId + ". Initialized a new empty album slot.");
                        return true;
                    }

                    // Existing-but-unusable state is NOT evidence that the player has zero
                    // albums. Keep retrying/recovering without ever blessing an empty state.
                    Debug.LogWarning(
                        "[AlbumSave] Supplemental state exists for " + saveId +
                        " but no safe candidate could be selected. Refusing destructive empty-state recovery.");
                    return false;
                }

                loadingFileVersion = file.Version <= 0 ? 1 : file.Version;
                bool needsBackgroundKeyMigration =
                    file.Albums.Any(entry => entry != null && string.IsNullOrEmpty(entry.BackgroundKey)) ||
                    (file.ProductionProject != null && string.IsNullOrEmpty(file.ProductionProject.BackgroundKey));
                bool needsCenterMemberIdMigration = file.Albums.Any(entry =>
                    entry != null &&
                    entry.CenterMemberIndex >= 0 &&
                    !entry.HasCenterMemberId);
                bool needsMemberSnapshotMigration = file.Albums.Any(entry =>
                    entry != null && entry.PlayerAlbum &&
                    entry.MemberIds != null && entry.MemberIds.Count > 0 &&
                    (entry.MemberSnapshots == null || entry.MemberSnapshots.Count == 0));
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

                // A selected legacy v3 document can be bound safely now because the vanilla
                // checkpoint is already loaded and was accepted by IMDC or the timestamp migration
                // guard. This prevents the same legacy document from remaining ambiguous forever.
                if (selectedLegacyUnbound && currentCheckpoint != null)
                {
                    file.CheckpointBindingVersion = CheckpointBindingVersion;
                    file.VanillaCheckpoint = currentCheckpoint;
                    json = JsonUtility.ToJson(file, true);
                }

                if ((selectedBackup || selectedImdc || selectedLegacyUnbound) &&
                    !string.IsNullOrEmpty(fallbackId))
                {
                    WriteAllTextAtomic(path, json);
                }

                if ((selectedExact || selectedBackup) && IMDataCoreIntegration.IsReady)
                {
                    // IMDC mutation during the vanilla load boundary is intentionally avoided.
                    // The next real save will re-seed it through the concrete save pipeline.
                    MarkDirty();
                }
                else if (selectedImdc && selectedLegacyUnbound)
                {
                    // The standalone mirror was upgraded above, but IMDC's custom JSON is still
                    // unbound until the next real save.
                    MarkDirty();
                }

                if (loadingFileVersion < 3)
                    MarkDirty();
                if (needsBackgroundKeyMigration)
                    MarkDirty();
                if (needsCenterMemberIdMigration)
                    MarkDirty();
                if (needsMemberSnapshotMigration)
                    MarkDirty();
                if (duplicatesSkipped > 0)
                    MarkDirty();

                Debug.Log(
                    "[AlbumSave] Loaded " + Albums.AlbumList.Count +
                    " unique album(s) from " + source + ".");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError("[AlbumSave] Load failed:\n" + ex);
                return false;
            }
        }

        private static string ReadTextIfPresent(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return string.Empty;
            return File.ReadAllText(path);
        }

        private static bool TryParseCompatibleCandidate(
            string json,
            bool fromImDataCore,
            AlbumVanillaCheckpointStamp currentCheckpoint,
            string vanillaPath,
            out AlbumSaveFile file,
            out bool legacyUnbound,
            out string rejectionReason)
        {
            file = null;
            legacyUnbound = false;
            rejectionReason = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                file = JsonUtility.FromJson<AlbumSaveFile>(json);
            }
            catch (Exception ex)
            {
                rejectionReason = "the JSON could not be parsed: " + ex.Message;
                return false;
            }

            if (file == null || file.Version <= 0 || file.Albums == null)
            {
                rejectionReason = "the document is empty or structurally invalid.";
                file = null;
                return false;
            }

            bool declaresBinding = file.CheckpointBindingVersion >= CheckpointBindingVersion;
            bool hasUsableBinding =
                file.VanillaCheckpoint != null &&
                AlbumSaveIdentity.IsValidFingerprint(file.VanillaCheckpoint.ContentFingerprint);

            if (declaresBinding && !hasUsableBinding)
            {
                rejectionReason = "it declares checkpoint binding but its binding is incomplete.";
                file = null;
                return false;
            }

            if (hasUsableBinding)
            {
                if (currentCheckpoint == null)
                {
                    rejectionReason = "the current vanilla checkpoint identity is not available yet.";
                    file = null;
                    return false;
                }

                if (AlbumSaveIdentity.CheckpointMatches(file.VanillaCheckpoint, currentCheckpoint))
                    return true;

                if (AlbumSaveIdentity.CheckpointIsSameSlot(file.VanillaCheckpoint, currentCheckpoint))
                {
                    // A same-slot mismatch is the signature produced by vanilla's historical
                    // asynchronous save race: CAA froze one graph while DataSaver serialized a
                    // later mutation. Preserve the album document and rebind it to the physical
                    // checkpoint after load instead of converting the mismatch into Albums=[].
                    legacyUnbound = true;
                    return true;
                }

                rejectionReason = "its checkpoint belongs to a different logical vanilla save slot.";
                file = null;
                return false;
            }

            legacyUnbound = true;

            // IM Data Core already resolves custom JSON through its own checkpoint-aware store.
            // Legacy CAA documents retrieved from that current branch can therefore be migrated.
            if (fromImDataCore)
                return true;

            if (!IsPlausibleLegacyExactMirror(file, vanillaPath))
            {
                rejectionReason =
                    "it is an unbound legacy mirror whose write time does not match the loaded vanilla checkpoint.";
                file = null;
                return false;
            }

            return true;
        }

        private static bool IsPlausibleLegacyExactMirror(
            AlbumSaveFile file,
            string vanillaPath)
        {
            if (file == null ||
                file.WrittenUtcTicks <= 0L ||
                string.IsNullOrEmpty(vanillaPath) ||
                !File.Exists(vanillaPath))
            {
                return false;
            }

            try
            {
                DateTime sidecarWriteUtc =
                    new DateTime(file.WrittenUtcTicks, DateTimeKind.Utc);
                DateTime vanillaWriteUtc = File.GetLastWriteTimeUtc(vanillaPath);
                TimeSpan delta = vanillaWriteUtc - sidecarWriteUtc;
                if (delta < TimeSpan.Zero)
                    delta = delta.Negate();
                return delta <= LegacyMirrorTimestampTolerance;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetCurrentCheckpointStamp(
            out AlbumVanillaCheckpointStamp checkpoint,
            out string errorMessage)
        {
            checkpoint = null;
            errorMessage = string.Empty;

            string activePath = AlbumSaveIdentity.GetActiveLoadTarget();
            if (string.IsNullOrEmpty(activePath))
            {
                errorMessage = "the concrete vanilla load path has not been captured yet.";
                return false;
            }

            // Do not fingerprint SaveManager.Data here. Vanilla LoadEvent subscribers mutate
            // fields inside the just-loaded SavedData object (notably PlayerData.LastSave), so
            // the in-memory object can stop matching the physical checkpoint before CAA's load
            // handler runs. The physical JSON is the immutable checkpoint authority.
            if (!File.Exists(activePath))
            {
                errorMessage = "the concrete vanilla save file is not available at " + activePath + ".";
                return false;
            }

            try
            {
                string json = File.ReadAllText(activePath);
                return AlbumSaveIdentity.TryCreateCheckpointStampFromJson(
                    json,
                    activePath,
                    out checkpoint,
                    out errorMessage);
            }
            catch (Exception ex)
            {
                errorMessage = "reading the concrete vanilla checkpoint failed: " + ex.Message;
                return false;
            }
        }

        private static bool QueueOrConfirmCommitValidation(
            string vanillaPath,
            string targetId,
            string expectedFingerprint,
            int snapshotDirtyGeneration)
        {
            for (int i = pendingCommitValidations.Count - 1; i >= 0; i--)
            {
                if (string.Equals(
                        pendingCommitValidations[i].VanillaPath,
                        vanillaPath,
                        StringComparison.Ordinal))
                {
                    pendingCommitValidations.RemoveAt(i);
                }
            }

            string observedFingerprint;
            if (TryReadPhysicalCheckpointFingerprint(vanillaPath, out observedFingerprint) &&
                string.Equals(observedFingerprint, expectedFingerprint, StringComparison.Ordinal))
            {
                ConfirmDirtyGeneration(snapshotDirtyGeneration);
                return true;
            }

            pendingCommitValidations.Add(
                new PendingCommitValidation
                {
                    VanillaPath = vanillaPath,
                    TargetId = targetId ?? string.Empty,
                    ExpectedFingerprint = expectedFingerprint ?? string.Empty,
                    DirtyGeneration = snapshotDirtyGeneration,
                    RetryFrames = CommitValidationRetryFrames,
                    AttemptsRemaining = CommitValidationMaxAttempts
                });
            return false;
        }

        private static void TickPendingCommitValidations()
        {
            for (int i = pendingCommitValidations.Count - 1; i >= 0; i--)
            {
                PendingCommitValidation pending = pendingCommitValidations[i];
                if (pending == null)
                {
                    pendingCommitValidations.RemoveAt(i);
                    continue;
                }

                if (pending.RetryFrames > 0)
                {
                    pending.RetryFrames--;
                    continue;
                }

                string observedFingerprint;
                if (TryReadPhysicalCheckpointFingerprint(
                        pending.VanillaPath,
                        out observedFingerprint) &&
                    string.Equals(
                        observedFingerprint,
                        pending.ExpectedFingerprint,
                        StringComparison.Ordinal))
                {
                    ConfirmDirtyGeneration(pending.DirtyGeneration);
                    Debug.Log(
                        "[AlbumSave] Confirmed asynchronous vanilla checkpoint for " +
                        pending.TargetId + ".");
                    pendingCommitValidations.RemoveAt(i);
                    continue;
                }

                pending.AttemptsRemaining--;
                if (pending.AttemptsRemaining <= 0)
                {
                    Debug.LogWarning(
                        "[AlbumSave] Vanilla checkpoint for " + pending.TargetId +
                        " never matched the staged supplemental fingerprint. " +
                        "The previous .bak mirror remains available and CAA state stays dirty.");
                    pendingCommitValidations.RemoveAt(i);
                    continue;
                }

                pending.RetryFrames = CommitValidationRetryFrames;
            }
        }

        private static void ConfirmDirtyGeneration(int savedGeneration)
        {
            if (dirty && dirtyGeneration == savedGeneration)
                dirty = false;
        }

        private static bool TryReadPhysicalCheckpointFingerprint(
            string vanillaPath,
            out string fingerprint)
        {
            fingerprint = string.Empty;
            if (string.IsNullOrEmpty(vanillaPath) || !File.Exists(vanillaPath))
                return false;

            try
            {
                string json = File.ReadAllText(vanillaPath);
                AlbumVanillaCheckpointStamp checkpoint;
                string errorMessage;
                if (!AlbumSaveIdentity.TryCreateCheckpointStampFromJson(
                        json,
                        vanillaPath,
                        out checkpoint,
                        out errorMessage) ||
                    checkpoint == null)
                {
                    return false;
                }

                fingerprint = checkpoint.ContentFingerprint ?? string.Empty;
                return AlbumSaveIdentity.IsValidFingerprint(fingerprint);
            }
            catch
            {
                return false;
            }
        }

        private static void RecoverFromPersistentLoadFailure(string saveId)
        {
            string path = AlbumSaveIdentity.GetActiveSidecarPath();
            if (string.IsNullOrEmpty(path))
            {
                string fallbackId = GetFallbackSaveId();
                path = GetPathForSaveId(!string.IsNullOrEmpty(fallbackId) ? fallbackId : saveId);
            }

            try
            {
                PreserveLoadFailureCopy(path);
                PreserveLoadFailureCopy(path + ".bak");
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumSave] Could not preserve a diagnostic copy of the failing sidecar: " +
                    ex.Message);
            }

            // Do not expose the previous save's albums in the newly loaded game, but equally
            // importantly do NOT call CompleteLoadContext here. That old behavior certified an
            // empty runtime list as valid and the next real save overwrote the player's sidecar.
            Albums.AlbumList.Clear();
            RivalAlbumManager.RebuildIdAllocator();
            supplementalWriteBlocked = true;
            supplementalWriteBlockReason =
                "an existing supplemental file could not be restored for logical slot " + saveId +
                "; diagnostic copies were preserved and CAA will not overwrite it with an empty document";
            dirty = true;
            Debug.LogError(
                "[AlbumSave] Supplemental state failed to restore repeatedly. " +
                "The existing sidecar was preserved and CAA writes are blocked for this load context instead of saving Albums=[].");
        }

        private static void PreserveLoadFailureCopy(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            string copyPath =
                path + ".loadfailed_" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            File.Copy(path, copyPath, false);
        }

        internal static void DeleteSupplementalStateForVanillaDeletion(
            string vanillaDirectoryPath,
            IEnumerable<string> vanillaSavePaths)
        {
            string storageDirectory = GetStorageDirectory();
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            int deletedCount = 0;

            if (vanillaSavePaths != null)
            {
                foreach (string savePath in vanillaSavePaths)
                {
                    // New v4.2 layout mirrors data/<slot>.json as CreateAnAlbum/<slot>.json.
                    string mirrored = AlbumSaveScope.GetSidecarPathForPhysicalVanillaPath(savePath);
                    if (!string.IsNullOrEmpty(mirrored))
                    {
                        string directory = Path.GetDirectoryName(mirrored);
                        string pattern = Path.GetFileName(mirrored) + "*";
                        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                        {
                            foreach (string candidate in Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly))
                            {
                                try { File.Delete(candidate); deletedCount++; }
                                catch (Exception ex)
                                {
                                    Debug.LogWarning("[AlbumSave] Could not delete supplemental file " + candidate + ": " + ex.Message);
                                }
                            }
                        }
                    }

                    // Keep cleaning the old top-level hashed layout during migration releases.
                    string id = AlbumSaveIdentity.GetIdentityForPath(savePath);
                    if (!string.IsNullOrEmpty(id))
                        ids.Add(id);
                    try
                    {
                        string absoluteLegacy = "path_" + AlbumSaveIdentity.StableHash(
                            Path.GetFullPath(savePath).Replace('\\', '/').ToLowerInvariant());
                        ids.Add(absoluteLegacy);
                    }
                    catch { }
                }
            }

            string normalizedDirectory = NormalizeDirectoryPath(vanillaDirectoryPath);
            if (!string.IsNullOrEmpty(normalizedDirectory) && Directory.Exists(storageDirectory))
            {
                foreach (string sidecarPath in Directory.GetFiles(
                    storageDirectory,
                    "albums_*.json",
                    SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        AlbumSaveFile file = JsonUtility.FromJson<AlbumSaveFile>(
                            File.ReadAllText(sidecarPath));
                        string boundPath =
                            file != null && file.VanillaCheckpoint != null
                                ? file.VanillaCheckpoint.NormalizedSavePath
                                : string.Empty;
                        if (!string.IsNullOrEmpty(boundPath) &&
                            IsPathInsideDirectory(boundPath, normalizedDirectory))
                        {
                            string fileName = Path.GetFileNameWithoutExtension(sidecarPath);
                            if (!string.IsNullOrEmpty(fileName) &&
                                fileName.StartsWith("albums_", StringComparison.Ordinal))
                            {
                                ids.Add(fileName.Substring(7));
                            }
                        }
                    }
                    catch
                    {
                        // A malformed sidecar is handled through the direct path identities above.
                    }
                }
            }

            foreach (string id in ids)
            {
                string primary = GetPathForSaveId(id);
                string directory = Path.GetDirectoryName(primary);
                string pattern = Path.GetFileName(primary) + "*";
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                    continue;

                foreach (string candidate in Directory.GetFiles(
                    directory,
                    pattern,
                    SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        File.Delete(candidate);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            "[AlbumSave] Could not delete supplemental file " +
                            candidate + ": " + ex.Message);
                    }
                }
            }

            if (!string.IsNullOrEmpty(normalizedDirectory))
            {
                for (int i = pendingCommitValidations.Count - 1; i >= 0; i--)
                {
                    if (IsPathInsideDirectory(
                            pendingCommitValidations[i].VanillaPath,
                            normalizedDirectory))
                    {
                        pendingCommitValidations.RemoveAt(i);
                    }
                }
            }

            Debug.Log(
                "[AlbumSave] Removed " + deletedCount +
                " supplemental file(s) for the deleted vanilla save data.");
        }

        private static string NormalizeDirectoryPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                return Path.GetFullPath(path)
                    .Replace('\\', '/')
                    .TrimEnd('/')
                    .ToLowerInvariant();
            }
            catch
            {
                return path.Trim()
                    .Replace('\\', '/')
                    .TrimEnd('/')
                    .ToLowerInvariant();
            }
        }

        private static bool IsPathInsideDirectory(
            string normalizedPath,
            string normalizedDirectory)
        {
            if (string.IsNullOrEmpty(normalizedPath) ||
                string.IsNullOrEmpty(normalizedDirectory))
            {
                return false;
            }

            string path = normalizedPath.Replace('\\', '/').ToLowerInvariant();
            string directory = normalizedDirectory.TrimEnd('/') + "/";
            return path.StartsWith(directory, StringComparison.Ordinal);
        }

        private static void CompleteLoadContext(
            string saveId,
            long chartProcessedTicks,
            AlbumProductionProject productionProject,
            bool productionMigrationCompleted)
        {
            loadedSaveId = saveId;
            supplementalWriteBlocked = false;
            supplementalWriteBlockReason = "";
            lastChartProcessedTicks = chartProcessedTicks > 0L
                ? chartProcessedTicks
                : 0L;
            dirty = false;
            dirtyGeneration = 0;
            pendingCommitValidations.Clear();
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

            // Persist both stable IDs and a tiny portrait/member descriptor. The descriptor
            // stores asset IDs only, never image bytes, so graduated idols remain renderable
            // without turning the save into a portrait cache.
            entry.MemberSnapshots = AlbumMemberRepair.CaptureAndGetSnapshots(album);
            if (entry.MemberSnapshots != null)
            {
                foreach (AlbumMemberSnapshot snapshot in entry.MemberSnapshots)
                {
                    if (snapshot != null && !entry.MemberIds.Contains(snapshot.GirlId))
                        entry.MemberIds.Add(snapshot.GirlId);
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

            entry.HasCenterMemberId = album.HasCenterMemberId;
            entry.CenterMemberId = album.CenterMemberId;

            // A runtime album created by older code may only have the legacy index populated.
            // Upgrade it while serializing, but do not mutate the album object itself here.
            if (!entry.HasCenterMemberId &&
                album.Members != null &&
                album.CenterMemberIndex >= 0 &&
                album.CenterMemberIndex < album.Members.Count &&
                album.Members[album.CenterMemberIndex] != null)
            {
                entry.CenterMemberId = album.Members[album.CenterMemberIndex].id;
                entry.HasCenterMemberId = true;
            }

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

            album.MemberSnapshots = entry.MemberSnapshots != null
                ? new List<AlbumMemberSnapshot>(entry.MemberSnapshots.Where(snapshot => snapshot != null))
                : new List<AlbumMemberSnapshot>();
            AlbumMemberRepair.RememberLegacyIds(album, entry.MemberIds);
            album.Members =
                RestoreMembers(
                    entry.MemberIds
                );
            AlbumMemberRepair.Repair(album);

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
                ? (loadingFileVersion < 3
                    ? AlbumFontCatalog.GetLegacySystemKey(entry.FontIndex)
                    : AlbumFontCatalog.GetKey(entry.FontIndex))
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

            if (entry.HasCenterMemberId)
            {
                album.HasCenterMemberId = true;
                album.CenterMemberId = entry.CenterMemberId;
                album.CenterMemberIndex = FindMemberIndexById(
                    album.Members,
                    entry.CenterMemberId);
            }
            else
            {
                // Legacy migration path. If the old indexed member is present, immediately
                // recover a stable identity. If it is missing we retain only the old index for
                // compatibility; there is no trustworthy ID to infer from an older document.
                album.CenterMemberIndex = entry.CenterMemberIndex;
                if (album.Members != null &&
                    entry.CenterMemberIndex >= 0 &&
                    entry.CenterMemberIndex < album.Members.Count &&
                    album.Members[entry.CenterMemberIndex] != null)
                {
                    album.CenterMemberId = album.Members[entry.CenterMemberIndex].id;
                    album.HasCenterMemberId = true;
                }
            }

            return album;
        }

        private static int FindMemberIndexById(
            List<data_girls.girls> members,
            int memberId)
        {
            if (members == null)
                return -1;

            for (int i = 0; i < members.Count; i++)
            {
                data_girls.girls girl = members[i];
                if (girl != null && girl.id == memberId)
                    return i;
            }

            return -1;
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
            // target that was actually loaded, not whichever destination was most recently written
            // and not whichever optional persistence backend happens to be ready this frame.
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
            if (string.IsNullOrEmpty(exactPath) || File.Exists(exactPath))
                return;

            List<string> candidates = new List<string>();
            string activePhysical = AlbumSaveIdentity.GetActiveLoadTarget();
            if (!string.IsNullOrEmpty(activePhysical))
            {
                string legacyAbsoluteIdentity = "path_" + AlbumSaveIdentity.StableHash(
                    activePhysical.Replace('\\', '/').ToLowerInvariant());
                candidates.Add(GetPathForSaveId(legacyAbsoluteIdentity));
            }

            candidates.Add(GetPathForSaveId(GetLegacyCampaignSaveId()));
            candidates.Add(GetPathForSaveId(GetLegacyProductionSaveId()));

            foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(candidate) || !File.Exists(candidate))
                    continue;
                try
                {
                    File.Copy(candidate, exactPath, false);
                    Debug.Log("[AlbumSave] Migrated legacy sidecar into stable relative save slot: " + exactPath);
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[AlbumSave] Legacy sidecar migration failed: " + ex.Message);
                }
            }

            // Portability migration: v4.1.4 encoded the absolute OS path into the filename.
            // When a save moved to another account/computer that hash cannot be recomputed, so
            // inspect legacy documents and compare only the canonical path below /data/.
            string relativeSlot = AlbumSaveIdentity.GetActiveRelativeSlot();
            string storage = GetStorageDirectory();
            if (string.IsNullOrEmpty(relativeSlot) || !Directory.Exists(storage))
                return;

            foreach (string candidate in Directory.GetFiles(storage, "albums_*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    AlbumSaveFile legacy = JsonUtility.FromJson<AlbumSaveFile>(File.ReadAllText(candidate));
                    string storedPath = legacy != null && legacy.VanillaCheckpoint != null
                        ? legacy.VanillaCheckpoint.NormalizedSavePath
                        : string.Empty;
                    if (!string.Equals(
                            AlbumSaveScope.CanonicalizeCheckpointPath(storedPath),
                            relativeSlot,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    File.Copy(candidate, exactPath, false);
                    Debug.Log("[AlbumSave] Migrated portable v4.1 absolute-path sidecar into " + exactPath);
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[AlbumSave] Could not inspect legacy sidecar " + candidate + ": " + ex.Message);
                }
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
                // Some Mono/filesystem combinations do not support File.Replace reliably.
                // Preserve the previous checkpoint explicitly before falling back to overwrite.
                File.Copy(path, backupPath, true);
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
