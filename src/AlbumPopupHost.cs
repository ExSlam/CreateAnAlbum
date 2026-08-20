using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Albummodelite
{
    internal enum AlbumPopupKind
    {
        Create,
        Library,
        Detail,
        Chart
    }

    /// <summary>
    /// Registers Album popup roots with Idol Manager's real PopupManager. PopupManager therefore owns
    /// the queue, pause/resume, agency input blocking, blur/darken, and close lifecycle.
    /// </summary>
    internal static class AlbumPopupHost
    {
        private const int CreateId = 0x414C4352;  // ALCR
        private const int LibraryId = 0x414C4C42; // ALLB
        private const int DetailId = 0x414C4454;  // ALDT
        private const int ChartId = 0x414C4348;   // ALCH

        private static readonly Dictionary<AlbumPopupKind, GameObject> roots =
            new Dictionary<AlbumPopupKind, GameObject>();

        internal static PopupManager._type TypeFor(AlbumPopupKind kind)
        {
            switch (kind)
            {
                case AlbumPopupKind.Create: return (PopupManager._type)CreateId;
                case AlbumPopupKind.Library: return (PopupManager._type)LibraryId;
                case AlbumPopupKind.Detail: return (PopupManager._type)DetailId;
                default: return (PopupManager._type)ChartId;
            }
        }

        internal static GameObject Prepare(AlbumPopupKind kind)
        {
            PopupManager manager = GetManager();
            if (manager == null)
                return null;

            GameObject root;
            if (!roots.TryGetValue(kind, out root) || root == null)
            {
                root = CreateRoot(manager, kind);
                if (root == null)
                    return null;
                roots[kind] = root;
            }

            for (int i = root.transform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(root.transform.GetChild(i).gameObject);

            root.SetActive(false);
            return root;
        }

        internal static bool Open(AlbumPopupKind kind)
        {
            PopupManager manager = GetManager();
            if (manager == null)
                return false;

            PopupManager._type type = TypeFor(kind);
            PopupManager._popup entry = manager.GetByType(type);
            if (entry == null || entry.obj == null)
                return false;

            if (entry.open)
            {
                manager.Close();
                return false;
            }

            // Hotkey/button opens should never lurk behind an unrelated popup in the vanilla queue.
            if (manager.IsThereAnOpenPopup())
                return false;

            manager.Open(type, true);
            return true;
        }

        internal static bool Transition(AlbumPopupKind from, AlbumPopupKind to)
        {
            PopupManager manager = GetManager();
            if (manager == null)
                return false;

            PopupManager._popup source = manager.GetByType(TypeFor(from));
            PopupManager._popup target = manager.GetByType(TypeFor(to));
            if (source == null || target == null || !source.open)
                return false;

            manager.Open(TypeFor(to), true);
            manager.Close();
            return true;
        }

        internal static bool IsOpen(AlbumPopupKind kind)
        {
            PopupManager manager = GetManager();
            PopupManager._popup entry = manager != null ? manager.GetByType(TypeFor(kind)) : null;
            return entry != null && entry.open;
        }

        internal static void Close(AlbumPopupKind kind)
        {
            PopupManager manager = GetManager();
            PopupManager._popup entry = manager != null ? manager.GetByType(TypeFor(kind)) : null;
            if (entry != null && entry.open)
                manager.Close();
        }

        internal static void Reset()
        {
            foreach (GameObject root in roots.Values)
            {
                if (root != null)
                    UnityEngine.Object.Destroy(root);
            }
            roots.Clear();
        }

        private static GameObject CreateRoot(PopupManager manager, AlbumPopupKind kind)
        {
            Transform parent = FindPopupParent(manager);
            if (parent == null)
                return null;

            GameObject root = new GameObject("CreateAnAlbum_" + kind + "PopupRoot");
            root.transform.SetParent(parent, false);
            // Popup.OnEnable calls Show immediately. Keep the root inactive while the Popup
            // component and its OnOpen event are initialized.
            root.SetActive(false);

            RectTransform rect = root.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            Popup popup = root.AddComponent<Popup>();
            popup.OnOpen = new UnityEvent();
            popup.ShowAnimation = true;
            popup.HideAnimation = true;
            popup.Increase_Popup_Counter = true;

            PopupManager._popup entry = new PopupManager._popup();
            entry.type = TypeFor(kind);
            entry.obj = root;
            entry.open = false;
            entry.BGBlur = true;
            entry.BGDarken = true;

            PopupManager._popup[] old = manager.popups ?? new PopupManager._popup[0];
            PopupManager._popup[] expanded = new PopupManager._popup[old.Length + 1];
            Array.Copy(old, expanded, old.Length);
            expanded[old.Length] = entry;
            manager.popups = expanded;

            root.SetActive(false);
            return root;
        }

        private static Transform FindPopupParent(PopupManager manager)
        {
            if (manager.popups != null)
            {
                for (int i = 0; i < manager.popups.Length; i++)
                {
                    PopupManager._popup popup = manager.popups[i];
                    if (popup != null && popup.obj != null && popup.obj.transform.parent != null)
                        return popup.obj.transform.parent;
                }
            }
            return manager.transform;
        }

        private static PopupManager GetManager()
        {
            try
            {
                if (Camera.main == null)
                    return null;
                mainScript main = Camera.main.GetComponent<mainScript>();
                if (main == null || main.Data == null)
                    return null;
                return main.Data.GetComponent<PopupManager>();
            }
            catch
            {
                return null;
            }
        }
    }
}
