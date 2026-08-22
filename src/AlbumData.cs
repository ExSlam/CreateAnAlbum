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

    // Historical portrait/member descriptors keep album covers renderable after an idol
    // graduates and leaves data_girls. Only vanilla/mod asset IDs are stored, never pixels.
    public List<AlbumMemberSnapshot> MemberSnapshots =
        new List<AlbumMemberSnapshot>();

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

    // 4.1+: stable release/font/background metadata. ReleaseKind uses CreateAnAlbumGroupRules.AlbumReleaseKind values.
    public int ReleaseKind;
    public string FontKey = "";
    public bool DebutFanRewardGranted;

    public int ThemeIndex;
    public int BackgroundIndex;
    public string BackgroundKey = "";
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

    // Legacy index into Members. -1 means automatic center. Kept for migration and
    // compatibility with older save documents. New runtime state also records the idol ID so
    // a temporarily missing member cannot make the center silently drift to another idol.
    public int CenterMemberIndex = -1;
    public int CenterMemberId = -1;
    public bool HasCenterMemberId;
}
[Serializable]
public class AlbumMemberSnapshot
{
    public int GirlId = -1;
    public string FirstName = "";
    public string LastName = "";
    public string Nickname = "";
    public data_girls.girls._type IdolType = data_girls.girls._type.NORMAL;
    public string CustomId = "";
    public string CustomSpriteAddress = "";
    public List<AlbumPortraitAssetReference> PortraitAssets =
        new List<AlbumPortraitAssetReference>();
}

[Serializable]
public class AlbumPortraitAssetReference
{
    public data_girls_textures._spriteType Type;
    public string AssetId = "";
}
