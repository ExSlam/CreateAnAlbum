using HarmonyLib;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace Albummodelite
{
    /// <summary>
    /// Optional reflection-only migration bridge for Cosmo's Graduation Details.
    /// New CAA albums own their historical portrait references themselves, but this bridge can
    /// enrich old ID-only CAA albums when the idol had already graduated before schema v4.
    /// No compile-time dependency is introduced.
    /// </summary>
    internal static class GraduationDetailsIntegration
    {
        private const string HarmonyId = "com.cosmo.graduationdetails";
        private const string AssemblyName = "com.cosmo.graduationdetails";
        private const string StoreTypeName = "GraduationDetails.GraduationSnapshotStore";

        private static bool probed;
        private static MethodInfo getSnapshotMethod;

        internal static bool TryGetMemberSnapshot(int girlId, out AlbumMemberSnapshot result)
        {
            result = null;
            EnsureProbed();
            if (getSnapshotMethod == null)
                return false;

            try
            {
                object source = getSnapshotMethod.Invoke(null, new object[] { girlId });
                if (source == null)
                    return false;

                Type type = source.GetType();
                AlbumMemberSnapshot snapshot = new AlbumMemberSnapshot
                {
                    GirlId = ReadField(type, source, "GirlId", girlId),
                    FirstName = ReadField(type, source, "FirstName", string.Empty),
                    LastName = ReadField(type, source, "LastName", string.Empty),
                    Nickname = ReadField(type, source, "Nickname", string.Empty),
                    IdolType = ReadField(type, source, "IdolType", data_girls.girls._type.NORMAL),
                    CustomId = ReadField(type, source, "CustomId", string.Empty),
                    CustomSpriteAddress = ReadField(type, source, "CustomSpriteAddress", string.Empty)
                };

                FieldInfo portraitAssetsField = type.GetField(
                    "PortraitAssets",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                IEnumerable portraitAssets = portraitAssetsField != null
                    ? portraitAssetsField.GetValue(source) as IEnumerable
                    : null;
                if (portraitAssets != null)
                {
                    foreach (object reference in portraitAssets)
                    {
                        if (reference == null)
                            continue;
                        Type referenceType = reference.GetType();
                        string assetId = ReadField(referenceType, reference, "AssetId", string.Empty);
                        data_girls_textures._spriteType spriteType = ReadField(
                            referenceType,
                            reference,
                            "Type",
                            data_girls_textures._spriteType.NONE);
                        if (string.IsNullOrEmpty(assetId) || spriteType == data_girls_textures._spriteType.NONE)
                            continue;
                        snapshot.PortraitAssets.Add(new AlbumPortraitAssetReference
                        {
                            Type = spriteType,
                            AssetId = assetId
                        });
                    }
                }

                if (snapshot.GirlId != girlId)
                    return false;
                result = snapshot;
                return true;
            }
            catch
            {
                // This is migration assistance only. A changed Graduation Details internal API
                // must never make CAA persistence or cover rendering fail.
                return false;
            }
        }

        private static void EnsureProbed()
        {
            if (probed)
                return;
            probed = true;

            try
            {
                if (!Harmony.HasAnyPatches(HarmonyId))
                    return;

                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(candidate => string.Equals(
                        candidate.GetName().Name,
                        AssemblyName,
                        StringComparison.OrdinalIgnoreCase));
                if (assembly == null)
                    return;

                Type storeType = assembly.GetType(StoreTypeName, false);
                if (storeType == null)
                    return;

                getSnapshotMethod = storeType.GetMethod(
                    "GetSnapshot",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new Type[] { typeof(int) },
                    null);
            }
            catch
            {
                getSnapshotMethod = null;
            }
        }

        private static T ReadField<T>(Type type, object instance, string name, T fallback)
        {
            if (type == null || instance == null)
                return fallback;
            FieldInfo field = type.GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
                return fallback;
            object value = field.GetValue(instance);
            if (value is T)
                return (T)value;
            return fallback;
        }
    }
}
