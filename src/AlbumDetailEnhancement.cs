using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Albummodelite;

namespace CreateAnAlbumChartTrackEnhancements
{
    internal static class AlbumCollaborationLookup
    {
        private static Type managerType;
        private static MemberInfo collaborationsMember;
        private static bool searched;

        internal static string GetFeatureText(
            singles._single song)
        {
            if (song == null)
                return "";

            try
            {
                IDictionary dictionary =
                    GetDictionary();

                if (dictionary == null ||
                    !dictionary.Contains(song.id))
                {
                    return "";
                }

                object data =
                    dictionary[song.id];

                if (data == null)
                    return "";

                object activeValue =
                    GetMemberValue(
                        data,
                        "IsActive"
                    );

                if (activeValue is bool &&
                    !(bool)activeValue)
                {
                    return "";
                }

                string rivalGroup =
                    Convert.ToString(
                        GetMemberValue(
                            data,
                            "RivalGroupName"
                        )
                    );

                string rivalIdol =
                    Convert.ToString(
                        GetMemberValue(
                            data,
                            "RivalIdolName"
                        )
                    );

                rivalGroup =
                    rivalGroup == null
                        ? ""
                        : rivalGroup.Trim();

                rivalIdol =
                    rivalIdol == null
                        ? ""
                        : rivalIdol.Trim();

                if (!string.IsNullOrEmpty(rivalIdol) &&
                    !string.IsNullOrEmpty(rivalGroup))
                {
                    return
                        "feat. " +
                        rivalIdol +
                        "  •  " +
                        rivalGroup;
                }

                if (!string.IsNullOrEmpty(rivalIdol))
                    return "feat. " + rivalIdol;

                if (!string.IsNullOrEmpty(rivalGroup))
                    return "feat. " + rivalGroup;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[AlbumDetail] Collaboration lookup failed for single " +
                    song.id +
                    ": " +
                    ex.Message
                );
            }

            return "";
        }

        private static IDictionary GetDictionary()
        {
            FindManager();

            if (managerType == null ||
                collaborationsMember == null)
            {
                return null;
            }

            object value = null;

            FieldInfo field =
                collaborationsMember as FieldInfo;

            if (field != null)
                value = field.GetValue(null);

            PropertyInfo property =
                collaborationsMember as PropertyInfo;

            if (property != null)
                value = property.GetValue(null, null);

            return value as IDictionary;
        }

        private static void FindManager()
        {
            if (searched)
                return;

            searched = true;

            Assembly[] assemblies =
                AppDomain.CurrentDomain
                    .GetAssemblies();

            foreach (Assembly assembly
                     in assemblies)
            {
                Type[] types;

                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }
                catch
                {
                    continue;
                }

                if (types == null)
                    continue;

                foreach (Type type in types)
                {
                    if (type == null ||
                        type.Name !=
                            "CollaborationManager")
                    {
                        continue;
                    }

                    FieldInfo field =
                        type.GetField(
                            "SingleCollaborations",
                            BindingFlags.Static |
                            BindingFlags.Public |
                            BindingFlags.NonPublic
                        );

                    if (field != null)
                    {
                        managerType = type;
                        collaborationsMember =
                            field;

                        Debug.Log(
                            "[AlbumDetail] Found collaboration manager: " +
                            type.FullName
                        );

                        return;
                    }

                    PropertyInfo property =
                        type.GetProperty(
                            "SingleCollaborations",
                            BindingFlags.Static |
                            BindingFlags.Public |
                            BindingFlags.NonPublic
                        );

                    if (property != null)
                    {
                        managerType = type;
                        collaborationsMember =
                            property;

                        Debug.Log(
                            "[AlbumDetail] Found collaboration manager property: " +
                            type.FullName
                        );

                        return;
                    }
                }
            }
        }

        private static object GetMemberValue(
            object instance,
            string name)
        {
            if (instance == null)
                return null;

            Type type =
                instance.GetType();

            PropertyInfo property =
                type.GetProperty(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                );

            if (property != null)
                return property.GetValue(
                    instance,
                    null
                );

            FieldInfo field =
                type.GetField(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic
                );

            if (field != null)
                return field.GetValue(instance);

            return null;
        }
    }

    [HarmonyPatch(typeof(AlbumDetailPopup), "Open")]
    internal static class AlbumDetailScrollableTracksPatch
    {
        private static void Postfix(
            AlbumDetailPopup __instance,
            AlbumData album)
        {
            if (__instance == null ||
                album == null)
            {
                return;
            }

            try
            {
                FieldInfo panelField =
                    AccessTools.Field(
                        typeof(AlbumDetailPopup),
                        "panel"
                    );

                if (panelField == null)
                    return;

                GameObject panel =
                    panelField.GetValue(__instance)
                        as GameObject;

                if (panel == null)
                    return;

                // Remove the original fixed list. It currently starts low
                // on the popup and keeps drawing downward past the panel.
                Transform oldLabel =
                    panel.transform.Find(
                        "TracksLabel"
                    );

                if (oldLabel != null)
                    UnityEngine.Object.Destroy(
                        oldLabel.gameObject
                    );

                List<GameObject> oldTracks =
                    new List<GameObject>();

                foreach (Transform child
                         in panel.transform)
                {
                    if (child != null &&
                        child.name.StartsWith(
                            "Track_",
                            StringComparison.Ordinal
                        ))
                    {
                        oldTracks.Add(
                            child.gameObject
                        );
                    }
                }

                foreach (GameObject oldTrack
                         in oldTracks)
                {
                    UnityEngine.Object.Destroy(
                        oldTrack
                    );
                }

                BuildTrackArea(
                    panel.transform,
                    album
                );
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[AlbumDetail] Scrollable track list failed:\n" +
                    ex
                );
            }
        }

        private static void BuildTrackArea(
            Transform parent,
            AlbumData album)
        {
            const float x = 330f;

            CreateText(
                parent,
                "EnhancedTracksLabel",
                "TRACK LIST",
                12,
                FontStyle.Bold,
                new Color(
                    0.39f,
                    0.34f,
                    0.72f
                ),
                new Vector2(
                    x,
                    -296f
                ),
                new Vector2(
                    300f,
                    24f
                ),
                TextAnchor.MiddleLeft
            );

            GameObject scrollRoot =
                new GameObject(
                    "AlbumTrackScrollRoot"
                );

            scrollRoot.transform.SetParent(
                parent,
                false
            );

            RectTransform scrollRootRect =
                scrollRoot.AddComponent<
                    RectTransform>();

            scrollRootRect.anchorMin =
                new Vector2(
                    0f,
                    1f
                );

            scrollRootRect.anchorMax =
                new Vector2(
                    0f,
                    1f
                );

            scrollRootRect.pivot =
                new Vector2(
                    0f,
                    1f
                );

            scrollRootRect.sizeDelta =
                new Vector2(
                    315f,
                    141f
                );

            scrollRootRect.anchoredPosition =
                new Vector2(
                    x,
                    -322f
                );

            ScrollRect scroll =
                scrollRoot.AddComponent<
                    ScrollRect>();

            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType =
                ScrollRect.MovementType
                    .Clamped;
            scroll.scrollSensitivity =
                25f;

            GameObject viewport =
                new GameObject(
                    "AlbumTrackViewport"
                );

            viewport.transform.SetParent(
                scrollRoot.transform,
                false
            );

            RectTransform vr =
                viewport.AddComponent<
                    RectTransform>();

            vr.anchorMin = Vector2.zero;
            vr.anchorMax = Vector2.one;
            vr.offsetMin = Vector2.zero;
            vr.offsetMax = Vector2.zero;

            Image viewportBg =
                viewport.AddComponent<Image>();

            viewportBg.color =
                new Color(
                    0.985f,
                    0.985f,
                    0.992f,
                    0.85f
                );

            Outline outline =
                viewport.AddComponent<
                    Outline>();

            outline.effectColor =
                new Color(
                    0.84f,
                    0.83f,
                    0.90f
                );

            outline.effectDistance =
                new Vector2(
                    1f,
                    -1f
                );

            viewport.AddComponent<
                RectMask2D>();

            GameObject content =
                new GameObject(
                    "TrackContent"
                );

            content.transform.SetParent(
                viewport.transform,
                false
            );

            RectTransform cr =
                content.AddComponent<
                    RectTransform>();

            cr.anchorMin =
                new Vector2(
                    0f,
                    1f
                );

            cr.anchorMax =
                new Vector2(
                    1f,
                    1f
                );

            cr.pivot =
                new Vector2(
                    0.5f,
                    1f
                );

            cr.anchoredPosition =
                Vector2.zero;

            int count =
                album.Songs != null
                    ? album.Songs.Count
                    : 0;

            const float rowHeight = 42f;

            cr.sizeDelta =
                new Vector2(
                    -12f,
                    Mathf.Max(
                        150f,
                        count *
                            rowHeight +
                        8f
                    )
                );

            scroll.viewport = vr;
            scroll.content = cr;
            AlbumUiResources.AttachVanillaListScrollIndicator(
                scrollRoot.transform,
                scroll,
                "AlbumTrackScrollIndicator"
            );
            scroll.verticalNormalizedPosition = 1f;

            if (count == 0)
            {
                CreateText(
                    content.transform,
                    "NoTracks",
                    "No tracks restored yet.",
                    10,
                    FontStyle.Italic,
                    new Color(
                        0.50f,
                        0.49f,
                        0.56f
                    ),
                    new Vector2(
                        10f,
                        -8f
                    ),
                    new Vector2(
                        270f,
                        28f
                    ),
                    TextAnchor.MiddleLeft
                );

                return;
            }

            for (int i = 0;
                 i < count;
                 i++)
            {
                singles._single song =
                    album.Songs[i];

                string title =
                    song != null &&
                    !string.IsNullOrEmpty(
                        song.title)
                        ? song.title
                        : "Unknown";

                float y =
                    -4f -
                    i * rowHeight;

                CreateText(
                    content.transform,
                    "TrackTitle_" + i,
                    (i + 1) +
                        ". " +
                        title,
                    10,
                    FontStyle.Normal,
                    new Color(
                        0.28f,
                        0.27f,
                        0.34f
                    ),
                    new Vector2(
                        9f,
                        y
                    ),
                    new Vector2(
                        270f,
                        21f
                    ),
                    TextAnchor.MiddleLeft
                );

                string feature =
                    AlbumCollaborationLookup
                        .GetFeatureText(song);

                if (!string.IsNullOrEmpty(
                        feature))
                {
                    CreateText(
                        content.transform,
                        "TrackFeature_" + i,
                        feature,
                        8,
                        FontStyle.Bold,
                        new Color(
                            0.42f,
                            0.38f,
                            0.72f
                        ),
                        new Vector2(
                            25f,
                            y - 20f
                        ),
                        new Vector2(
                            252f,
                            18f
                        ),
                        TextAnchor.MiddleLeft
                    );
                }
            }
        }

        private static void CreateText(
            Transform parent,
            string name,
            string value,
            int fontSize,
            FontStyle style,
            Color color,
            Vector2 position,
            Vector2 size,
            TextAnchor alignment)
        {
            GameObject obj =
                new GameObject(name);

            obj.transform.SetParent(
                parent,
                false
            );

            RectTransform rect =
                obj.AddComponent<
                    RectTransform>();

            rect.anchorMin =
                new Vector2(
                    0f,
                    1f
                );

            rect.anchorMax =
                new Vector2(
                    0f,
                    1f
                );

            rect.pivot =
                new Vector2(
                    0f,
                    1f
                );

            rect.anchoredPosition =
                position;

            rect.sizeDelta =
                size;

            Text text =
                obj.AddComponent<Text>();

            text.font =
                AlbumUiResources.GetGameFont();

            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize =
                Math.Max(
                    7,
                    fontSize - 3
                );
            text.resizeTextMaxSize =
                fontSize;
        }
    }
}
