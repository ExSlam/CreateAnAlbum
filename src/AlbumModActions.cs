using System;
using UnityEngine;

namespace Albummodelite
{
    public static class AlbumModActions
    {
        public static void OpenCreateAlbum()
        {
            Debug.Log("[CreateAnAlbum] Create Album clicked.");

            try
            {
                CreateAnAlbum runtime =
                    UnityEngine.Object.FindObjectOfType<CreateAnAlbum>();

                if (runtime == null)
                    throw new InvalidOperationException(
                        "Create An Album is only available during gameplay."
                    );

                if (!runtime.OpenCreateAlbum(true))
                    throw new InvalidOperationException(
                        "The Create Album popup request was rejected."
                    );
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[CreateAnAlbum] Create Album failed:\n" +
                    ex
                );
                throw;
            }
        }

        public static void OpenDiscography()
        {
            Debug.Log("[CreateAnAlbum] Discography clicked.");

            try
            {
                CreateAnAlbum runtime =
                    UnityEngine.Object.FindObjectOfType<CreateAnAlbum>();

                if (runtime == null)
                    throw new InvalidOperationException(
                        "Create An Album is only available during gameplay."
                    );

                if (!runtime.OpenAlbumLibrary(true))
                    throw new InvalidOperationException(
                        "The Discography popup request was rejected."
                    );
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[CreateAnAlbum] Discography failed:\n" +
                    ex
                );
                throw;
            }
        }

        public static void OpenAlbumChart()
        {
            Debug.Log("[CreateAnAlbum] Album Chart clicked.");

            try
            {
                CreateAnAlbum runtime =
                    UnityEngine.Object.FindObjectOfType<CreateAnAlbum>();

                if (runtime == null)
                    throw new InvalidOperationException(
                        "Create An Album is only available during gameplay."
                    );

                Debug.Log(
                    "[CreateAnAlbum] Runtime host found."
                );

                if (!runtime.OpenAlbumChart(true))
                    throw new InvalidOperationException(
                        "The Album Chart popup request was rejected."
                    );

                Debug.Log(
                    "[CreateAnAlbum] OpenAlbumChart call completed."
                );
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[CreateAnAlbum] Album Chart failed:\n" +
                    ex
                );
                throw;
            }
        }
    }
}
