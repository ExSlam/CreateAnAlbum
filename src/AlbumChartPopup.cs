using UnityEngine;
using CreateAnAlbumChartTrackEnhancements;

namespace Albummodelite
{
    public class AlbumChartPopup : MonoBehaviour
    {
        public bool Open(bool queueBehindCurrentPopup = false)
        {
            return AlbumChartEnhancement.Toggle(queueBehindCurrentPopup);
        }
    }
}
