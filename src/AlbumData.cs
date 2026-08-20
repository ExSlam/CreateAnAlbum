using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AlbumData
{
    public int ID;
    public string Title = "";
    public string GroupName = "";

    public DateTime ReleaseDate;
    public bool Released;

    public bool PlayerAlbum = true;
    public string RivalName = "";
    public int RivalGroupId = -1;

    public List<data_girls.girls> Members =
        new List<data_girls.girls>();

    public List<singles._single> Songs =
        new List<singles._single>();

    public long Sales;
    public long WeeklySales;
    public long Profit;

    public int ChartPosition;
    public int PreviousChartPosition;
    public int PeakChartPosition;
    public int WeeksOnChart;

    public string CoverPath = "";
    public string Theme = "";

    public int ThemeIndex;
    public int BackgroundIndex;
    public int LayoutIndex;
    public int FontIndex;
    public int TextColorIndex;
    public int TitlePosition;

    public bool ShowGroupName = true;

    public int OrnamentStyle;
    public int FrameStyle;
    public int TitleEffect;

    public float PortraitScale = 1f;
    public float CenterEmphasis = 1.08f;
    public float PortraitYOffset;
    public float PortraitSpacing = 1f;
    public float EffectsIntensity = 1f;

    // Index into Members. -1 means automatic center.
    public int CenterMemberIndex = -1;
}