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
    /// Tracks the concrete vanilla save that was loaded. Save targets are deliberately kept
    /// separate from the loaded identity: autosaves and Save As create checkpoints, but must
    /// never make the live session pretend that it loaded from the just-written destination.
    /// </summary>
    internal static class AlbumSaveIdentity
    {
        private static string activeLoadTarget = string.Empty;
        private static string pendingSaveTarget = string.Empty;

        internal static void Reset()
        {
            activeLoadTarget = string.Empty;
            pendingSaveTarget = string.Empty;
        }

        internal static void CaptureLoadTarget(string path)
        {
            string normalized = Normalize(path);
            if (string.IsNullOrEmpty(normalized))
                return;

            activeLoadTarget = normalized;
            pendingSaveTarget = string.Empty;
        }

        internal static void CaptureSaveTarget(string path)
        {
            string normalized = Normalize(path);
            if (!string.IsNullOrEmpty(normalized))
                pendingSaveTarget = normalized;
        }

        internal static string GetActiveFallbackIdentity()
        {
            return string.IsNullOrEmpty(activeLoadTarget)
                ? string.Empty
                : "path_" + StableHash(activeLoadTarget);
        }

        internal static string GetPendingFallbackIdentity()
        {
            return string.IsNullOrEmpty(pendingSaveTarget)
                ? string.Empty
                : "path_" + StableHash(pendingSaveTarget);
        }

        internal static string GetIdentityForPath(string path)
        {
            string normalized = Normalize(path);
            return string.IsNullOrEmpty(normalized)
                ? string.Empty
                : "path_" + StableHash(normalized);
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
            catch
            {
                return string.Empty;
            }
        }
    }

    [HarmonyPatch(typeof(SaveManager), "LoadData", new Type[] { typeof(string) })]
    internal static class AlbumSaveIdentity_LoadPath_Patch
    {
        [HarmonyPrefix]
        private static void Prefix(string path)
        {
            AlbumSaveIdentity.CaptureLoadTarget(path);
        }
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
                new Type[] { typeof(string) });
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

                yield return new CodeInstruction(OpCodes.Ldloc, pathLocal);
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
}
