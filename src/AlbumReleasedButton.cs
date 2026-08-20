using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AlbumReleasedButton
    : MonoBehaviour
{
    public TextMeshProUGUI Title;

    public TextMeshProUGUI Sales;

    public RawImage Cover;

    public void Set(
        AlbumData album
    )
    {
        Title.text =
            album.Title;

        Sales.text =
            album.Sales.ToString();
    }
}
