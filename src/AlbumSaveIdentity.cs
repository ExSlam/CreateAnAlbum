using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Albummodelite
{
    [Serializable]
    internal sealed class AlbumVanillaCheckpointStamp
    {
        // v2 stores a stable path relative to persistentDataPath/data. v1 absolute
        // paths are canonicalized during matching for in-place migration.
        public string NormalizedSavePath = string.Empty;
        public string LastSave = string.Empty;
        public long PlaytimeSeconds;
        public string GameDateTime = string.Empty;
        public string ContentFingerprint = string.Empty;
    }

    /// <summary>
    /// Tracks the concrete vanilla save that was loaded. Identity is a stable game-relative
    /// slot (for example manual_saves/12/save.json), never an OS/user-specific absolute path.
    /// The physical path is retained only transiently for I/O and checkpoint verification.
    /// </summary>
    internal static class AlbumSaveIdentity
    {
        private static string activeLoadTarget = string.Empty;
        private static string activeLoadPhysicalTarget = string.Empty;
        private static string pendingSaveTarget = string.Empty;

        internal static void Reset()
        {
            activeLoadTarget = string.Empty;
            activeLoadPhysicalTarget = string.Empty;
            pendingSaveTarget = string.Empty;
        }

        internal static void CaptureLoadTarget(string path)
        {
            string physical;
            string relative;
            if (!AlbumSaveScope.TryResolveLoadTarget(path, out physical, out relative))
                return;

            activeLoadTarget = relative;
            activeLoadPhysicalTarget = physical;
            pendingSaveTarget = string.Empty;
        }

        internal static void CaptureSaveTarget(string path, bool fullPath)
        {
            string physical;
            string relative;
            if (AlbumSaveScope.TryResolveWriteTarget(path, fullPath, out physical, out relative))
                pendingSaveTarget = relative;
        }

        internal static string GetActiveFallbackIdentity()
        {
            return string.IsNullOrEmpty(activeLoadTarget)
                ? string.Empty
                : "slot_" + StableHash(activeLoadTarget);
        }

        internal static string GetPendingFallbackIdentity()
        {
            return string.IsNullOrEmpty(pendingSaveTarget)
                ? string.Empty
                : "slot_" + StableHash(pendingSaveTarget);
        }

        internal static string GetIdentityForPath(string path)
        {
            string physical;
            string relative;
            if (!AlbumSaveScope.TryResolvePhysicalTarget(path, out physical, out relative))
                return string.Empty;
            return "slot_" + StableHash(relative);
        }

        internal static string GetIdentityForWriteTarget(string path, bool fullPath)
        {
            string physical;
            string relative;
            if (!AlbumSaveScope.TryResolveWriteTarget(path, fullPath, out physical, out relative))
                return string.Empty;
            return "slot_" + StableHash(relative);
        }

        internal static string GetRelativeSlotForWriteTarget(string path, bool fullPath)
        {
            string physical;
            string relative;
            return AlbumSaveScope.TryResolveWriteTarget(path, fullPath, out physical, out relative)
                ? relative
                : string.Empty;
        }

        internal static string GetActiveRelativeSlot()
        {
            return activeLoadTarget;
        }

        internal static string GetLogicalNewStoryAlias()
        {
            try
            {
                if (!staticVars.IsStoryMode())
                    return string.Empty;

                string folder = staticVars.PlayerData != null ? staticVars.PlayerData.GetSaveFolderName() : string.Empty;
                string name = staticVars.PlayerData != null ? staticVars.PlayerData.SaveFileName : string.Empty;
                if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(name))
                    return string.Empty;
                return "story_new_" + StableHash(folder + "|" + name);
            }
            catch
            {
                return string.Empty;
            }
        }

        internal static void ClearPendingSaveTarget()
        {
            pendingSaveTarget = string.Empty;
        }

        internal static string NormalizePath(string path)
        {
            string physical;
            string relative;
            if (AlbumSaveScope.TryResolvePhysicalTarget(path, out physical, out relative))
                return relative;
            if (AlbumSaveScope.TryResolveLoadTarget(path, out physical, out relative))
                return relative;
            return string.Empty;
        }

        internal static string ResolvePhysicalPath(string path)
        {
            string physical;
            string relative;
            if (AlbumSaveScope.TryResolveLoadTarget(path, out physical, out relative))
                return physical;
            if (AlbumSaveScope.TryResolvePhysicalTarget(path, out physical, out relative))
                return physical;
            return string.Empty;
        }

        internal static string ResolveWritePhysicalPath(string path, bool fullPath)
        {
            string physical;
            string relative;
            return AlbumSaveScope.TryResolveWriteTarget(path, fullPath, out physical, out relative)
                ? physical
                : string.Empty;
        }

        internal static string GetActiveLoadTarget()
        {
            return activeLoadPhysicalTarget;
        }

        internal static string GetActiveSidecarPath()
        {
            return AlbumSaveScope.GetSidecarPath(activeLoadTarget);
        }

        internal static string GetSidecarPathForWriteTarget(string path, bool fullPath)
        {
            string physical;
            string relative;
            return AlbumSaveScope.TryResolveWriteTarget(path, fullPath, out physical, out relative)
                ? AlbumSaveScope.GetSidecarPath(relative)
                : string.Empty;
        }

        internal static bool TryCreateCheckpointStamp(
            SaveManager.SavedData savedData,
            string path,
            out AlbumVanillaCheckpointStamp stamp,
            out string errorMessage)
        {
            string physical;
            string relative;
            bool resolved = AlbumSaveScope.TryResolvePhysicalTarget(path, out physical, out relative) ||
                            AlbumSaveScope.TryResolveLoadTarget(path, out physical, out relative);
            return TryCreateCheckpointStampCore(savedData, resolved ? relative : string.Empty, out stamp, out errorMessage);
        }

        internal static bool TryCreateCheckpointStampForWrite(
            SaveManager.SavedData savedData,
            string path,
            bool fullPath,
            out AlbumVanillaCheckpointStamp stamp,
            out string errorMessage)
        {
            string physical;
            string relative;
            if (!AlbumSaveScope.TryResolveWriteTarget(path, fullPath, out physical, out relative))
            {
                stamp = null;
                errorMessage = "The vanilla save path could not be resolved to a stable game-relative slot.";
                return false;
            }
            return TryCreateCheckpointStampCore(savedData, relative, out stamp, out errorMessage);
        }

        private static bool TryCreateCheckpointStampCore(
            SaveManager.SavedData savedData,
            string relativeSlot,
            out AlbumVanillaCheckpointStamp stamp,
            out string errorMessage)
        {
            stamp = null;
            errorMessage = string.Empty;

            if (savedData == null || savedData.staticVars__PlayerData == null)
            {
                errorMessage = "Vanilla SavedData or PlayerData is unavailable.";
                return false;
            }
            if (string.IsNullOrEmpty(relativeSlot))
            {
                errorMessage = "The vanilla save path could not be normalized.";
                return false;
            }

            try
            {
                string compactJson = JsonUtility.ToJson(savedData, false);
                stamp = new AlbumVanillaCheckpointStamp
                {
                    NormalizedSavePath = relativeSlot,
                    LastSave = savedData.staticVars__PlayerData.LastSave ?? string.Empty,
                    PlaytimeSeconds = savedData.staticVars__PlayerData.Playtime_Seconds,
                    GameDateTime = savedData.staticVars__dateTime ?? string.Empty,
                    ContentFingerprint = ComputeSha256Fingerprint(compactJson)
                };
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Computing the vanilla checkpoint fingerprint failed: " + ex.Message;
                return false;
            }
        }

        internal static bool TryCreateCheckpointStampFromJson(
            string json,
            string path,
            out AlbumVanillaCheckpointStamp stamp,
            out string errorMessage)
        {
            stamp = null;
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                errorMessage = "The vanilla save JSON is empty.";
                return false;
            }

            try
            {
                SaveManager.SavedData savedData = JsonUtility.FromJson<SaveManager.SavedData>(json);
                string physical;
                string relative;
                if (!AlbumSaveScope.TryResolvePhysicalTarget(path, out physical, out relative) &&
                    !AlbumSaveScope.TryResolveLoadTarget(path, out physical, out relative))
                {
                    errorMessage = "The vanilla save path could not be resolved to a stable game-relative slot.";
                    return false;
                }
                return TryCreateCheckpointStampCore(savedData, relative, out stamp, out errorMessage);
            }
            catch (Exception ex)
            {
                errorMessage = "Parsing the vanilla save JSON failed: " + ex.Message;
                return false;
            }
        }

        internal static bool CheckpointMatches(
            AlbumVanillaCheckpointStamp saved,
            AlbumVanillaCheckpointStamp current)
        {
            return saved != null && current != null &&
                   AlbumSaveScope.IsSameLogicalSlot(saved.NormalizedSavePath, current.NormalizedSavePath) &&
                   string.Equals(saved.LastSave, current.LastSave, StringComparison.Ordinal) &&
                   saved.PlaytimeSeconds == current.PlaytimeSeconds &&
                   string.Equals(saved.GameDateTime, current.GameDateTime, StringComparison.Ordinal) &&
                   string.Equals(saved.ContentFingerprint, current.ContentFingerprint, StringComparison.Ordinal);
        }

        internal static bool CheckpointIsSameSlot(
            AlbumVanillaCheckpointStamp saved,
            AlbumVanillaCheckpointStamp current)
        {
            return saved != null && current != null &&
                   AlbumSaveScope.IsSameLogicalSlot(saved.NormalizedSavePath, current.NormalizedSavePath);
        }

        internal static bool IsValidFingerprint(string fingerprint)
        {
            const string prefix = "sha256:";
            if (string.IsNullOrEmpty(fingerprint) ||
                fingerprint.Length != prefix.Length + 64 ||
                !fingerprint.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            for (int i = prefix.Length; i < fingerprint.Length; i++)
            {
                char c = fingerprint[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                    return false;
            }
            return true;
        }

        private static string ComputeSha256Fingerprint(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                StringBuilder builder = new StringBuilder("sha256:", 71);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }

        internal static string StableHash(string value)
        {
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                for (int i = 0; i < (value ?? string.Empty).Length; i++)
                {
                    hash ^= value[i];
                    hash *= 1099511628211UL;
                }
                return hash.ToString("x16");
            }
        }
    }

    /// <summary>
    /// Capture the exact string actually consumed by DataSaver.loadData&lt;SavedData&gt;.
    /// This avoids calling GetLatestAutosavePath a second time and racing vanilla's own choice.
    /// </summary>
    [HarmonyPatch]
    internal static class AlbumSaveIdentity_ConcreteLoadRead_Patch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return RequireMethod(typeof(SaveManager), nameof(SaveManager.LoadData), new Type[] { typeof(string) });
            yield return RequireMethod(typeof(SaveManager), nameof(SaveManager.LoadData), new Type[] { typeof(bool) });
        }

        [HarmonyTranspiler]
        [HarmonyPriority(Priority.First)]
        [HarmonyBefore(new string[] { "com.cosmo.imdatacore", "com.cosmo.savewriteorderingfix" })]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions,
            MethodBase __originalMethod)
        {
            MethodInfo capture = AccessTools.Method(
                typeof(AlbumSaveIdentity),
                nameof(AlbumSaveIdentity.CaptureLoadTarget),
                new Type[] { typeof(string) });
            int injected = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (IsSavedDataRead(instruction))
                {
                    CodeInstruction dup = new CodeInstruction(OpCodes.Dup);
                    dup.labels.AddRange(instruction.labels);
                    dup.blocks.AddRange(instruction.blocks);
                    instruction.labels.Clear();
                    instruction.blocks.Clear();
                    yield return dup;
                    yield return new CodeInstruction(OpCodes.Call, capture);
                    injected++;
                }
                yield return instruction;
            }
            if (injected != 1)
                throw new InvalidOperationException("Expected exactly one SavedData read in " +
                    (__originalMethod == null ? "an unknown load caller" : __originalMethod.DeclaringType.FullName + "." + __originalMethod.Name) +
                    "; found " + injected + ".");
        }

        private static bool IsSavedDataRead(CodeInstruction instruction)
        {
            MethodInfo method = instruction == null ? null : instruction.operand as MethodInfo;
            if (method == null || method.DeclaringType != typeof(DataSaver) || method.Name != "loadData" || !method.IsGenericMethod)
                return false;
            Type[] args = method.GetGenericArguments();
            ParameterInfo[] parameters = method.GetParameters();
            return args.Length == 1 && args[0] == typeof(SaveManager.SavedData) &&
                   parameters.Length == 1 && parameters[0].ParameterType == typeof(string);
        }

        private static MethodBase RequireMethod(Type type, string name, Type[] parameterTypes)
        {
            MethodInfo method = AccessTools.Method(type, name, parameterTypes);
            if (method == null)
                throw new MissingMethodException(type.FullName, name);
            return method;
        }
    }

    /// <summary>
    /// Every vanilla save route ultimately issues one DataSaver.saveData&lt;SavedData&gt; call.
    /// Stage CAA at SaveEvent, then commit that staged document at this exact concrete target.
    ///
    /// This transpiler MUST run before IM Data Core's caller-level save transpiler. IMDC then
    /// snapshots/forks the branch after our custom-JSON mutation is present, and Save Write
    /// Ordering Fix subsequently freezes the same vanilla payload. This avoids the 4.1.0/4.1.1
    /// timing bug where CAA could write a temporary/logical branch and IMDC could checkpoint a
    /// different physical manual-save path.
    /// </summary>
    [HarmonyPatch]
    internal static class AlbumSaveIdentity_ConcreteSaveWrite_Patch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return RequireMethod(
                typeof(SaveManager),
                nameof(SaveManager.SaveData),
                new Type[] { typeof(bool), typeof(bool) });

            yield return RequireMethod(
                typeof(SaveManager),
                nameof(SaveManager.SaveChapter),
                new Type[] { typeof(tasks._chapter) });

            yield return RequireMethod(
                typeof(Popup_Save),
                "Save",
                Type.EmptyTypes);

            yield return RequireMethod(
                typeof(Popup_Load_Story),
                "Do_Overwrite_Save",
                new Type[] { typeof(Popup_Load_Story.save_info) });

            yield return RequireMethod(
                typeof(Popup_Load_Story),
                nameof(Popup_Load_Story.Do_New_Save),
                new Type[] { typeof(string) });
        }

        [HarmonyTranspiler]
        [HarmonyPriority(Priority.First)]
        [HarmonyBefore(new string[]
        {
            "com.cosmo.imdatacore",
            "com.cosmo.savewriteorderingfix"
        })]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions,
            ILGenerator generator,
            MethodBase __originalMethod)
        {
            LocalBuilder dataLocal = generator.DeclareLocal(typeof(SaveManager.SavedData));
            LocalBuilder pathLocal = generator.DeclareLocal(typeof(string));
            LocalBuilder jsonLocal = generator.DeclareLocal(typeof(bool));
            LocalBuilder fullPathLocal = generator.DeclareLocal(typeof(bool));

            MethodInfo commitMethod = AccessTools.Method(
                typeof(AlbumPersistence),
                nameof(AlbumPersistence.CaptureConcreteSaveWriteTarget),
                new Type[] { typeof(SaveManager.SavedData), typeof(string), typeof(bool), typeof(bool) });
            if (commitMethod == null)
            {
                throw new MissingMethodException(
                    typeof(AlbumPersistence).FullName,
                    nameof(AlbumPersistence.CaptureConcreteSaveWriteTarget));
            }

            int injectedCount = 0;

            foreach (CodeInstruction instruction in instructions)
            {
                if (!IsSavedDataWrite(instruction))
                {
                    yield return instruction;
                    continue;
                }

                CodeInstruction first = new CodeInstruction(OpCodes.Stloc, fullPathLocal);
                first.labels.AddRange(instruction.labels);
                first.blocks.AddRange(instruction.blocks);
                instruction.labels.Clear();
                instruction.blocks.Clear();

                yield return first;
                yield return new CodeInstruction(OpCodes.Stloc, jsonLocal);
                yield return new CodeInstruction(OpCodes.Stloc, pathLocal);
                yield return new CodeInstruction(OpCodes.Stloc, dataLocal);

                yield return new CodeInstruction(OpCodes.Ldloc, dataLocal);
                yield return new CodeInstruction(OpCodes.Ldloc, pathLocal);
                yield return new CodeInstruction(OpCodes.Ldloc, jsonLocal);
                yield return new CodeInstruction(OpCodes.Ldloc, fullPathLocal);
                yield return new CodeInstruction(OpCodes.Call, commitMethod);

                yield return new CodeInstruction(OpCodes.Ldloc, dataLocal);
                yield return new CodeInstruction(OpCodes.Ldloc, pathLocal);
                yield return new CodeInstruction(OpCodes.Ldloc, jsonLocal);
                yield return new CodeInstruction(OpCodes.Ldloc, fullPathLocal);
                yield return instruction;

                injectedCount++;
            }

            if (injectedCount != 1)
            {
                throw new InvalidOperationException(
                    "Expected exactly one SavedData write in " +
                    (__originalMethod == null
                        ? "an unknown vanilla save caller"
                        : __originalMethod.DeclaringType.FullName + "." + __originalMethod.Name) +
                    "; found " + injectedCount + ".");
            }
        }

        private static bool IsSavedDataWrite(CodeInstruction instruction)
        {
            if (instruction == null ||
                (instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt))
                return false;

            MethodInfo method = instruction.operand as MethodInfo;
            if (method == null ||
                method.DeclaringType != typeof(DataSaver) ||
                method.Name != "saveData" ||
                !method.IsGenericMethod)
                return false;

            Type[] arguments = method.GetGenericArguments();
            if (arguments.Length != 1 || arguments[0] != typeof(SaveManager.SavedData))
                return false;

            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length == 4 &&
                   parameters[0].ParameterType == typeof(SaveManager.SavedData) &&
                   parameters[1].ParameterType == typeof(string) &&
                   parameters[2].ParameterType == typeof(bool) &&
                   parameters[3].ParameterType == typeof(bool);
        }

        private static MethodBase RequireMethod(Type type, string name, Type[] parameterTypes)
        {
            MethodInfo method = AccessTools.Method(type, name, parameterTypes);
            if (method == null)
                throw new MissingMethodException(type.FullName, name);
            return method;
        }
    }


    internal sealed class AlbumDeletedSaveState
    {
        internal string VanillaDirectoryPath = string.Empty;
        internal bool DirectoryExistedBeforeDelete;
        internal readonly List<string> VanillaSavePaths = new List<string>();
    }

    internal static class AlbumDeletedSaveBinding
    {
        internal static AlbumDeletedSaveState Capture(
            string vanillaDirectoryPath,
            string expectedSavePath = null)
        {
            if (string.IsNullOrWhiteSpace(vanillaDirectoryPath))
                return null;

            try
            {
                AlbumDeletedSaveState state = new AlbumDeletedSaveState
                {
                    VanillaDirectoryPath = Path.GetFullPath(vanillaDirectoryPath),
                    DirectoryExistedBeforeDelete = Directory.Exists(vanillaDirectoryPath)
                };

                if (!string.IsNullOrWhiteSpace(expectedSavePath))
                    state.VanillaSavePaths.Add(expectedSavePath);

                if (Directory.Exists(state.VanillaDirectoryPath))
                {
                    try
                    {
                        foreach (string path in Directory.GetFiles(
                            state.VanillaDirectoryPath,
                            "*.json",
                            SearchOption.AllDirectories))
                        {
                            if (!state.VanillaSavePaths.Contains(path))
                                state.VanillaSavePaths.Add(path);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Keep the already-captured directory/expected path. A partial cleanup is
                        // safer than allowing the observation helper to interfere with vanilla delete.
                        Debug.LogWarning(
                            "[AlbumSave] Could not enumerate every save file before deletion: " +
                            ex.Message);
                    }
                }

                return state;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumSave] Could not capture vanilla deletion path: " + ex.Message);
                return null;
            }
        }

        internal static void CleanupAfterSuccessfulDelete(AlbumDeletedSaveState state)
        {
            if (state == null ||
                !state.DirectoryExistedBeforeDelete ||
                string.IsNullOrEmpty(state.VanillaDirectoryPath) ||
                Directory.Exists(state.VanillaDirectoryPath))
            {
                return;
            }

            try
            {
                AlbumPersistence.DeleteSupplementalStateForVanillaDeletion(
                    state.VanillaDirectoryPath,
                    state.VanillaSavePaths);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumSave] Could not remove supplemental state for a deleted vanilla save: " +
                    ex.Message);
            }
        }
    }

    [HarmonyPatch(typeof(Popup_Save), "Delete")]
    internal static class AlbumPersistence_PopupSaveDelete_Patch
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(
            Popup_Save __instance,
            out AlbumDeletedSaveState __state)
        {
            __state = null;
            try
            {
                if (__instance == null || __instance.SaveFile_ID == 0)
                    return;

                string directory = Path.Combine(
                    Application.persistentDataPath,
                    "data",
                    "manual_saves",
                    __instance.SaveFile_ID.ToString());
                string savePath = Path.Combine(directory, "save.json");
                __state = AlbumDeletedSaveBinding.Capture(directory, savePath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumSave] Could not prepare manual-save cleanup: " + ex.Message);
            }
        }

        [HarmonyFinalizer]
        [HarmonyPriority(Priority.Last)]
        private static Exception Finalizer(
            Exception __exception,
            AlbumDeletedSaveState __state)
        {
            AlbumDeletedSaveBinding.CleanupAfterSuccessfulDelete(__state);
            return __exception;
        }
    }

    [HarmonyPatch]
    internal static class AlbumPersistence_StorySaveDelete_Patch
    {
        private static MethodBase TargetMethod()
        {
            return RequirePrivateMethod(
                typeof(Popup_Load_Story),
                "Delete_Save",
                new Type[] { typeof(Popup_Load_Story.save_info) });
        }

        [HarmonyPriority(Priority.First)]
        private static void Prefix(
            Popup_Load_Story.save_info Save,
            out AlbumDeletedSaveState __state)
        {
            __state = null;
            try
            {
                if (Save == null)
                    return;

                __state = AlbumDeletedSaveBinding.Capture(
                    Save.GetDirectory(),
                    Save.Path_File);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumSave] Could not prepare story-save cleanup: " + ex.Message);
            }
        }

        [HarmonyFinalizer]
        [HarmonyPriority(Priority.Last)]
        private static Exception Finalizer(
            Exception __exception,
            AlbumDeletedSaveState __state)
        {
            AlbumDeletedSaveBinding.CleanupAfterSuccessfulDelete(__state);
            return __exception;
        }

        private static MethodBase RequirePrivateMethod(
            Type type,
            string name,
            Type[] parameterTypes)
        {
            MethodInfo method = AccessTools.Method(type, name, parameterTypes);
            if (method == null)
                throw new MissingMethodException(type.FullName, name);
            return method;
        }
    }

    [HarmonyPatch]
    internal static class AlbumPersistence_PlaythroughDelete_Patch
    {
        private static MethodBase TargetMethod()
        {
            MethodInfo method = AccessTools.Method(
                typeof(Popup_Load_Story),
                "Delete_Playthrough",
                new Type[] { typeof(Popup_Load_Story.playthrough_info) });
            if (method == null)
                throw new MissingMethodException(
                    typeof(Popup_Load_Story).FullName,
                    "Delete_Playthrough");
            return method;
        }

        [HarmonyPriority(Priority.First)]
        private static void Prefix(
            Popup_Load_Story.playthrough_info Playthrough,
            out AlbumDeletedSaveState __state)
        {
            __state = null;
            try
            {
                if (Playthrough != null)
                    __state = AlbumDeletedSaveBinding.Capture(Playthrough.Dir);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumSave] Could not prepare playthrough cleanup: " + ex.Message);
            }
        }

        [HarmonyFinalizer]
        [HarmonyPriority(Priority.Last)]
        private static Exception Finalizer(
            Exception __exception,
            AlbumDeletedSaveState __state)
        {
            AlbumDeletedSaveBinding.CleanupAfterSuccessfulDelete(__state);
            return __exception;
        }
    }
}
