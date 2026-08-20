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
                {
                    Debug.LogError(
                        "[CreateAnAlbum] Runtime host was not found."
                    );
                    return;
                }

                runtime.OpenCreateAlbum();
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[CreateAnAlbum] Create Album failed:\n" +
                    ex
                );
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
                {
                    Debug.LogError(
                        "[CreateAnAlbum] Runtime host was not found."
                    );
                    return;
                }

                runtime.OpenAlbumLibrary();
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    "[CreateAnAlbum] Discography failed:\n" +
                    ex
                );
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
                {
                    Debug.LogError(
                        "[CreateAnAlbum] Runtime host was not found."
                    );
                    return;
                }

                Debug.Log(
                    "[CreateAnAlbum] Runtime host found."
                );

                runtime.OpenAlbumChart();

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
            }
        }
    }
}
