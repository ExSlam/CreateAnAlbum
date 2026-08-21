using HarmonyLib;
using UnityEngine;
using CreateAnAlbumGroupRules;

namespace Albummodelite
{
    /// <summary>
    /// Runtime component attached by HarmonyIntegration after Idol Manager's gameplay mainScript starts.
    /// This is deliberately not a BepInEx plugin. IM-HarmonyIntegration discovers and applies the
    /// Harmony patches in this assembly; the patch below creates the small runtime host only in game.
    /// </summary>
    public sealed class CreateAnAlbum : MonoBehaviour
    {
        private AlbumPopup createPopup;
        private AlbumLibraryPopup libraryPopup;
        private AlbumChartPopup chartPopup;

        private void Awake()
        {
            AlbumPersistence.Initialize();
            RivalsRebornIntegration.BeginGameplaySession();

            if (GetComponent<AlbumSalesManager>() == null)
                gameObject.AddComponent<AlbumSalesManager>();
            if (GetComponent<AlbumChartUpdatePopup>() == null)
                gameObject.AddComponent<AlbumChartUpdatePopup>();

            Debug.Log("[CreateAlbum] Harmony runtime initialized.");
        }

        private void OnDestroy()
        {
            AlbumPopupHost.Reset();
            AlbumProductionManager.Shutdown();
            AlbumPersistence.Shutdown();
            // RR availability is deliberately latched until the main menu is reached.
            // A gameplay object being recreated (for example while loading another save) must not
            // re-probe a mod that was disabled at the beginning of this gameplay session.
        }

        private void Update()
        {
            AlbumPersistence.Tick();
            AlbumProductionManager.Tick();

            if (Input.GetKeyDown(KeyCode.F2))
                OpenCreateAlbum();

            if (Input.GetKeyDown(KeyCode.F3))
                OpenAlbumLibrary();

            if (Input.GetKeyDown(KeyCode.F8))
                OpenAlbumChart();
        }

        public bool OpenCreateAlbum(bool queueBehindCurrentPopup = false)
        {
            if (!AlbumPersistence.EnsureCurrentSaveLoaded())
            {
                Debug.LogWarning("[CreateAlbum] Album data is not ready for this save.");
                return false;
            }

            if (AlbumProductionManager.TryOpenExisting(queueBehindCurrentPopup))
                return true;

            if (createPopup == null)
                createPopup = GetComponent<AlbumPopup>() ?? gameObject.AddComponent<AlbumPopup>();

            return createPopup.Open(queueBehindCurrentPopup);
        }

        public bool OpenAlbumLibrary(bool queueBehindCurrentPopup = false)
        {
            if (!AlbumPersistence.EnsureCurrentSaveLoaded())
            {
                Debug.LogWarning("[CreateAlbum] Album data is not ready for this save.");
                return false;
            }
            Albums.DeduplicateInPlace();

            if (libraryPopup == null)
                libraryPopup = GetComponent<AlbumLibraryPopup>() ?? gameObject.AddComponent<AlbumLibraryPopup>();

            return libraryPopup.Open(queueBehindCurrentPopup);
        }

        public bool OpenAlbumChart(bool queueBehindCurrentPopup = false)
        {
            if (!AlbumPersistence.EnsureCurrentSaveLoaded())
            {
                Debug.LogWarning("[CreateAlbum] Album data is not ready for this save.");
                return false;
            }
            Albums.DeduplicateInPlace();

            if (chartPopup == null)
                chartPopup = GetComponent<AlbumChartPopup>() ?? gameObject.AddComponent<AlbumChartPopup>();

            return chartPopup.Open(queueBehindCurrentPopup);
        }
    }

    [HarmonyPatch(typeof(mainScript), "Start")]
    internal static class AlbumMainScriptStartPatch
    {
        private static void Postfix(mainScript __instance)
        {
            if (__instance == null)
                return;

            if (!__instance.IsGameScene || mainScript.IsMainMenu())
            {
                AlbumPopupHost.Reset();
                AlbumProductionManager.Shutdown();
                AlbumPersistence.Shutdown();
                AlbumFontCatalog.Shutdown();
                AlbumBackgroundCatalog.Shutdown();
                IMDataCoreIntegration.EndGameplaySession();
                RivalsRebornIntegration.EndGameplaySession();
                return;
            }

            if (__instance.GetComponent<CreateAnAlbum>() == null)
                __instance.gameObject.AddComponent<CreateAnAlbum>();
        }
    }
}
