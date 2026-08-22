using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Debug = UnityEngine.Debug;

namespace Albummodelite.EmbeddedSaveOrdering
{
    /// <summary>
    /// Final caller-level writer replacement. CAA's persistence transpiler runs first,
    /// IM Data Core gets its normal checkpoint preparation next, and this replacement
    /// freezes the exact final SavedData argument before any background write begins.
    /// </summary>
    [HarmonyPatch]
    internal static class EmbeddedSavedDataWritePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return RequireMethod(typeof(SaveManager), nameof(SaveManager.SaveData), new Type[] { typeof(bool), typeof(bool) });
            yield return RequireMethod(typeof(SaveManager), nameof(SaveManager.SaveChapter), new Type[] { typeof(tasks._chapter) });
            yield return RequireMethod(typeof(Popup_Save), "Save", Type.EmptyTypes);
            yield return RequireMethod(typeof(Popup_Load_Story), "Do_Overwrite_Save", new Type[] { typeof(Popup_Load_Story.save_info) });
            yield return RequireMethod(typeof(Popup_Load_Story), nameof(Popup_Load_Story.Do_New_Save), new Type[] { typeof(string) });
        }

        [HarmonyTranspiler]
        [HarmonyPriority(Priority.Last)]
        [HarmonyAfter(
            EmbeddedSaveOrderingConstants.ImDataCoreHarmonyId,
            EmbeddedSaveOrderingConstants.GraduationDetailsHarmonyId)]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions,
            MethodBase __originalMethod)
        {
            // If Cosmo's standalone SWO is installed, its own Priority.Last transpiler owns
            // the final writer. Returning the original IL prevents double queueing.
            if (Harmony.HasAnyPatches(EmbeddedSaveOrderingConstants.StandaloneHarmonyId))
                return instructions;

            MethodInfo replacement = AccessTools.Method(
                typeof(EmbeddedSaveOrderingCoordinator),
                nameof(EmbeddedSaveOrderingCoordinator.QueueVanillaSavedDataWrite),
                new Type[]
                {
                    typeof(SaveManager.SavedData),
                    typeof(string),
                    typeof(bool),
                    typeof(bool)
                });
            if (replacement == null)
                return instructions;

            List<CodeInstruction> result = new List<CodeInstruction>(instructions);
            int replacements = 0;
            foreach (CodeInstruction instruction in result)
            {
                if (!IsSavedDataWrite(instruction))
                    continue;
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                replacements++;
            }

            if (replacements != 1)
            {
                Debug.LogWarning(
                    EmbeddedSaveOrderingConstants.LogPrefix +
                    "Expected one SavedData writer in " + Describe(__originalMethod) +
                    ", found " + replacements + ".");
            }
            return result;
        }

        private static bool IsSavedDataWrite(CodeInstruction instruction)
        {
            MethodInfo method = instruction == null ? null : instruction.operand as MethodInfo;
            if (method == null || method.DeclaringType != typeof(DataSaver) ||
                !string.Equals(method.Name, "saveData", StringComparison.Ordinal) ||
                !method.IsGenericMethod)
                return false;

            Type[] generic = method.GetGenericArguments();
            ParameterInfo[] parameters = method.GetParameters();
            return generic.Length == 1 && generic[0] == typeof(SaveManager.SavedData) &&
                   parameters.Length == 4 &&
                   parameters[0].ParameterType == typeof(SaveManager.SavedData) &&
                   parameters[1].ParameterType == typeof(string) &&
                   parameters[2].ParameterType == typeof(bool) &&
                   parameters[3].ParameterType == typeof(bool);
        }

        private static MethodBase RequireMethod(Type type, string name, Type[] args)
        {
            MethodInfo method = AccessTools.Method(type, name, args);
            if (method == null)
                throw new MissingMethodException(type.FullName, name);
            return method;
        }

        private static string Describe(MethodBase method)
        {
            return method == null
                ? "an unknown save caller"
                : method.DeclaringType.FullName + "." + method.Name;
        }
    }

    /// <summary>
    /// Prevents a load/save-list read from racing a queued write to the same path.
    /// Only concrete SavedData call sites are replaced; generic DataSaver<T> itself is untouched.
    /// </summary>
    [HarmonyPatch]
    internal static class EmbeddedSavedDataReadPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return RequireMethod(typeof(SaveManager), nameof(SaveManager.GetLatestAutosavePath), Type.EmptyTypes);
            yield return RequireMethod(typeof(SaveManager), nameof(SaveManager.LoadData), new Type[] { typeof(string) });
            yield return RequireMethod(typeof(SaveManager), nameof(SaveManager.LoadData), new Type[] { typeof(bool) });
            yield return RequireMethod(typeof(Popup_Load_Story), "Get_Playthrough_Info", new Type[] { typeof(string) });
            yield return RequireMethod(typeof(Popup_Load_Story), "Get_Saves", new Type[] { typeof(Popup_Load_Story.playthrough_info) });
            yield return RequireMethod(typeof(Popup_Save._save_data), nameof(Popup_Save._save_data.Set), new Type[] { typeof(string) });
            yield return RequireMethod(typeof(Popup_Save._save_data), nameof(Popup_Save._save_data.SetAutosave), Type.EmptyTypes);
        }

        [HarmonyTranspiler]
        [HarmonyPriority(Priority.Last)]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions,
            MethodBase __originalMethod)
        {
            if (Harmony.HasAnyPatches(EmbeddedSaveOrderingConstants.StandaloneHarmonyId))
                return instructions;

            MethodInfo replacement = AccessTools.Method(
                typeof(EmbeddedSaveOrderingCoordinator),
                nameof(EmbeddedSaveOrderingCoordinator.LoadSavedDataAfterPendingWrites),
                new Type[] { typeof(string) });
            if (replacement == null)
                return instructions;

            List<CodeInstruction> result = new List<CodeInstruction>(instructions);
            int replacements = 0;
            foreach (CodeInstruction instruction in result)
            {
                if (!IsSavedDataRead(instruction))
                    continue;
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                replacements++;
            }

            if (replacements == 0)
            {
                Debug.LogWarning(
                    EmbeddedSaveOrderingConstants.LogPrefix +
                    "No SavedData reader was found in " + Describe(__originalMethod) + ".");
            }
            return result;
        }

        private static bool IsSavedDataRead(CodeInstruction instruction)
        {
            MethodInfo method = instruction == null ? null : instruction.operand as MethodInfo;
            if (method == null || method.DeclaringType != typeof(DataSaver) ||
                !string.Equals(method.Name, "loadData", StringComparison.Ordinal) ||
                !method.IsGenericMethod)
                return false;

            Type[] generic = method.GetGenericArguments();
            ParameterInfo[] parameters = method.GetParameters();
            return generic.Length == 1 && generic[0] == typeof(SaveManager.SavedData) &&
                   parameters.Length == 1 && parameters[0].ParameterType == typeof(string) &&
                   method.ReturnType == typeof(SaveManager.SavedData);
        }

        private static MethodBase RequireMethod(Type type, string name, Type[] args)
        {
            MethodInfo method = AccessTools.Method(type, name, args);
            if (method == null)
                throw new MissingMethodException(type.FullName, name);
            return method;
        }

        private static string Describe(MethodBase method)
        {
            return method == null
                ? "an unknown read caller"
                : method.DeclaringType.FullName + "." + method.Name;
        }
    }
}
