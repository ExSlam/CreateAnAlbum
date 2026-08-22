using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Albummodelite
{
    /// <summary>
    /// Preserves historical album lineups across registry timing and graduation. Live idols are
    /// preferred; when a NORMAL idol has left data_girls, a detached portrait-only shell is rebuilt
    /// from exact saved texture asset IDs. This intentionally stores no rendered cover/portrait PNGs.
    /// </summary>
    internal static class AlbumMemberRepair
    {
        internal static List<AlbumMemberSnapshot> CaptureAndGetSnapshots(AlbumData album)
        {
            List<AlbumMemberSnapshot> existing = album != null && album.MemberSnapshots != null
                ? album.MemberSnapshots.Where(snapshot => snapshot != null).ToList()
                : new List<AlbumMemberSnapshot>();
            if (album == null)
                return existing;

            Dictionary<int, data_girls.girls> liveById = new Dictionary<int, data_girls.girls>();
            if (album.Members != null)
            {
                foreach (data_girls.girls girl in album.Members)
                {
                    if (girl != null && !liveById.ContainsKey(girl.id))
                        liveById.Add(girl.id, girl);
                }
            }

            List<AlbumMemberSnapshot> ordered = new List<AlbumMemberSnapshot>();
            HashSet<int> seen = new HashSet<int>();

            // Preserve historical ordering. Refresh descriptors in place when the live idol is
            // still available, but never reorder the lineup just because one member graduated.
            foreach (AlbumMemberSnapshot oldSnapshot in existing)
            {
                if (oldSnapshot == null || !seen.Add(oldSnapshot.GirlId))
                    continue;
                data_girls.girls live;
                AlbumMemberSnapshot refreshed = liveById.TryGetValue(oldSnapshot.GirlId, out live)
                    ? Capture(live)
                    : oldSnapshot;
                ordered.Add(refreshed ?? oldSnapshot);
            }

            // Newly-created albums have no descriptors yet. Also append genuinely new live
            // members for compatibility with runtime/editor mutations.
            if (album.Members != null)
            {
                foreach (data_girls.girls live in album.Members)
                {
                    if (live == null || !seen.Add(live.id))
                        continue;
                    AlbumMemberSnapshot snapshot = Capture(live);
                    if (snapshot != null)
                        ordered.Add(snapshot);
                }
            }

            album.MemberSnapshots = ordered;
            return ordered;
        }

        internal static List<AlbumMemberSnapshot> CaptureMembers(IEnumerable<data_girls.girls> members)
        {
            List<AlbumMemberSnapshot> result = new List<AlbumMemberSnapshot>();
            HashSet<int> seen = new HashSet<int>();
            if (members == null)
                return result;

            foreach (data_girls.girls member in members)
            {
                if (member == null || !seen.Add(member.id))
                    continue;
                AlbumMemberSnapshot snapshot = Capture(member);
                if (snapshot != null)
                    result.Add(snapshot);
            }
            return result;
        }

        internal static void RememberLegacyIds(AlbumData album, IEnumerable<int> ids)
        {
            if (album == null || ids == null)
                return;
            if (album.MemberSnapshots == null)
                album.MemberSnapshots = new List<AlbumMemberSnapshot>();

            foreach (int id in ids)
            {
                if (album.MemberSnapshots.Any(s => s != null && s.GirlId == id))
                    continue;
                album.MemberSnapshots.Add(new AlbumMemberSnapshot { GirlId = id });
            }
        }

        internal static int Repair(AlbumData album)
        {
            if (album == null)
                return 0;

            if (album.MemberSnapshots == null || album.MemberSnapshots.Count == 0)
                CaptureAndGetSnapshots(album);
            if (album.MemberSnapshots == null || album.MemberSnapshots.Count == 0)
                return album.Members != null ? album.Members.Count : 0;

            // Gameplay-facing Members contains only real live registry objects. Archived
            // portrait shells are produced separately for cover rendering so fan/reward code
            // can never mutate a detached graduated idol. A legacy ID-only descriptor is
            // upgraded once a live idol becomes available, rather than recapturing every frame.
            List<data_girls.girls> restoredLive = new List<data_girls.girls>();
            for (int i = 0; i < album.MemberSnapshots.Count; i++)
            {
                AlbumMemberSnapshot snapshot = album.MemberSnapshots[i];
                if (snapshot == null)
                    continue;
                data_girls.girls live = FindLiveGirl(snapshot.GirlId);
                if (live == null &&
                    (snapshot.PortraitAssets == null || snapshot.PortraitAssets.Count == 0))
                {
                    AlbumMemberSnapshot archived;
                    if (GraduationDetailsIntegration.TryGetMemberSnapshot(snapshot.GirlId, out archived) &&
                        archived != null)
                    {
                        snapshot = archived;
                        album.MemberSnapshots[i] = archived;
                    }
                }
                if (live != null)
                {
                    if (snapshot.PortraitAssets == null || snapshot.PortraitAssets.Count == 0)
                    {
                        AlbumMemberSnapshot refreshed = Capture(live);
                        if (refreshed != null)
                        {
                            snapshot = refreshed;
                            album.MemberSnapshots[i] = refreshed;
                        }
                    }
                    if (restoredLive.All(existing => existing == null || existing.id != live.id))
                        restoredLive.Add(live);
                }
            }
            album.Members = restoredLive;
            RepairCenter(album);
            return restoredLive.Count;
        }

        internal static List<data_girls.girls> GetRenderMembers(AlbumData album)
        {
            List<data_girls.girls> result = new List<data_girls.girls>();
            if (album == null)
                return result;

            CaptureAndGetSnapshots(album);
            if (album.MemberSnapshots == null || album.MemberSnapshots.Count == 0)
                return album.Members != null
                    ? album.Members.Where(member => member != null).Take(8).ToList()
                    : result;

            for (int i = 0; i < album.MemberSnapshots.Count; i++)
            {
                AlbumMemberSnapshot snapshot = album.MemberSnapshots[i];
                if (snapshot == null)
                    continue;
                data_girls.girls girl = FindLiveGirl(snapshot.GirlId);
                if (girl == null &&
                    (snapshot.PortraitAssets == null || snapshot.PortraitAssets.Count == 0))
                {
                    AlbumMemberSnapshot archived;
                    if (GraduationDetailsIntegration.TryGetMemberSnapshot(snapshot.GirlId, out archived) &&
                        archived != null)
                    {
                        snapshot = archived;
                        album.MemberSnapshots[i] = archived;
                    }
                }
                if (girl == null)
                    TryCreateDetachedPortraitGirl(snapshot, out girl);
                if (girl != null)
                    result.Add(girl);
                if (result.Count >= 8)
                    break;
            }
            return result;
        }

        internal static void RepairAll()
        {
            if (Albums.AlbumList == null)
                return;
            foreach (AlbumData album in Albums.AlbumList)
                Repair(album);
        }

        internal static int GetHistoricalMemberCount(AlbumData album)
        {
            if (album == null)
                return 0;
            int live = album.Members != null ? album.Members.Count : 0;
            int historical = album.MemberSnapshots != null ? album.MemberSnapshots.Count : 0;
            return Math.Max(live, historical);
        }

        private static AlbumMemberSnapshot Capture(data_girls.girls girl)
        {
            if (girl == null)
                return null;

            AlbumMemberSnapshot snapshot = new AlbumMemberSnapshot
            {
                GirlId = girl.id,
                FirstName = girl.firstName ?? string.Empty,
                LastName = girl.lastName ?? string.Empty,
                Nickname = girl.nickname ?? string.Empty,
                IdolType = girl.Type,
                CustomId = girl.Type == data_girls.girls._type.NORMAL ? string.Empty : (girl.GetCustomID() ?? string.Empty),
                CustomSpriteAddress = girl.Type == data_girls.girls._type.NORMAL ? string.Empty : (girl.GetCustomSpriteAddress() ?? string.Empty),
                PortraitAssets = new List<AlbumPortraitAssetReference>()
            };

            if (girl.textureAssets != null)
            {
                foreach (data_girls.girls._textureAsset textureAsset in girl.textureAssets)
                {
                    if (textureAsset == null || textureAsset.asset == null)
                        continue;
                    string assetId = textureAsset.asset.GetID();
                    if (string.IsNullOrEmpty(assetId))
                        continue;
                    snapshot.PortraitAssets.Add(new AlbumPortraitAssetReference
                    {
                        Type = textureAsset.type,
                        AssetId = assetId
                    });
                }
            }
            return snapshot;
        }

        private static data_girls.girls FindLiveGirl(int id)
        {
            try
            {
                if (id <= -1000000)
                    return RivalsRebornIntegration.TryGetDisplayGirlById(id);
                return data_girls.GetGirlByID(id);
            }
            catch
            {
                return null;
            }
        }

        private static bool TryCreateDetachedPortraitGirl(
            AlbumMemberSnapshot snapshot,
            out data_girls.girls girl)
        {
            girl = null;
            if (snapshot == null || snapshot.IdolType != data_girls.girls._type.NORMAL ||
                snapshot.PortraitAssets == null || snapshot.PortraitAssets.Count == 0)
                return false;

            Dictionary<data_girls_textures._spriteType, data_girls_textures._textureAsset> resolvedByType =
                new Dictionary<data_girls_textures._spriteType, data_girls_textures._textureAsset>();
            foreach (AlbumPortraitAssetReference reference in snapshot.PortraitAssets)
            {
                if (reference == null || string.IsNullOrEmpty(reference.AssetId) ||
                    reference.Type == data_girls_textures._spriteType.NONE ||
                    resolvedByType.ContainsKey(reference.Type))
                    return false;

                data_girls_textures._textureAsset asset =
                    data_girls_textures.GetTextureAssetByID(reference.AssetId, reference.Type);
                if (asset == null || asset.type != reference.Type ||
                    !string.Equals(asset.GetID(), reference.AssetId, StringComparison.Ordinal))
                    return false;
                resolvedByType.Add(reference.Type, asset);
            }

            if (!resolvedByType.ContainsKey(data_girls_textures._spriteType.body))
                return false;

            List<data_girls.girls._textureAsset> textureAssets = new List<data_girls.girls._textureAsset>();
            AddPart(textureAssets, resolvedByType, data_girls_textures._spriteType.body);
            AddPart(textureAssets, resolvedByType, data_girls_textures._spriteType.hair);
            AddPart(textureAssets, resolvedByType, data_girls_textures._spriteType.face);
            AddPart(textureAssets, resolvedByType, data_girls_textures._spriteType.acc);

            try
            {
                data_girls.girls detached = new data_girls.girls
                {
                    id = snapshot.GirlId,
                    firstName = snapshot.FirstName ?? string.Empty,
                    lastName = snapshot.LastName ?? string.Empty,
                    nickname = snapshot.Nickname ?? string.Empty,
                    Type = data_girls.girls._type.NORMAL,
                    textureAssets = textureAssets
                };
                detached.UpdateTextureData();
                girl = detached;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[AlbumMemberRepair] Could not rebuild archived portrait for idol " +
                    snapshot.GirlId + ": " + ex.Message);
                return false;
            }
        }

        private static void AddPart(
            List<data_girls.girls._textureAsset> destination,
            Dictionary<data_girls_textures._spriteType, data_girls_textures._textureAsset> source,
            data_girls_textures._spriteType type)
        {
            data_girls_textures._textureAsset asset;
            if (source.TryGetValue(type, out asset) && asset != null)
            {
                destination.Add(new data_girls.girls._textureAsset { type = type, asset = asset });
            }
        }

        private static void RepairCenter(AlbumData album)
        {
            if (album == null || album.Members == null || !album.HasCenterMemberId)
                return;
            for (int i = 0; i < album.Members.Count; i++)
            {
                data_girls.girls girl = album.Members[i];
                if (girl != null && girl.id == album.CenterMemberId)
                {
                    album.CenterMemberIndex = i;
                    return;
                }
            }
            album.CenterMemberIndex = -1;
        }
    }
}
