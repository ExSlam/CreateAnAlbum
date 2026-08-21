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

}
