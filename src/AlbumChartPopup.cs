using UnityEngine;
using CreateAnAlbumChartTrackEnhancements;

namespace Albummodelite
{
    public class AlbumChartPopup : MonoBehaviour
    {
        public void Open()
        {
            AlbumChartEnhancement.Toggle();
        }
    }
}
