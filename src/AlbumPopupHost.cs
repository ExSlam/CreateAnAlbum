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
        Chart,
        ChartUpdate,
        Production
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
        private const int ChartUpdateId = 0x414C5550; // ALUP
        private const int ProductionId = 0x414C5052; // ALPR

        private static readonly Dictionary<AlbumPopupKind, GameObject> roots =
            new Dictionary<AlbumPopupKind, GameObject>();

        private static readonly HashSet<AlbumPopupKind> closing =
            new HashSet<AlbumPopupKind>();

        private static PopupManager registeredManager;
        private static int lifecycleGeneration;

        internal static PopupManager._type TypeFor(AlbumPopupKind kind)
        {
            switch (kind)
            {
                case AlbumPopupKind.Create: return (PopupManager._type)CreateId;
                case AlbumPopupKind.Library: return (PopupManager._type)LibraryId;
                case AlbumPopupKind.Detail: return (PopupManager._type)DetailId;
                case AlbumPopupKind.Chart: return (PopupManager._type)ChartId;
                case AlbumPopupKind.ChartUpdate: return (PopupManager._type)ChartUpdateId;
                default: return (PopupManager._type)ProductionId;
            }
        }

        internal static GameObject Prepare(AlbumPopupKind kind)
        {
            PopupManager manager = GetManager();
            if (manager == null)
                return null;

            EnsureManager(manager);

            if (closing.Contains(kind))
                return null;

            PopupManager._type type = TypeFor(kind);
            if (manager.queue.Contains(type))
                return null;

            GameObject root;
            if (!roots.TryGetValue(kind, out root) || root == null)
            {
                PopupManager._popup existing = FindEntry(manager, type);
                if (existing != null && existing.obj != null &&
                    existing.obj.GetComponent<Popup>() != null)
                {
                    bool wasOpen = existing.open;
                    root = existing.obj;
                    RegisterRoot(manager, kind, root, wasOpen);
                }
                else
                {
                    root = CreateRoot(manager, kind);
                    if (root == null)
                        return null;
                }
                roots[kind] = root;
            }

            PopupManager._popup entry = FindEntry(manager, type);
            if (entry == null || entry.obj != root || entry.open || root.activeInHierarchy)
                return null;

            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.transform.GetChild(i).gameObject;
                child.SetActive(false);
                child.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(child);
            }

            root.SetActive(false);
            return root;
        }

        internal static bool Open(
            AlbumPopupKind kind,
            bool queueBehindCurrentPopup = false
        )
        {
            PopupManager manager = GetManager();
            if (manager == null || ActiveDialogueController.ShowingDialogue)
                return false;

            EnsureManager(manager);

            if (closing.Contains(kind))
                return false;

            PopupManager._type type = TypeFor(kind);
            PopupManager._popup entry = FindEntry(manager, type);
            GameObject root;
            if (!roots.TryGetValue(kind, out root) || root == null ||
                entry == null || entry.obj != root)
                return false;

            if (entry.open || manager.queue.Contains(type))
                return false;

            if (!queueBehindCurrentPopup && !IsPopupSystemIdle(manager))
                return false;

            ApplyLayerRecursively(root, root.transform.parent.gameObject.layer);

            // Mod Buttons invokes actions before closing its Action Hub. Queueing here lets
            // PopupManager advance to this popup when the hub closes.
            manager.Open(type, true);
            return true;
        }

        internal static bool Transition(AlbumPopupKind from, AlbumPopupKind to)
        {
            PopupManager manager = GetManager();
            if (manager == null || ActiveDialogueController.ShowingDialogue ||
                closing.Contains(from) || closing.Contains(to))
                return false;

            EnsureManager(manager);

            PopupManager._type sourceType = TypeFor(from);
            PopupManager._type targetType = TypeFor(to);
            PopupManager._popup source = FindEntry(manager, sourceType);
            PopupManager._popup target = FindEntry(manager, targetType);
            GameObject sourceRoot;
            GameObject targetRoot;
            if (source == null || target == null || !source.open || target.open ||
                !roots.TryGetValue(from, out sourceRoot) || sourceRoot == null ||
                source.obj != sourceRoot ||
                !roots.TryGetValue(to, out targetRoot) || targetRoot == null ||
                target.obj != targetRoot || manager.queue.Contains(targetType) ||
                manager.queue.Count != 1 || manager.queue[0] != sourceType)
                return false;

            ApplyLayerRecursively(targetRoot, targetRoot.transform.parent.gameObject.layer);
            closing.Add(from);
            int generation = lifecycleGeneration;
            manager.Open(targetType, true);
            manager.Close(delegate
            {
                FinishClose(from, sourceRoot, generation, null);
            });
            return true;
        }

        internal static bool IsOpen(AlbumPopupKind kind)
        {
            PopupManager manager = GetManager();
            PopupManager._popup entry = manager != null ? FindEntry(manager, TypeFor(kind)) : null;
            return entry != null && entry.open;
        }

        internal static bool IsOpenQueuedOrClosing(AlbumPopupKind kind)
        {
            PopupManager manager = GetManager();
            if (manager == null)
                return false;

            PopupManager._type type = TypeFor(kind);
            PopupManager._popup entry = FindEntry(manager, type);
            return closing.Contains(kind) ||
                (entry != null && entry.open) ||
                manager.queue.Contains(type);
        }

        internal static bool IsPopupSystemIdle()
        {
            PopupManager manager = GetManager();
            if (manager == null)
                return false;

            EnsureManager(manager);
            return IsPopupSystemIdle(manager);
        }

        internal static bool Close(AlbumPopupKind kind, Action onClosed = null)
        {
            PopupManager manager = GetManager();
            if (manager == null || closing.Contains(kind))
                return false;

            PopupManager._popup entry = FindEntry(manager, TypeFor(kind));
            GameObject root;
            if (entry == null || !entry.open ||
                !roots.TryGetValue(kind, out root) || root == null || entry.obj != root)
                return false;

            closing.Add(kind);
            int generation = lifecycleGeneration;
            manager.Close(delegate
            {
                FinishClose(kind, root, generation, onClosed);
            });
            return true;
        }

        internal static void Reset()
        {
            PopupManager manager = registeredManager != null ? registeredManager : GetManager();
            lifecycleGeneration++;
            CleanupManager(manager);
            ClearLocalRoots();
            registeredManager = null;
        }

        private static GameObject CreateRoot(PopupManager manager, AlbumPopupKind kind)
        {
            Transform parent = FindPopupParent(manager);
            if (parent == null)
                return null;

            GameObject root = new GameObject(
                "CreateAnAlbum_" + kind + "PopupRoot",
                typeof(RectTransform),
                typeof(CanvasGroup)
            );
            root.transform.SetParent(parent, false);
            ApplyLayerRecursively(root, parent.gameObject.layer);
            // Popup.OnEnable calls Show immediately. Keep the root inactive while the Popup
            // component and its OnOpen event are initialized.
            root.SetActive(false);

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;

            Popup popup = root.AddComponent<Popup>();
            popup.OnOpen = new UnityEvent();
            popup.ShowAnimation = true;
            popup.HideAnimation = true;
            popup.Increase_Popup_Counter = true;

            RegisterRoot(manager, kind, root, false);

            root.SetActive(false);
            return root;
        }

        private static void EnsureManager(PopupManager manager)
        {
            if (!object.ReferenceEquals(registeredManager, null) &&
                registeredManager != manager)
            {
                lifecycleGeneration++;
                if (registeredManager != null)
                    CleanupManager(registeredManager);
                ClearLocalRoots();
            }

            registeredManager = manager;
        }

        private static void RegisterRoot(
            PopupManager manager,
            AlbumPopupKind kind,
            GameObject root,
            bool rootWasOpen
        )
        {
            PopupManager._type type = TypeFor(kind);
            PopupManager._popup[] old = manager.popups ?? new PopupManager._popup[0];
            List<PopupManager._popup> updated = new List<PopupManager._popup>(old.Length + 1);
            PopupManager._popup registration = null;

            for (int i = 0; i < old.Length; i++)
            {
                PopupManager._popup entry = old[i];
                if (entry != null && entry.type == type && entry.obj == root)
                {
                    registration = entry;
                    break;
                }
            }

            bool newRegistration = registration == null;
            if (newRegistration)
            {
                registration = new PopupManager._popup();
                registration.type = type;
            }

            for (int i = 0; i < old.Length; i++)
            {
                PopupManager._popup entry = old[i];
                if (entry == null)
                    continue;

                if (entry.type != type)
                {
                    updated.Add(entry);
                    continue;
                }

                if (entry == registration)
                {
                    updated.Add(entry);
                }
                else
                {
                    DisposeDuplicateRegistration(entry, root);
                }
            }

            if (newRegistration)
                updated.Add(registration);

            registration.obj = root;
            registration.open = rootWasOpen;
            registration.BGBlur = true;
            registration.BGDarken = true;
            registration.BGRenderTexture = null;
            manager.popups = updated.ToArray();

            bool keptQueueEntry = false;
            for (int i = 0; i < manager.queue.Count; i++)
            {
                if (manager.queue[i] != type)
                    continue;
                if (!keptQueueEntry)
                {
                    keptQueueEntry = true;
                    continue;
                }
                manager.queue.RemoveAt(i);
                i--;
            }
        }

        private static void DisposeDuplicateRegistration(
            PopupManager._popup entry,
            GameObject retainedRoot
        )
        {
            if (entry == null)
                return;

            GameObject duplicateRoot = entry.obj;
            bool wasOpen = entry.open;
            if (duplicateRoot == null)
            {
                if (wasOpen && PopupManager.PopupCounter > 0)
                    PopupManager.PopupCounter--;
                entry.open = false;
                return;
            }

            if (duplicateRoot != null && duplicateRoot != retainedRoot)
            {
                Popup popup = duplicateRoot.GetComponent<Popup>();
                if (wasOpen && popup != null)
                {
                    popup.HideAnimation = false;
                    entry.Close();
                }
                else
                {
                    if (wasOpen && PopupManager.PopupCounter > 0)
                        PopupManager.PopupCounter--;
                    entry.open = false;
                }

                UnityEngine.Object.Destroy(
                    duplicateRoot,
                    duplicateRoot.activeInHierarchy ? 0.6f : 0f
                );
            }
            entry.open = false;
        }

        private static PopupManager._popup FindEntry(
            PopupManager manager,
            PopupManager._type type
        )
        {
            if (manager == null || manager.popups == null)
                return null;

            PopupManager._popup fallback = null;
            for (int i = 0; i < manager.popups.Length; i++)
            {
                PopupManager._popup entry = manager.popups[i];
                if (entry == null || entry.type != type)
                    continue;
                if (fallback == null)
                    fallback = entry;
                if (entry.obj != null)
                    return entry;
            }
            return fallback;
        }

        private static void FinishClose(
            AlbumPopupKind kind,
            GameObject expectedRoot,
            int generation,
            Action onClosed
        )
        {
            GameObject root;
            if (generation != lifecycleGeneration ||
                !roots.TryGetValue(kind, out root) || root == null ||
                root != expectedRoot)
            {
                return;
            }

            if (!closing.Remove(kind))
                return;

            root.SetActive(false);
            if (onClosed != null)
                onClosed();
        }

        private static void CleanupManager(PopupManager manager)
        {
            if (manager == null)
                return;

            if (manager.popups == null)
            {
                RemoveOwnedQueueEntries(manager, false);
                return;
            }

            List<PopupManager._popup> nonNull =
                new List<PopupManager._popup>(manager.popups.Length);
            for (int i = 0; i < manager.popups.Length; i++)
            {
                if (manager.popups[i] != null)
                    nonNull.Add(manager.popups[i]);
            }
            manager.popups = nonNull.ToArray();

            bool canCloseThroughManager = false;
            if (manager.queue.Count > 0 && IsOwnedType(manager.queue[0]))
            {
                PopupManager._popup head = FindEntry(manager, manager.queue[0]);
                canCloseThroughManager = head != null && head.open && head.obj != null;
            }

            for (int i = 0; i < manager.popups.Length; i++)
            {
                PopupManager._popup entry = manager.popups[i];
                if (!IsOwnedType(entry.type) || !entry.open)
                    continue;

                if (entry.obj == null)
                {
                    if (PopupManager.PopupCounter > 0)
                        PopupManager.PopupCounter--;
                    entry.open = false;
                    continue;
                }

                Popup popup = entry.obj.GetComponent<Popup>();
                if (popup != null)
                    popup.HideAnimation = false;
                else
                {
                    if (PopupManager.PopupCounter > 0)
                        PopupManager.PopupCounter--;
                    entry.obj.SetActive(false);
                    entry.open = false;
                }
            }

            RemoveOwnedQueueEntries(manager, canCloseThroughManager);

            if (canCloseThroughManager)
            {
                manager.Close();
            }
            else
            {
                for (int i = 0; i < manager.popups.Length; i++)
                {
                    PopupManager._popup entry = manager.popups[i];
                    if (!IsOwnedType(entry.type) || !entry.open || entry.obj == null)
                        continue;

                    Popup popup = entry.obj.GetComponent<Popup>();
                    if (popup != null)
                        entry.Close();
                    else
                    {
                        if (PopupManager.PopupCounter > 0)
                            PopupManager.PopupCounter--;
                        entry.obj.SetActive(false);
                        entry.open = false;
                    }
                }

                if (!HasOpenPopup(manager))
                {
                    if (manager.queue.Count == 0)
                        manager.Close();
                    else
                        manager.RunTheQueue();
                }
            }

            RemoveRegistrations(manager);
        }

        private static void RemoveOwnedQueueEntries(
            PopupManager manager,
            bool preserveOpenHead
        )
        {
            for (int i = manager.queue.Count - 1; i >= 0; i--)
            {
                if (!IsOwnedType(manager.queue[i]))
                    continue;
                if (preserveOpenHead && i == 0)
                    continue;
                manager.queue.RemoveAt(i);
            }
        }

        private static bool HasOpenPopup(PopupManager manager)
        {
            if (manager.popups == null)
                return false;

            for (int i = 0; i < manager.popups.Length; i++)
            {
                PopupManager._popup entry = manager.popups[i];
                if (entry != null && entry.open)
                    return true;
            }
            return false;
        }

        private static void ClearLocalRoots()
        {
            foreach (GameObject root in roots.Values)
            {
                if (root != null)
                {
                    UnityEngine.Object.Destroy(
                        root,
                        root.activeInHierarchy ? 0.6f : 0f
                    );
                }
            }
            roots.Clear();
            closing.Clear();
        }

        private static void RemoveRegistrations(PopupManager manager)
        {
            if (manager.popups == null)
                return;

            List<PopupManager._popup> retained =
                new List<PopupManager._popup>(manager.popups.Length);
            for (int i = 0; i < manager.popups.Length; i++)
            {
                PopupManager._popup entry = manager.popups[i];
                if (entry != null && IsOwnedType(entry.type))
                {
                    entry.open = false;
                    continue;
                }
                retained.Add(entry);
            }
            manager.popups = retained.ToArray();
        }

        private static bool IsOwnedType(PopupManager._type type)
        {
            return type == TypeFor(AlbumPopupKind.Create) ||
                type == TypeFor(AlbumPopupKind.Library) ||
                type == TypeFor(AlbumPopupKind.Detail) ||
                type == TypeFor(AlbumPopupKind.Chart) ||
                type == TypeFor(AlbumPopupKind.ChartUpdate) ||
                type == TypeFor(AlbumPopupKind.Production);
        }

        internal static void ApplyLayerRecursively(GameObject root, int layer)
        {
            if (root == null)
                return;

            root.layer = layer;
            for (int i = 0; i < root.transform.childCount; i++)
                ApplyLayerRecursively(root.transform.GetChild(i).gameObject, layer);
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

        private static bool IsPopupSystemIdle(PopupManager manager)
        {
            if (manager == null || ActiveDialogueController.ShowingDialogue ||
                PopupManager.PopupCounter != 0 || manager.queue.Count != 0 ||
                manager.IsThereAnOpenPopup() || HasDueWaitingEvent(manager))
            {
                return false;
            }

            try
            {
                return staticVars.IsGameplay() && !mainScript.IsBlockingHotkeys();
            }
            catch
            {
                return false;
            }
        }

        private static bool HasDueWaitingEvent(PopupManager manager)
        {
            try
            {
                Event_Manager eventManager = manager.GetComponent<Event_Manager>();
                if (eventManager == null || eventManager.activeEvents == null)
                    return false;

                DateTime currentDate = staticVars.dateTime;
                for (int i = 0; i < eventManager.activeEvents.Count; i++)
                {
                    Event_Manager._activeEvent activeEvent = eventManager.activeEvents[i];
                    if (activeEvent != null &&
                        activeEvent.state == Event_Manager._activeEvent._state.waiting &&
                        activeEvent.date <= currentDate)
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                // Automatic album reports are deliberately lower priority than game events.
                return true;
            }
        }
    }
}
