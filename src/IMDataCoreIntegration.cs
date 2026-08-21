using HarmonyLib;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Albummodelite
{
    /// <summary>
    /// Optional, reflection-only bridge to Cosmo's IM Data Core. Create An Album keeps no
    /// compile-time dependency on IMDC, but when its Harmony owner is active we store the v3
    /// state document in IMDC's branch/checkpoint-aware custom JSON namespace.
    /// </summary>
    internal static class IMDataCoreIntegration
    {
        private const string AssemblyName = "com.cosmo.imdatacore";
        private const string HarmonyId = "com.cosmo.imdatacore";
        private const string NamespaceId = "com.jordanss.createanalbum";
        private const string StateKey = "state_v3";

        private static bool probed;
        private static bool ready;
        private static bool retryWhenCoreReady;
        private static object session;
        private static MethodInfo isReadyMethod;
        private static MethodInfo registerMethod;
        private static MethodInfo getMethod;
        private static MethodInfo setMethod;
        private static MethodInfo getActiveSaveKeyMethod;
        private static Assembly consumerAssembly;

        internal static bool IsReady
        {
            get
            {
                EnsureProbed();
                return ready;
            }
        }

        internal static bool IsBootstrapping
        {
            get
            {
                EnsureProbed();
                return !ready && retryWhenCoreReady;
            }
        }

        internal static void BeginGameplaySession()
        {
            ResetProbe();
            EnsureProbed();
        }

        internal static void EndGameplaySession()
        {
            session = null;
            ResetProbe();
        }

        internal static void StopRetryingForSession(string reason)
        {
            ready = false;
            retryWhenCoreReady = false;
            session = null;
            probed = true;
            Debug.LogWarning(
                "[AlbumSave/IMDC] Falling back to standalone persistence for this gameplay session: " +
                reason);
        }

        internal static bool TryGetState(out string json)
        {
            json = string.Empty;
            if (!EnsureSession())
                return false;

            try
            {
                object[] args = { session, consumerAssembly, StateKey, string.Empty, string.Empty };
                bool result = Convert.ToBoolean(getMethod.Invoke(null, args));
                json = args[3] as string ?? string.Empty;
                if (!result && !string.IsNullOrEmpty(args[4] as string))
                    Debug.LogWarning("[AlbumSave/IMDC] Read failed: " + args[4]);
                return result && !string.IsNullOrEmpty(json);
            }
            catch (Exception ex)
            {
                Disable("read", ex);
                return false;
            }
        }

        internal static bool TrySetState(string json)
        {
            if (string.IsNullOrEmpty(json) || !EnsureSession())
                return false;

            try
            {
                object[] args = { session, consumerAssembly, StateKey, json, string.Empty };
                bool result = Convert.ToBoolean(setMethod.Invoke(null, args));
                if (!result)
                    Debug.LogWarning("[AlbumSave/IMDC] Write failed: " + (args[4] as string ?? "unknown error"));
                return result;
            }
            catch (Exception ex)
            {
                Disable("write", ex);
                return false;
            }
        }

        internal static bool TryGetActiveSaveKey(out string saveKey)
        {
            saveKey = string.Empty;
            EnsureProbed();
            if (!ready || getActiveSaveKeyMethod == null)
                return false;

            try
            {
                object[] args = { string.Empty, string.Empty };
                bool result = Convert.ToBoolean(getActiveSaveKeyMethod.Invoke(null, args));
                saveKey = args[0] as string ?? string.Empty;
                return result && !string.IsNullOrEmpty(saveKey);
            }
            catch
            {
                return false;
            }
        }

        private static bool EnsureSession()
        {
            EnsureProbed();
            if (!ready)
                return false;
            if (session != null)
                return true;

            try
            {
                object[] args = { NamespaceId, consumerAssembly, null, string.Empty };
                bool result = Convert.ToBoolean(registerMethod.Invoke(null, args));
                session = args[2];
                if (!result || session == null)
                {
                    Debug.LogWarning("[AlbumSave/IMDC] Namespace registration failed: " + (args[3] as string ?? "unknown error"));
                    ready = false;
                    return false;
                }
                Debug.Log("[AlbumSave/IMDC] Using IM Data Core persistence backend.");
                return true;
            }
            catch (Exception ex)
            {
                Disable("registration", ex);
                return false;
            }
        }

        private static void EnsureProbed()
        {
            if (probed && (ready || !retryWhenCoreReady))
                return;
            probed = true;
            ready = false;
            retryWhenCoreReady = false;
            consumerAssembly = typeof(IMDataCoreIntegration).Assembly;

            try
            {
                if (!Harmony.HasAnyPatches(HarmonyId))
                    return;

                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, AssemblyName, StringComparison.OrdinalIgnoreCase));
                if (assembly == null)
                    return;

                Type apiType = assembly.GetType("IMDataCore.IMDataCoreApi", false);
                Type interopType = assembly.GetType("IMDataCore.IMDataCoreInteropApi", false);
                if (apiType == null || interopType == null)
                    return;

                isReadyMethod = FindStatic(apiType, "IsReady", 0);
                getActiveSaveKeyMethod = FindStatic(apiType, "TryGetActiveSaveKey", 2);
                registerMethod = FindStatic(interopType, "TryRegisterNamespace", 4);
                getMethod = FindStatic(interopType, "TryGetCustomJson", 5);
                setMethod = FindStatic(interopType, "TrySetCustomJson", 5);

                if (isReadyMethod == null || registerMethod == null || getMethod == null || setMethod == null)
                    return;

                ready = Convert.ToBoolean(isReadyMethod.Invoke(null, null));
                if (!ready)
                {
                    // IM Data Core bootstraps from PopupManager.Start, which can run after
                    // Create An Album's mainScript.Start host. Keep the bridge probe retryable
                    // until IMDC reports that its storage controller is actually ready.
                    retryWhenCoreReady = true;
                }
            }
            catch (Exception ex)
            {
                Disable("probe", ex);
            }
        }

        private static MethodInfo FindStatic(Type type, string name, int parameterCount)
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == name && m.GetParameters().Length == parameterCount);
        }

        private static void Disable(string operation, Exception ex)
        {
            ready = false;
            session = null;
            Debug.LogWarning("[AlbumSave/IMDC] Disabled after " + operation + " failure: " + ex.Message);
        }

        private static void ResetProbe()
        {
            probed = false;
            ready = false;
            retryWhenCoreReady = false;
            session = null;
            isReadyMethod = null;
            registerMethod = null;
            getMethod = null;
            setMethod = null;
            getActiveSaveKeyMethod = null;
        }
    }
}
