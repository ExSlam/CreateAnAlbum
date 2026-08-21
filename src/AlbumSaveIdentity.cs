using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace Albummodelite
{
    /// <summary>
    /// Captures the concrete vanilla save/load target at the caller level. This intentionally
    /// does not patch generic DataSaver<T>, so it composes with IM Data Core and Save Write
    /// Ordering Fix on Idol Manager's Mono runtime.
    /// </summary>
    internal static class AlbumSaveIdentity
    {
        private static string activeTarget = string.Empty;
        private static string pendingSaveTarget = string.Empty;

        internal static void Reset()
        {
            activeTarget = string.Empty;
            pendingSaveTarget = string.Empty;
        }

        internal static void CaptureLoadTarget(string path)
        {
            string normalized = Normalize(path);
            if (!string.IsNullOrEmpty(normalized))
                activeTarget = normalized;
        }

        internal static void CaptureSaveTarget(string path)
        {
            string normalized = Normalize(path);
            if (!string.IsNullOrEmpty(normalized))
            {
                pendingSaveTarget = normalized;
                activeTarget = normalized;
            }
        }

        internal static string GetFallbackIdentity()
        {
            string target = !string.IsNullOrEmpty(pendingSaveTarget) ? pendingSaveTarget : activeTarget;
            if (!string.IsNullOrEmpty(target))
                return "path_" + StableHash(target);
            return string.Empty;
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

        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;
            try
            {
                string p = path.Trim().Replace('\\', '/');
                if (!Path.IsPathRooted(p))
                    p = Path.Combine(Application.persistentDataPath, "data", p).Replace('\\', '/');
                if (!p.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    p += ".json";
                return Path.GetFullPath(p).Replace('\\', '/').ToLowerInvariant();
            }
            catch
            {
                return path.Trim().Replace('\\', '/').ToLowerInvariant();
            }
        }

        internal static string StableHash(string value)
        {
            unchecked
            {
                // 64-bit FNV-1a keeps physical save-path identities compact while making
                // accidental slot collisions materially less likely than the old 32-bit key.
                ulong hash = 14695981039346656037UL;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 1099511628211UL;
                }
                return hash.ToString("x16");
            }
        }

        internal static string InvokeSaveFileName(object instance, Type argumentType, object argument)
        {
            try
            {
                MethodInfo method = instance.GetType().GetMethod(
                    "GetSaveFileName",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { argumentType },
                    null);
                return method != null ? method.Invoke(instance, new[] { argument }) as string : string.Empty;
            }
            catch { return string.Empty; }
        }
    }

    [HarmonyPatch(typeof(SaveManager), "LoadData", new Type[] { typeof(string) })]
    internal static class AlbumSaveIdentity_LoadPath_Patch
    {
        [HarmonyPrefix]
        private static void Prefix(string path) { AlbumSaveIdentity.CaptureLoadTarget(path); }
    }

    [HarmonyPatch(typeof(SaveManager), "LoadData", new Type[] { typeof(bool) })]
    internal static class AlbumSaveIdentity_LoadBool_Patch
    {
        [HarmonyPrefix]
        private static void Prefix(SaveManager __instance, bool autoSave)
        {
            string path = autoSave
                ? SaveManager.GetLatestAutosavePath()
                : AlbumSaveIdentity.InvokeSaveFileName(__instance, typeof(bool), false);
            AlbumSaveIdentity.CaptureLoadTarget(path);
        }
    }

    [HarmonyPatch(typeof(SaveManager), "SaveData", new Type[] { typeof(bool), typeof(bool) })]
    internal static class AlbumSaveIdentity_SaveData_Patch
    {
        [HarmonyPrefix]
        private static void Prefix(SaveManager __instance, bool autoSave)
        {
            // Vanilla returns before SaveEvent when an autosave is attempted while loading.
            // Do not let a save that never happens change our active checkpoint identity.
            if (autoSave && !SaveManager.CanContinue)
                return;

            AlbumSaveIdentity.CaptureSaveTarget(
                AlbumSaveIdentity.InvokeSaveFileName(__instance, typeof(bool), autoSave));
        }
    }

    [HarmonyPatch(typeof(SaveManager), "SaveChapter", new Type[] { typeof(tasks._chapter) })]
    internal static class AlbumSaveIdentity_SaveChapter_Patch
    {
        [HarmonyPrefix]
        private static void Prefix(SaveManager __instance, tasks._chapter Chapter)
        {
            AlbumSaveIdentity.CaptureSaveTarget(
                AlbumSaveIdentity.InvokeSaveFileName(__instance, typeof(tasks._chapter), Chapter));
        }
    }

    [HarmonyPatch(typeof(Popup_Save), "Save")]
    internal static class AlbumSaveIdentity_PopupSave_Patch
    {
        [HarmonyPrefix]
        private static void Prefix(Popup_Save __instance)
        {
            try
            {
                MethodInfo getId = typeof(Popup_Save).GetMethod("GetSaveFileID", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo getNewId = typeof(Popup_Save).GetMethod("GetNewSaveFileID", BindingFlags.Instance | BindingFlags.NonPublic);
                int id = getId != null ? Convert.ToInt32(getId.Invoke(__instance, null)) : 0;
                if (id == 0 && getNewId != null)
                    id = Convert.ToInt32(getNewId.Invoke(__instance, null));
                string path = AlbumSaveIdentity.InvokeSaveFileName(__instance, typeof(int), id);
                AlbumSaveIdentity.CaptureSaveTarget(path);
            }
            catch { }
        }
    }

    [HarmonyPatch(typeof(Popup_Load_Story), "Do_Overwrite_Save")]
    internal static class AlbumSaveIdentity_StoryOverwrite_Patch
    {
        [HarmonyPrefix]
        private static void Prefix(Popup_Load_Story.save_info Save)
        {
            if (Save != null)
                AlbumSaveIdentity.CaptureSaveTarget(Save.Path_File);
        }
    }

    [HarmonyPatch(typeof(Popup_Load_Story), "Do_New_Save")]
    internal static class AlbumSaveIdentity_StoryNewSave_Patch
    {
        [HarmonyPrefix]
        private static void Prefix(string file_name)
        {
            try
            {
                string folder = staticVars.PlayerData != null ? staticVars.PlayerData.GetSaveFolderName() : string.Empty;
                AlbumSaveIdentity.CaptureSaveTarget("story-new://" + folder + "/" + file_name);
            }
            catch { }
        }
    }

    /// <summary>
    /// Do_New_Save computes its random manual-save directory after SaveEvent. Capture that
    /// final path at the concrete SavedData callsite. The transpiler runs after IM Data Core
    /// has prepared its checkpoint and before Save Write Ordering Fix replaces the vanilla
    /// writer, while leaving the writer instruction itself untouched.
    /// </summary>
    [HarmonyPatch(typeof(Popup_Load_Story), nameof(Popup_Load_Story.Do_New_Save), new Type[] { typeof(string) })]
    internal static class AlbumSaveIdentity_StoryNewSaveConcreteWrite_Patch
    {
        [HarmonyTranspiler]
        [HarmonyPriority(Priority.Normal)]
        [HarmonyAfter("com.cosmo.imdatacore")]
        [HarmonyBefore("com.cosmo.savewriteorderingfix")]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions,
            ILGenerator generator)
        {
            List<CodeInstruction> result = new List<CodeInstruction>(instructions);
            int writeIndex = -1;
            int writeCount = 0;

            for (int i = 0; i < result.Count; i++)
            {
                if (!IsSavedDataWrite(result[i]))
                    continue;
                writeIndex = i;
                writeCount++;
            }

            if (writeCount != 1 || writeIndex < 0)
            {
                Debug.LogWarning(
                    "[AlbumSave] Expected one SavedData write in Do_New_Save; found " +
                    writeCount + ". Concrete-path fallback capture was left disabled.");
                return result;
            }

            LocalBuilder dataLocal = generator.DeclareLocal(typeof(SaveManager.SavedData));
            LocalBuilder pathLocal = generator.DeclareLocal(typeof(string));
            LocalBuilder jsonLocal = generator.DeclareLocal(typeof(bool));
            LocalBuilder fullPathLocal = generator.DeclareLocal(typeof(bool));
            MethodInfo captureMethod = AccessTools.Method(
                typeof(AlbumPersistence),
                nameof(AlbumPersistence.CaptureConcreteSaveWriteTarget),
                new Type[] { typeof(string) });
            if (captureMethod == null)
                return result;

            CodeInstruction original = result[writeIndex];
            CodeInstruction first = new CodeInstruction(OpCodes.Stloc, fullPathLocal);
            first.labels.AddRange(original.labels);
            first.blocks.AddRange(original.blocks);
            original.labels.Clear();
            original.blocks.Clear();

            List<CodeInstruction> injected = new List<CodeInstruction>
            {
                first,
                new CodeInstruction(OpCodes.Stloc, jsonLocal),
                new CodeInstruction(OpCodes.Stloc, pathLocal),
                new CodeInstruction(OpCodes.Stloc, dataLocal),
                new CodeInstruction(OpCodes.Ldloc, pathLocal),
                new CodeInstruction(OpCodes.Call, captureMethod),
                new CodeInstruction(OpCodes.Ldloc, dataLocal),
                new CodeInstruction(OpCodes.Ldloc, pathLocal),
                new CodeInstruction(OpCodes.Ldloc, jsonLocal),
                new CodeInstruction(OpCodes.Ldloc, fullPathLocal),
                original
            };

            result.RemoveAt(writeIndex);
            result.InsertRange(writeIndex, injected);
            return result;
        }

        private static bool IsSavedDataWrite(CodeInstruction instruction)
        {
            if (instruction == null ||
                (instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt))
                return false;

            MethodInfo method = instruction.operand as MethodInfo;
            if (method == null || method.Name != "saveData" || !method.IsGenericMethod)
                return false;

            Type declaringType = method.DeclaringType;
            if (declaringType != typeof(DataSaver))
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
    }

}
