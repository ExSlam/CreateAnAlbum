using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Albummodelite
{
    internal enum RivalsRebornSessionState
    {
        NotChecked,
        Disabled,
        Ready,
        Failed
    }

    internal sealed class RivalIdolView
    {
        internal object Source;
        internal int Uid;
        internal string Name;
        internal int Fame;
        internal int StatTotal;
        internal bool IsCenter;
        internal int Status;
    }

    internal sealed class RivalLabelView
    {
        internal object Source;
        internal int GroupId;
        internal string Name;
        internal int Wealth;
        internal float Momentum;
        internal bool Disbanded;
        internal List<RivalIdolView> Roster = new List<RivalIdolView>();
    }

    /// <summary>
    /// Optional Rivals Reborn bridge. The RR assembly is never referenced by the Album assembly.
    /// Harmony ownership is inspected once at gameplay-session start; Disabled/Failed are latched
    /// until the game returns to the main menu and a new gameplay mainScript starts.
    /// </summary>
    internal static class RivalsRebornIntegration
    {
        private const string AssemblyName = "rivalsreborn";
        private const string HarmonyOwnerId = "rivalsreborn";
        private const string RootTypeName = "RivalsReborn.RR";
        private const string PortraitsTypeName = "RivalsReborn.Portraits";
        private const int ActiveStatus = 0;

        private static RivalsRebornSessionState state = RivalsRebornSessionState.NotChecked;
        private static Assembly assembly;
        private static Type rrType;
        private static Type stateType;
        private static Type labelType;
        private static Type idolType;
        private static Type portraitsType;
        private static FieldInfo rrStateField;
        private static FieldInfo stateLabelsField;
        private static MethodInfo ensureInitMethod;
        private static MethodInfo getVanillaGroupMethod;
        private static MethodInfo getDisplayGirlMethod;
        private static MethodInfo getDisplayGirlByIdMethod;
        private static MethodInfo queueNewsMethod;

        internal static RivalsRebornSessionState State { get { return state; } }
        internal static bool IsReady { get { return state == RivalsRebornSessionState.Ready; } }

        internal static void BeginGameplaySession()
        {
            if (state != RivalsRebornSessionState.NotChecked)
                return;

            try
            {
                assembly = FindLoadedAssembly(AssemblyName);
                if (assembly == null || !HasHarmonyOwner(HarmonyOwnerId))
                {
                    state = RivalsRebornSessionState.Disabled;
                    ClearBindings(false);
                    Debug.Log("[CreateAlbum/RR] Rivals Reborn is not active for this gameplay session.");
                    return;
                }

                if (!BindApi(assembly))
                {
                    state = RivalsRebornSessionState.Failed;
                    Debug.LogWarning("[CreateAlbum/RR] Rivals Reborn is active, but the compatible reflection surface was not found.");
                    return;
                }

                // RR owns its own lifecycle, but this mirrors the safe Cheats-style bridge behavior:
                // initialize once after verifying that the RR Harmony owner is actually active.
                if (ensureInitMethod != null)
                    ensureInitMethod.Invoke(null, null);

                state = RivalsRebornSessionState.Ready;
                Debug.Log("[CreateAlbum/RR] Reflection bridge ready for this gameplay session.");
            }
            catch (Exception ex)
            {
                state = RivalsRebornSessionState.Failed;
                Debug.LogWarning("[CreateAlbum/RR] Reflection bridge failed and is latched off for this session: " + ex.Message);
            }
        }

        internal static void EndGameplaySession()
        {
            state = RivalsRebornSessionState.NotChecked;
            ClearBindings(true);
        }

        internal static List<RivalLabelView> GetLabels()
        {
            List<RivalLabelView> result = new List<RivalLabelView>();
            if (!IsReady || rrStateField == null || stateLabelsField == null)
                return result;

            try
            {
                object rrState = rrStateField.GetValue(null);
                if (rrState == null)
                    return result;

                IEnumerable labels = stateLabelsField.GetValue(rrState) as IEnumerable;
                if (labels == null)
                    return result;

                foreach (object rawLabel in labels)
                {
                    RivalLabelView label = ReadLabel(rawLabel);
                    if (label != null)
                        result.Add(label);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CreateAlbum/RR] Could not read RR labels: " + ex.Message);
            }

            return result;
        }

        internal static RivalLabelView FindLabel(int groupId)
        {
            return GetLabels().FirstOrDefault(x => x != null && x.GroupId == groupId);
        }

        internal static Rivals._group TryGetVanillaGroup(int groupId)
        {
            if (!IsReady || getVanillaGroupMethod == null)
                return null;

            try
            {
                return getVanillaGroupMethod.Invoke(null, new object[] { groupId }) as Rivals._group;
            }
            catch
            {
                return null;
            }
        }

        internal static data_girls.girls TryGetDisplayGirl(RivalIdolView idol)
        {
            if (!IsReady || idol == null || idol.Source == null || getDisplayGirlMethod == null)
                return null;

            try
            {
                return getDisplayGirlMethod.Invoke(null, new object[] { idol.Source }) as data_girls.girls;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CreateAlbum/RR] Rival portrait unavailable for " + idol.Name + ": " + ex.Message);
                return null;
            }
        }

        internal static data_girls.girls TryGetDisplayGirlById(int id)
        {
            if (!IsReady || getDisplayGirlByIdMethod == null)
                return null;

            try
            {
                return getDisplayGirlByIdMethod.Invoke(null, new object[] { id }) as data_girls.girls;
            }
            catch
            {
                return null;
            }
        }

        internal static bool TryQueueNews(string text)
        {
            if (!IsReady || queueNewsMethod == null || string.IsNullOrWhiteSpace(text))
                return false;

            try
            {
                queueNewsMethod.Invoke(null, new object[] { text });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CreateAlbum/RR] Could not queue RR release news: " + ex.Message);
                return false;
            }
        }

        private static bool BindApi(Assembly rrAssembly)
        {
            rrType = rrAssembly.GetType(RootTypeName, false);
            portraitsType = rrAssembly.GetType(PortraitsTypeName, false);
            if (rrType == null)
                return false;

            BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            rrStateField = rrType.GetField("State", staticFlags);
            ensureInitMethod = rrType.GetMethod("EnsureInit", staticFlags, null, Type.EmptyTypes, null);
            getVanillaGroupMethod = rrType.GetMethod("GetVanillaGroup", staticFlags, null, new Type[] { typeof(int) }, null);
            queueNewsMethod = rrType.GetMethod("QueueNews", staticFlags, null, new Type[] { typeof(string) }, null);

            if (rrStateField == null)
                return false;

            stateType = rrStateField.FieldType;
            stateLabelsField = stateType.GetField("Labels", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (stateLabelsField == null)
                return false;

            Type enumerableType = stateLabelsField.FieldType;
            if (enumerableType.IsGenericType)
                labelType = enumerableType.GetGenericArguments()[0];

            if (labelType == null)
                labelType = rrAssembly.GetType("RivalsReborn.RLabel", false);

            idolType = rrAssembly.GetType("RivalsReborn.RIdol", false);
            if (labelType == null || idolType == null)
                return false;

            if (portraitsType != null)
            {
                getDisplayGirlMethod = portraitsType.GetMethod("GetDisplayGirl", staticFlags, null, new Type[] { idolType }, null);
                getDisplayGirlByIdMethod = portraitsType.GetMethod("GetDisplayGirlById", staticFlags, null, new Type[] { typeof(int) }, null);
            }

            return true;
        }

        private static RivalLabelView ReadLabel(object rawLabel)
        {
            if (rawLabel == null || labelType == null)
                return null;

            RivalLabelView label = new RivalLabelView();
            label.Source = rawLabel;
            label.GroupId = Read<int>(rawLabel, "GroupId", -1);
            label.Name = Read<string>(rawLabel, "Name", "");
            label.Wealth = Read<int>(rawLabel, "Wealth", 0);
            label.Momentum = Read<float>(rawLabel, "Momentum", 1f);
            label.Disbanded = Read<bool>(rawLabel, "Disbanded", false);

            IEnumerable roster = ReadObject(rawLabel, "Roster") as IEnumerable;
            if (roster != null)
            {
                foreach (object rawIdol in roster)
                {
                    RivalIdolView idol = ReadIdol(rawIdol);
                    if (idol != null)
                        label.Roster.Add(idol);
                }
            }

            return label;
        }

        private static RivalIdolView ReadIdol(object rawIdol)
        {
            if (rawIdol == null)
                return null;

            RivalIdolView idol = new RivalIdolView();
            idol.Source = rawIdol;
            idol.Uid = Read<int>(rawIdol, "Uid", 0);
            string first = Read<string>(rawIdol, "FirstName", "");
            string last = Read<string>(rawIdol, "LastName", "");
            idol.Name = (last + " " + first).Trim();
            idol.Fame = Read<int>(rawIdol, "Fame", 0);
            idol.IsCenter = Read<bool>(rawIdol, "IsCenter", false);
            idol.Status = Read<int>(rawIdol, "Status", -1);
            idol.StatTotal =
                Read<int>(rawIdol, "Vocal", 0) +
                Read<int>(rawIdol, "Dance", 0) +
                Read<int>(rawIdol, "Cute", 0) +
                Read<int>(rawIdol, "Cool", 0) +
                Read<int>(rawIdol, "Sexy", 0) +
                Read<int>(rawIdol, "Funny", 0) +
                Read<int>(rawIdol, "Smart", 0);
            return idol;
        }

        internal static bool IsActiveIdol(RivalIdolView idol)
        {
            return idol != null && idol.Status == ActiveStatus;
        }

        private static T Read<T>(object target, string name, T fallback)
        {
            object value = ReadObject(target, name);
            if (value == null)
                return fallback;

            try
            {
                if (value is T)
                    return (T)value;
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return fallback;
            }
        }

        private static object ReadObject(object target, string name)
        {
            if (target == null)
                return null;

            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            Type type = target.GetType();
            FieldInfo field = type.GetField(name, flags);
            if (field != null)
                return field.GetValue(target);

            PropertyInfo property = type.GetProperty(name, flags);
            return property != null ? property.GetValue(target, null) : null;
        }

        private static Assembly FindLoadedAssembly(string simpleName)
        {
            foreach (Assembly candidate in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (candidate != null && string.Equals(candidate.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase))
                        return candidate;
                }
                catch
                {
                }
            }
            return null;
        }

        private static bool HasHarmonyOwner(string ownerId)
        {
            try
            {
                foreach (MethodBase method in Harmony.GetAllPatchedMethods())
                {
                    Patches patches = Harmony.GetPatchInfo(method);
                    if (patches == null || patches.Owners == null)
                        continue;

                    foreach (string owner in patches.Owners)
                    {
                        if (string.Equals(owner, ownerId, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CreateAlbum/RR] Harmony owner inspection failed: " + ex.Message);
            }

            return false;
        }

        private static void ClearBindings(bool clearAssembly)
        {
            rrType = null;
            stateType = null;
            labelType = null;
            idolType = null;
            portraitsType = null;
            rrStateField = null;
            stateLabelsField = null;
            ensureInitMethod = null;
            getVanillaGroupMethod = null;
            getDisplayGirlMethod = null;
            getDisplayGirlByIdMethod = null;
            queueNewsMethod = null;
            if (clearAssembly)
                assembly = null;
        }
    }
}
