using System;
using System.IO;
using System.Reflection;

namespace Albummodelite
{
    internal static class AlbumLocalization
    {
        private static object localizer;
        private static MethodInfo getMethod;
        private static bool resolved;

        internal static string Get(string key, string fallback)
        {
            Resolve();
            if (localizer == null || getMethod == null)
                return fallback;

            try
            {
                string value = getMethod.Invoke(
                    localizer,
                    new object[] { key, fallback }
                ) as string;
                return value ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static void Resolve()
        {
            if (resolved)
                return;
            resolved = true;

            try
            {
                Type localizationType = null;
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    localizationType = assemblies[i].GetType(
                        "ModLocalizationSystem.ModLocalization",
                        false
                    );
                    if (localizationType != null)
                        break;
                }
                if (localizationType == null)
                    return;

                MethodInfo forDirectory = localizationType.GetMethod(
                    "ForDirectory",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(string) },
                    null
                );
                if (forDirectory == null)
                    return;

                string directory = Path.GetDirectoryName(
                    typeof(AlbumLocalization).Assembly.Location
                );
                localizer = forDirectory.Invoke(
                    null,
                    new object[] { directory ?? "" }
                );
                if (localizer == null)
                    return;

                getMethod = localizer.GetType().GetMethod(
                    "Get",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new Type[] { typeof(string), typeof(string) },
                    null
                );
            }
            catch
            {
                localizer = null;
                getMethod = null;
            }
        }
    }
}
