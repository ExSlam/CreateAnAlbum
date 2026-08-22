using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CreateAnAlbumGroupRules;

namespace Albummodelite
{
    /// <summary>
    /// CAA-specific codec for the supplemental album save envelope.
    ///
    /// This deliberately does not use UnityEngine.JsonUtility. In the actual Idol Manager
    /// runtime JsonUtility can emit the scalar header while silently omitting the album,
    /// production-project, checkpoint, and nested collection graph. That creates a syntactically
    /// valid header-only document which can then be mirrored into IM Data Core.
    ///
    /// The design follows IM Data Core's LightweightSidecarJson rules:
    /// - every durable collection/structural property is emitted explicitly, including empties;
    /// - duplicate JSON object properties are rejected;
    /// - a v4 document missing a required structural property is rejected rather than interpreted
    ///   as an empty campaign;
    /// - unknown properties are tolerated so additive changes remain readable;
    /// - all numeric formatting/parsing is invariant-culture and finite.
    /// </summary>
    internal static class AlbumSaveJson
    {
        private const int CurrentVersion = 4;

        private static readonly string[] V4RootFields =
        {
            "Version", "WrittenUtcTicks", "CheckpointBindingVersion",
            "VanillaCheckpoint", "LastChartProcessedTicks", "ProductionProject",
            "LegacyProductionMigrationCompleted", "Albums"
        };

        private static readonly string[] V4CheckpointFields =
        {
            "NormalizedSavePath", "LastSave", "PlaytimeSeconds",
            "GameDateTime", "ContentFingerprint"
        };

        private static readonly string[] V4ProductionProjectFields =
        {
            "SaveId", "Title", "GroupName", "GroupId", "ReleaseKind",
            "SongIds", "MemberIds", "MemberSnapshots", "CenterMemberId",
            "StartTicks", "Theme", "ThemeIndex", "BackgroundIndex",
            "BackgroundKey", "LayoutIndex", "FontIndex", "FontKey",
            "TextColorIndex", "TitlePosition", "ShowGroupName", "OrnamentStyle",
            "FrameStyle", "TitleEffect", "PortraitScale", "CenterEmphasis",
            "PortraitYOffset", "PortraitSpacing", "EffectsIntensity"
        };

        private static readonly string[] V4AlbumFields =
        {
            "ID", "Title", "GroupName", "ReleaseTicks", "Released",
            "PlayerAlbum", "RivalName", "RivalGroupId", "MemberIds",
            "MemberSnapshots", "SongIds", "Sales", "WeeklySales", "Profit",
            "ChartPosition", "PreviousChartPosition", "PeakChartPosition",
            "WeeksOnChart", "CoverPath", "Theme", "ThemeIndex",
            "BackgroundIndex", "BackgroundKey", "LayoutIndex", "FontIndex",
            "FontKey", "ReleaseKind", "DebutFanRewardGranted", "TextColorIndex",
            "TitlePosition", "ShowGroupName", "OrnamentStyle", "FrameStyle",
            "TitleEffect", "PortraitScale", "CenterEmphasis", "PortraitYOffset",
            "PortraitSpacing", "EffectsIntensity", "CenterMemberIndex",
            "CenterMemberId", "HasCenterMemberId"
        };

        private static readonly string[] V4MemberSnapshotFields =
        {
            "GirlId", "FirstName", "LastName", "Nickname", "IdolType",
            "CustomId", "CustomSpriteAddress", "PortraitAssets"
        };

        private static readonly string[] V4PortraitAssetFields =
        {
            "Type", "AssetId"
        };

        internal static string Serialize(AlbumPersistence.AlbumSaveFile document)
        {
            ValidateSerializableDocument(document);

            StringBuilder builder = new StringBuilder(4096);
            builder.Append('{');

            AppendPropertyName(builder, "Version");
            AppendInt32(builder, document.Version);
            builder.Append(',');
            AppendPropertyName(builder, "WrittenUtcTicks");
            AppendInt64(builder, document.WrittenUtcTicks);
            builder.Append(',');
            AppendPropertyName(builder, "CheckpointBindingVersion");
            AppendInt32(builder, document.CheckpointBindingVersion);
            builder.Append(',');
            AppendPropertyName(builder, "VanillaCheckpoint");
            AppendCheckpoint(builder, document.VanillaCheckpoint);
            builder.Append(',');
            AppendPropertyName(builder, "LastChartProcessedTicks");
            AppendInt64(builder, document.LastChartProcessedTicks);
            builder.Append(',');
            AppendPropertyName(builder, "ProductionProject");
            AppendProductionProject(builder, document.ProductionProject);
            builder.Append(',');
            AppendPropertyName(builder, "LegacyProductionMigrationCompleted");
            AppendBoolean(builder, document.LegacyProductionMigrationCompleted);
            builder.Append(',');
            AppendPropertyName(builder, "Albums");
            AppendAlbums(builder, document.Albums);

            builder.Append('}');
            return builder.ToString();
        }

        /// <summary>
        /// Recognizes the exact scalar-only document shape produced by CAA 4.2.0 when
        /// Unity JsonUtility silently omitted every complex field. This is intentionally
        /// narrower than normal deserialization: it exists only so a known-unrecoverable
        /// header can be quarantined and replaced on the next real save rather than causing
        /// permanent write blocking. It never fabricates albums or a production project.
        /// </summary>
        internal static bool TryReadKnownJsonUtilityHeaderOnly(
            string json,
            out AlbumPersistence.AlbumSaveFile metadata)
        {
            metadata = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                JsonValue rootValue = new JsonParser(json).ParseDocument();
                Dictionary<string, JsonValue> root = RequireObject(
                    rootValue,
                    "The CAA album save document must be a JSON object.");

                string[] exactFields =
                {
                    "Version",
                    "WrittenUtcTicks",
                    "CheckpointBindingVersion",
                    "LastChartProcessedTicks",
                    "LegacyProductionMigrationCompleted"
                };

                if (root.Count != exactFields.Length)
                    return false;
                for (int i = 0; i < exactFields.Length; i++)
                {
                    if (!root.ContainsKey(exactFields[i]))
                        return false;
                }

                int version = RequireInt32(root, "Version");
                if (version != 4)
                    return false;

                AlbumPersistence.AlbumSaveFile recovered =
                    new AlbumPersistence.AlbumSaveFile();
                recovered.Version = version;
                recovered.WrittenUtcTicks = RequireInt64(root, "WrittenUtcTicks");
                recovered.CheckpointBindingVersion =
                    RequireInt32(root, "CheckpointBindingVersion");
                recovered.LastChartProcessedTicks =
                    RequireInt64(root, "LastChartProcessedTicks");
                recovered.LegacyProductionMigrationCompleted =
                    RequireBoolean(root, "LegacyProductionMigrationCompleted");
                recovered.VanillaCheckpoint = null;
                recovered.ProductionProject = null;
                recovered.Albums = new List<AlbumPersistence.AlbumSaveEntry>();
                metadata = recovered;
                return true;
            }
            catch
            {
                metadata = null;
                return false;
            }
        }

        internal static AlbumPersistence.AlbumSaveFile Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidDataException("The CAA album save document is empty.");

            JsonValue rootValue = new JsonParser(json).ParseDocument();
            Dictionary<string, JsonValue> root = RequireObject(
                rootValue,
                "The CAA album save document must be a JSON object.");

            int version = RequireInt32(root, "Version");
            if (version <= 0 || version > CurrentVersion)
            {
                throw new InvalidDataException(
                    "The CAA album save schema version " + version + " is unsupported.");
            }

            // Albums is required for every recoverable CAA envelope. Treating a missing Albums
            // property as an empty list would reproduce the exact header-only data-loss bug this
            // codec exists to prevent.
            RequireProperty(root, "Albums");

            if (version >= 4)
            {
                // Schema v4 is the first schema emitted by this explicit codec. Require the whole
                // structural envelope so JsonUtility-style scalar-only output is never accepted.
                RequireProperties(root, V4RootFields, "CAA v4 root");
            }

            AlbumPersistence.AlbumSaveFile document =
                new AlbumPersistence.AlbumSaveFile();
            document.Version = version;
            document.WrittenUtcTicks = ReadInt64(root, "WrittenUtcTicks", 0L);
            document.CheckpointBindingVersion =
                ReadInt32(root, "CheckpointBindingVersion", 0);
            document.VanillaCheckpoint = ReadCheckpoint(root, "VanillaCheckpoint", version);
            document.LastChartProcessedTicks =
                ReadInt64(root, "LastChartProcessedTicks", 0L);
            document.ProductionProject =
                ReadProductionProject(root, "ProductionProject", version);
            document.LegacyProductionMigrationCompleted =
                ReadBoolean(root, "LegacyProductionMigrationCompleted", false);
            document.Albums = ReadAlbums(root, "Albums", version);
            return document;
        }

        private static void ValidateSerializableDocument(
            AlbumPersistence.AlbumSaveFile document)
        {
            if (document == null)
                throw new ArgumentNullException("document");
            if (document.Version <= 0 || document.Version > CurrentVersion)
                throw new InvalidDataException("The CAA album save schema version is invalid.");
            if (document.Albums == null)
                throw new InvalidDataException("The CAA Albums collection is null.");

            ValidateProductionProject(document.ProductionProject);
            for (int i = 0; i < document.Albums.Count; i++)
            {
                AlbumPersistence.AlbumSaveEntry entry = document.Albums[i];
                if (entry == null)
                    throw new InvalidDataException("CAA Albums contains a null entry at index " + i + ".");
                if (entry.MemberIds == null)
                    throw new InvalidDataException("CAA album MemberIds is null at index " + i + ".");
                if (entry.MemberSnapshots == null)
                    throw new InvalidDataException("CAA album MemberSnapshots is null at index " + i + ".");
                if (entry.SongIds == null)
                    throw new InvalidDataException("CAA album SongIds is null at index " + i + ".");
                ValidateMemberSnapshots(entry.MemberSnapshots, "CAA album " + i);
                ValidateFinite(entry.PortraitScale, "PortraitScale");
                ValidateFinite(entry.CenterEmphasis, "CenterEmphasis");
                ValidateFinite(entry.PortraitYOffset, "PortraitYOffset");
                ValidateFinite(entry.PortraitSpacing, "PortraitSpacing");
                ValidateFinite(entry.EffectsIntensity, "EffectsIntensity");
            }
        }

        private static void ValidateProductionProject(AlbumProductionProject project)
        {
            if (project == null)
                return;
            if (project.SongIds == null)
                throw new InvalidDataException("CAA production-project SongIds is null.");
            if (project.MemberIds == null)
                throw new InvalidDataException("CAA production-project MemberIds is null.");
            if (project.MemberSnapshots == null)
                throw new InvalidDataException("CAA production-project MemberSnapshots is null.");
            ValidateMemberSnapshots(project.MemberSnapshots, "CAA production project");
            ValidateFinite(project.PortraitScale, "PortraitScale");
            ValidateFinite(project.CenterEmphasis, "CenterEmphasis");
            ValidateFinite(project.PortraitYOffset, "PortraitYOffset");
            ValidateFinite(project.PortraitSpacing, "PortraitSpacing");
            ValidateFinite(project.EffectsIntensity, "EffectsIntensity");
        }

        private static void ValidateMemberSnapshots(
            List<AlbumMemberSnapshot> snapshots,
            string owner)
        {
            for (int i = 0; i < snapshots.Count; i++)
            {
                AlbumMemberSnapshot snapshot = snapshots[i];
                if (snapshot == null)
                    throw new InvalidDataException(owner + " has a null MemberSnapshot at index " + i + ".");
                if (snapshot.PortraitAssets == null)
                    throw new InvalidDataException(owner + " has a null PortraitAssets collection at index " + i + ".");
                for (int j = 0; j < snapshot.PortraitAssets.Count; j++)
                {
                    if (snapshot.PortraitAssets[j] == null)
                        throw new InvalidDataException(owner + " has a null portrait asset at index " + j + ".");
                }
            }
        }

        private static void ValidateFinite(float value, string fieldName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new InvalidDataException("CAA field '" + fieldName + "' is not a finite number.");
        }

        private static void AppendCheckpoint(
            StringBuilder builder,
            AlbumVanillaCheckpointStamp checkpoint)
        {
            if (checkpoint == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append('{');
            AppendPropertyName(builder, "NormalizedSavePath");
            AppendString(builder, checkpoint.NormalizedSavePath ?? string.Empty);
            builder.Append(',');
            AppendPropertyName(builder, "LastSave");
            AppendString(builder, checkpoint.LastSave ?? string.Empty);
            builder.Append(',');
            AppendPropertyName(builder, "PlaytimeSeconds");
            AppendInt64(builder, checkpoint.PlaytimeSeconds);
            builder.Append(',');
            AppendPropertyName(builder, "GameDateTime");
            AppendString(builder, checkpoint.GameDateTime ?? string.Empty);
            builder.Append(',');
            AppendPropertyName(builder, "ContentFingerprint");
            AppendString(builder, checkpoint.ContentFingerprint ?? string.Empty);
            builder.Append('}');
        }

        private static void AppendProductionProject(
            StringBuilder builder,
            AlbumProductionProject project)
        {
            if (project == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append('{');
            AppendPropertyName(builder, "SaveId");
            AppendString(builder, project.SaveId ?? string.Empty);
            builder.Append(',');
            AppendPropertyName(builder, "Title");
            AppendString(builder, project.Title ?? string.Empty);
            builder.Append(',');
            AppendPropertyName(builder, "GroupName");
            AppendString(builder, project.GroupName ?? string.Empty);
            builder.Append(',');
            AppendPropertyName(builder, "GroupId");
            AppendInt32(builder, project.GroupId);
            builder.Append(',');
            AppendPropertyName(builder, "ReleaseKind");
            AppendInt32(builder, project.ReleaseKind);
            builder.Append(',');
            AppendPropertyName(builder, "SongIds");
            AppendInt32Array(builder, project.SongIds);
            builder.Append(',');
            AppendPropertyName(builder, "MemberIds");
            AppendInt32Array(builder, project.MemberIds);
            builder.Append(',');
            AppendPropertyName(builder, "MemberSnapshots");
            AppendMemberSnapshots(builder, project.MemberSnapshots);
            builder.Append(',');
            AppendPropertyName(builder, "CenterMemberId");
            AppendInt32(builder, project.CenterMemberId);
            builder.Append(',');
            AppendPropertyName(builder, "StartTicks");
            AppendInt64(builder, project.StartTicks);
            builder.Append(',');
            AppendPropertyName(builder, "Theme");
            AppendString(builder, project.Theme ?? string.Empty);
            builder.Append(',');
            AppendPropertyName(builder, "ThemeIndex");
            AppendInt32(builder, project.ThemeIndex);
            builder.Append(',');
            AppendPropertyName(builder, "BackgroundIndex");
            AppendInt32(builder, project.BackgroundIndex);
            builder.Append(',');
            AppendPropertyName(builder, "BackgroundKey");
            AppendString(builder, project.BackgroundKey ?? string.Empty);
            builder.Append(',');
            AppendPropertyName(builder, "LayoutIndex");
            AppendInt32(builder, project.LayoutIndex);
            builder.Append(',');
            AppendPropertyName(builder, "FontIndex");
            AppendInt32(builder, project.FontIndex);
            builder.Append(',');
            AppendPropertyName(builder, "FontKey");
            AppendString(builder, project.FontKey ?? string.Empty);
            builder.Append(',');
            AppendPropertyName(builder, "TextColorIndex");
            AppendInt32(builder, project.TextColorIndex);
            builder.Append(',');
            AppendPropertyName(builder, "TitlePosition");
            AppendInt32(builder, project.TitlePosition);
            builder.Append(',');
            AppendPropertyName(builder, "ShowGroupName");
            AppendBoolean(builder, project.ShowGroupName);
            builder.Append(',');
            AppendPropertyName(builder, "OrnamentStyle");
            AppendInt32(builder, project.OrnamentStyle);
            builder.Append(',');
            AppendPropertyName(builder, "FrameStyle");
            AppendInt32(builder, project.FrameStyle);
            builder.Append(',');
            AppendPropertyName(builder, "TitleEffect");
            AppendInt32(builder, project.TitleEffect);
            builder.Append(',');
            AppendPropertyName(builder, "PortraitScale");
            AppendSingle(builder, project.PortraitScale);
            builder.Append(',');
            AppendPropertyName(builder, "CenterEmphasis");
            AppendSingle(builder, project.CenterEmphasis);
            builder.Append(',');
            AppendPropertyName(builder, "PortraitYOffset");
            AppendSingle(builder, project.PortraitYOffset);
            builder.Append(',');
            AppendPropertyName(builder, "PortraitSpacing");
            AppendSingle(builder, project.PortraitSpacing);
            builder.Append(',');
            AppendPropertyName(builder, "EffectsIntensity");
            AppendSingle(builder, project.EffectsIntensity);
            builder.Append('}');
        }

        private static void AppendAlbums(
            StringBuilder builder,
            List<AlbumPersistence.AlbumSaveEntry> albums)
        {
            builder.Append('[');
            for (int i = 0; i < albums.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                AppendAlbum(builder, albums[i]);
            }
            builder.Append(']');
        }

        private static void AppendAlbum(
            StringBuilder builder,
            AlbumPersistence.AlbumSaveEntry entry)
        {
            builder.Append('{');
            AppendPropertyName(builder, "ID"); AppendInt32(builder, entry.ID); builder.Append(',');
            AppendPropertyName(builder, "Title"); AppendString(builder, entry.Title ?? string.Empty); builder.Append(',');
            AppendPropertyName(builder, "GroupName"); AppendString(builder, entry.GroupName ?? string.Empty); builder.Append(',');
            AppendPropertyName(builder, "ReleaseTicks"); AppendInt64(builder, entry.ReleaseTicks); builder.Append(',');
            AppendPropertyName(builder, "Released"); AppendBoolean(builder, entry.Released); builder.Append(',');
            AppendPropertyName(builder, "PlayerAlbum"); AppendBoolean(builder, entry.PlayerAlbum); builder.Append(',');
            AppendPropertyName(builder, "RivalName"); AppendString(builder, entry.RivalName ?? string.Empty); builder.Append(',');
            AppendPropertyName(builder, "RivalGroupId"); AppendInt32(builder, entry.RivalGroupId); builder.Append(',');
            AppendPropertyName(builder, "MemberIds"); AppendInt32Array(builder, entry.MemberIds); builder.Append(',');
            AppendPropertyName(builder, "MemberSnapshots"); AppendMemberSnapshots(builder, entry.MemberSnapshots); builder.Append(',');
            AppendPropertyName(builder, "SongIds"); AppendInt32Array(builder, entry.SongIds); builder.Append(',');
            AppendPropertyName(builder, "Sales"); AppendInt64(builder, entry.Sales); builder.Append(',');
            AppendPropertyName(builder, "WeeklySales"); AppendInt64(builder, entry.WeeklySales); builder.Append(',');
            AppendPropertyName(builder, "Profit"); AppendInt64(builder, entry.Profit); builder.Append(',');
            AppendPropertyName(builder, "ChartPosition"); AppendInt32(builder, entry.ChartPosition); builder.Append(',');
            AppendPropertyName(builder, "PreviousChartPosition"); AppendInt32(builder, entry.PreviousChartPosition); builder.Append(',');
            AppendPropertyName(builder, "PeakChartPosition"); AppendInt32(builder, entry.PeakChartPosition); builder.Append(',');
            AppendPropertyName(builder, "WeeksOnChart"); AppendInt32(builder, entry.WeeksOnChart); builder.Append(',');
            AppendPropertyName(builder, "CoverPath"); AppendString(builder, entry.CoverPath ?? string.Empty); builder.Append(',');
            AppendPropertyName(builder, "Theme"); AppendString(builder, entry.Theme ?? string.Empty); builder.Append(',');
            AppendPropertyName(builder, "ThemeIndex"); AppendInt32(builder, entry.ThemeIndex); builder.Append(',');
            AppendPropertyName(builder, "BackgroundIndex"); AppendInt32(builder, entry.BackgroundIndex); builder.Append(',');
            AppendPropertyName(builder, "BackgroundKey"); AppendString(builder, entry.BackgroundKey ?? string.Empty); builder.Append(',');
            AppendPropertyName(builder, "LayoutIndex"); AppendInt32(builder, entry.LayoutIndex); builder.Append(',');
            AppendPropertyName(builder, "FontIndex"); AppendInt32(builder, entry.FontIndex); builder.Append(',');
            AppendPropertyName(builder, "FontKey"); AppendString(builder, entry.FontKey ?? string.Empty); builder.Append(',');
            AppendPropertyName(builder, "ReleaseKind"); AppendInt32(builder, entry.ReleaseKind); builder.Append(',');
            AppendPropertyName(builder, "DebutFanRewardGranted"); AppendBoolean(builder, entry.DebutFanRewardGranted); builder.Append(',');
            AppendPropertyName(builder, "TextColorIndex"); AppendInt32(builder, entry.TextColorIndex); builder.Append(',');
            AppendPropertyName(builder, "TitlePosition"); AppendInt32(builder, entry.TitlePosition); builder.Append(',');
            AppendPropertyName(builder, "ShowGroupName"); AppendBoolean(builder, entry.ShowGroupName); builder.Append(',');
            AppendPropertyName(builder, "OrnamentStyle"); AppendInt32(builder, entry.OrnamentStyle); builder.Append(',');
            AppendPropertyName(builder, "FrameStyle"); AppendInt32(builder, entry.FrameStyle); builder.Append(',');
            AppendPropertyName(builder, "TitleEffect"); AppendInt32(builder, entry.TitleEffect); builder.Append(',');
            AppendPropertyName(builder, "PortraitScale"); AppendSingle(builder, entry.PortraitScale); builder.Append(',');
            AppendPropertyName(builder, "CenterEmphasis"); AppendSingle(builder, entry.CenterEmphasis); builder.Append(',');
            AppendPropertyName(builder, "PortraitYOffset"); AppendSingle(builder, entry.PortraitYOffset); builder.Append(',');
            AppendPropertyName(builder, "PortraitSpacing"); AppendSingle(builder, entry.PortraitSpacing); builder.Append(',');
            AppendPropertyName(builder, "EffectsIntensity"); AppendSingle(builder, entry.EffectsIntensity); builder.Append(',');
            AppendPropertyName(builder, "CenterMemberIndex"); AppendInt32(builder, entry.CenterMemberIndex); builder.Append(',');
            AppendPropertyName(builder, "CenterMemberId"); AppendInt32(builder, entry.CenterMemberId); builder.Append(',');
            AppendPropertyName(builder, "HasCenterMemberId"); AppendBoolean(builder, entry.HasCenterMemberId);
            builder.Append('}');
        }

        private static void AppendMemberSnapshots(
            StringBuilder builder,
            List<AlbumMemberSnapshot> snapshots)
        {
            builder.Append('[');
            for (int i = 0; i < snapshots.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                AlbumMemberSnapshot snapshot = snapshots[i];
                builder.Append('{');
                AppendPropertyName(builder, "GirlId"); AppendInt32(builder, snapshot.GirlId); builder.Append(',');
                AppendPropertyName(builder, "FirstName"); AppendString(builder, snapshot.FirstName ?? string.Empty); builder.Append(',');
                AppendPropertyName(builder, "LastName"); AppendString(builder, snapshot.LastName ?? string.Empty); builder.Append(',');
                AppendPropertyName(builder, "Nickname"); AppendString(builder, snapshot.Nickname ?? string.Empty); builder.Append(',');
                AppendPropertyName(builder, "IdolType"); AppendInt32(builder, (int)snapshot.IdolType); builder.Append(',');
                AppendPropertyName(builder, "CustomId"); AppendString(builder, snapshot.CustomId ?? string.Empty); builder.Append(',');
                AppendPropertyName(builder, "CustomSpriteAddress"); AppendString(builder, snapshot.CustomSpriteAddress ?? string.Empty); builder.Append(',');
                AppendPropertyName(builder, "PortraitAssets"); AppendPortraitAssets(builder, snapshot.PortraitAssets);
                builder.Append('}');
            }
            builder.Append(']');
        }

        private static void AppendPortraitAssets(
            StringBuilder builder,
            List<AlbumPortraitAssetReference> assets)
        {
            builder.Append('[');
            for (int i = 0; i < assets.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                AlbumPortraitAssetReference asset = assets[i];
                builder.Append('{');
                AppendPropertyName(builder, "Type"); AppendInt32(builder, (int)asset.Type); builder.Append(',');
                AppendPropertyName(builder, "AssetId"); AppendString(builder, asset.AssetId ?? string.Empty);
                builder.Append('}');
            }
            builder.Append(']');
        }

        private static void AppendInt32Array(StringBuilder builder, List<int> values)
        {
            builder.Append('[');
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');
                AppendInt32(builder, values[i]);
            }
            builder.Append(']');
        }

        private static AlbumVanillaCheckpointStamp ReadCheckpoint(
            Dictionary<string, JsonValue> owner,
            string propertyName,
            int version)
        {
            JsonValue value;
            if (!owner.TryGetValue(propertyName, out value) || value == null || value.Kind == JsonValueKind.Null)
                return null;

            Dictionary<string, JsonValue> item = RequireObject(
                value,
                "CAA field '" + propertyName + "' must be an object or null.");
            if (version >= 4)
                RequireProperties(item, V4CheckpointFields, "CAA v4 checkpoint");
            return new AlbumVanillaCheckpointStamp
            {
                NormalizedSavePath = ReadString(item, "NormalizedSavePath", string.Empty),
                LastSave = ReadString(item, "LastSave", string.Empty),
                PlaytimeSeconds = ReadInt64(item, "PlaytimeSeconds", 0L),
                GameDateTime = ReadString(item, "GameDateTime", string.Empty),
                ContentFingerprint = ReadString(item, "ContentFingerprint", string.Empty)
            };
        }

        private static AlbumProductionProject ReadProductionProject(
            Dictionary<string, JsonValue> owner,
            string propertyName,
            int version)
        {
            JsonValue value;
            if (!owner.TryGetValue(propertyName, out value) || value == null || value.Kind == JsonValueKind.Null)
                return null;

            Dictionary<string, JsonValue> item = RequireObject(
                value,
                "CAA field '" + propertyName + "' must be an object or null.");

            if (version >= 4)
                RequireProperties(item, V4ProductionProjectFields, "CAA v4 production project");

            AlbumProductionProject project = new AlbumProductionProject();
            project.SaveId = ReadString(item, "SaveId", string.Empty);
            project.Title = ReadString(item, "Title", string.Empty);
            project.GroupName = ReadString(item, "GroupName", string.Empty);
            project.GroupId = ReadInt32(item, "GroupId", -1);
            project.ReleaseKind = ReadInt32(item, "ReleaseKind", 0);
            project.SongIds = ReadInt32List(item, "SongIds", version >= 4);
            project.MemberIds = ReadInt32List(item, "MemberIds", version >= 4);
            project.MemberSnapshots = ReadMemberSnapshots(item, "MemberSnapshots", version >= 4);
            project.CenterMemberId = ReadInt32(item, "CenterMemberId", -1);
            project.StartTicks = ReadInt64(item, "StartTicks", 0L);
            project.Theme = ReadString(item, "Theme", string.Empty);
            project.ThemeIndex = ReadInt32(item, "ThemeIndex", 0);
            project.BackgroundIndex = ReadInt32(item, "BackgroundIndex", 0);
            project.BackgroundKey = ReadString(item, "BackgroundKey", string.Empty);
            project.LayoutIndex = ReadInt32(item, "LayoutIndex", 0);
            project.FontIndex = ReadInt32(item, "FontIndex", 0);
            project.FontKey = ReadString(item, "FontKey", string.Empty);
            project.TextColorIndex = ReadInt32(item, "TextColorIndex", 0);
            project.TitlePosition = ReadInt32(item, "TitlePosition", 0);
            project.ShowGroupName = ReadBoolean(item, "ShowGroupName", false);
            project.OrnamentStyle = ReadInt32(item, "OrnamentStyle", 0);
            project.FrameStyle = ReadInt32(item, "FrameStyle", 0);
            project.TitleEffect = ReadInt32(item, "TitleEffect", 0);
            project.PortraitScale = ReadSingle(item, "PortraitScale", 1f);
            project.CenterEmphasis = ReadSingle(item, "CenterEmphasis", 1.08f);
            project.PortraitYOffset = ReadSingle(item, "PortraitYOffset", 0f);
            project.PortraitSpacing = ReadSingle(item, "PortraitSpacing", 1f);
            project.EffectsIntensity = ReadSingle(item, "EffectsIntensity", 1f);
            return project;
        }

        private static List<AlbumPersistence.AlbumSaveEntry> ReadAlbums(
            Dictionary<string, JsonValue> owner,
            string propertyName,
            int version)
        {
            JsonValue value;
            if (!owner.TryGetValue(propertyName, out value))
                throw new InvalidDataException("The CAA album save document is missing required field 'Albums'.");
            List<JsonValue> values = RequireArray(
                value,
                "CAA field 'Albums' must be a JSON array.");

            List<AlbumPersistence.AlbumSaveEntry> result =
                new List<AlbumPersistence.AlbumSaveEntry>(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                Dictionary<string, JsonValue> item = RequireObject(
                    values[i],
                    "CAA Albums[" + i + "] must be a JSON object.");

                if (version >= 4)
                    RequireProperties(item, V4AlbumFields, "CAA v4 album entry " + i);

                AlbumPersistence.AlbumSaveEntry entry =
                    new AlbumPersistence.AlbumSaveEntry();
                entry.ID = ReadInt32(item, "ID", 0);
                entry.Title = ReadString(item, "Title", string.Empty);
                entry.GroupName = ReadString(item, "GroupName", string.Empty);
                entry.ReleaseTicks = ReadInt64(item, "ReleaseTicks", 0L);
                entry.Released = ReadBoolean(item, "Released", false);
                entry.PlayerAlbum = ReadBoolean(item, "PlayerAlbum", false);
                entry.RivalName = ReadString(item, "RivalName", string.Empty);
                entry.RivalGroupId = ReadInt32(item, "RivalGroupId", -1);
                entry.MemberIds = ReadInt32List(item, "MemberIds", version >= 4);
                entry.MemberSnapshots = ReadMemberSnapshots(item, "MemberSnapshots", version >= 4);
                entry.SongIds = ReadInt32List(item, "SongIds", version >= 4);
                entry.Sales = ReadInt64(item, "Sales", 0L);
                entry.WeeklySales = ReadInt64(item, "WeeklySales", 0L);
                entry.Profit = ReadInt64(item, "Profit", 0L);
                entry.ChartPosition = ReadInt32(item, "ChartPosition", 0);
                entry.PreviousChartPosition = ReadInt32(item, "PreviousChartPosition", 0);
                entry.PeakChartPosition = ReadInt32(item, "PeakChartPosition", 0);
                entry.WeeksOnChart = ReadInt32(item, "WeeksOnChart", 0);
                entry.CoverPath = ReadString(item, "CoverPath", string.Empty);
                entry.Theme = ReadString(item, "Theme", string.Empty);
                entry.ThemeIndex = ReadInt32(item, "ThemeIndex", 0);
                entry.BackgroundIndex = ReadInt32(item, "BackgroundIndex", 0);
                entry.BackgroundKey = ReadString(item, "BackgroundKey", string.Empty);
                entry.LayoutIndex = ReadInt32(item, "LayoutIndex", 0);
                entry.FontIndex = ReadInt32(item, "FontIndex", 0);
                entry.FontKey = ReadString(item, "FontKey", string.Empty);
                entry.ReleaseKind = ReadInt32(item, "ReleaseKind", 0);
                entry.DebutFanRewardGranted = ReadBoolean(item, "DebutFanRewardGranted", false);
                entry.TextColorIndex = ReadInt32(item, "TextColorIndex", 0);
                entry.TitlePosition = ReadInt32(item, "TitlePosition", 0);
                entry.ShowGroupName = ReadBoolean(item, "ShowGroupName", false);
                entry.OrnamentStyle = ReadInt32(item, "OrnamentStyle", 0);
                entry.FrameStyle = ReadInt32(item, "FrameStyle", 0);
                entry.TitleEffect = ReadInt32(item, "TitleEffect", 0);
                entry.PortraitScale = ReadSingle(item, "PortraitScale", 0f);
                entry.CenterEmphasis = ReadSingle(item, "CenterEmphasis", 0f);
                entry.PortraitYOffset = ReadSingle(item, "PortraitYOffset", 0f);
                entry.PortraitSpacing = ReadSingle(item, "PortraitSpacing", 0f);
                entry.EffectsIntensity = ReadSingle(item, "EffectsIntensity", 0f);
                entry.CenterMemberIndex = ReadInt32(item, "CenterMemberIndex", 0);
                entry.CenterMemberId = ReadInt32(item, "CenterMemberId", -1);
                entry.HasCenterMemberId = ReadBoolean(item, "HasCenterMemberId", false);
                result.Add(entry);
            }
            return result;
        }

        private static List<AlbumMemberSnapshot> ReadMemberSnapshots(
            Dictionary<string, JsonValue> owner,
            string propertyName,
            bool required)
        {
            JsonValue value;
            if (!owner.TryGetValue(propertyName, out value))
            {
                if (required)
                    throw new InvalidDataException("CAA is missing required field '" + propertyName + "'.");
                return new List<AlbumMemberSnapshot>();
            }

            List<JsonValue> values = RequireArray(
                value,
                "CAA field '" + propertyName + "' must be a JSON array.");
            List<AlbumMemberSnapshot> result = new List<AlbumMemberSnapshot>(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                Dictionary<string, JsonValue> item = RequireObject(
                    values[i],
                    "CAA " + propertyName + "[" + i + "] must be a JSON object.");
                if (required)
                    RequireProperties(item, V4MemberSnapshotFields, "CAA v4 member snapshot " + i);
                AlbumMemberSnapshot snapshot = new AlbumMemberSnapshot();
                snapshot.GirlId = ReadInt32(item, "GirlId", -1);
                snapshot.FirstName = ReadString(item, "FirstName", string.Empty);
                snapshot.LastName = ReadString(item, "LastName", string.Empty);
                snapshot.Nickname = ReadString(item, "Nickname", string.Empty);
                snapshot.IdolType = (data_girls.girls._type)ReadInt32(item, "IdolType", 0);
                snapshot.CustomId = ReadString(item, "CustomId", string.Empty);
                snapshot.CustomSpriteAddress = ReadString(item, "CustomSpriteAddress", string.Empty);
                snapshot.PortraitAssets = ReadPortraitAssets(item, "PortraitAssets", required);
                result.Add(snapshot);
            }
            return result;
        }

        private static List<AlbumPortraitAssetReference> ReadPortraitAssets(
            Dictionary<string, JsonValue> owner,
            string propertyName,
            bool required)
        {
            JsonValue value;
            if (!owner.TryGetValue(propertyName, out value))
            {
                if (required)
                    throw new InvalidDataException("CAA is missing required field '" + propertyName + "'.");
                return new List<AlbumPortraitAssetReference>();
            }

            List<JsonValue> values = RequireArray(
                value,
                "CAA field '" + propertyName + "' must be a JSON array.");
            List<AlbumPortraitAssetReference> result =
                new List<AlbumPortraitAssetReference>(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                Dictionary<string, JsonValue> item = RequireObject(
                    values[i],
                    "CAA " + propertyName + "[" + i + "] must be a JSON object.");
                if (required)
                    RequireProperties(item, V4PortraitAssetFields, "CAA v4 portrait asset " + i);
                result.Add(new AlbumPortraitAssetReference
                {
                    Type = (data_girls_textures._spriteType)ReadInt32(item, "Type", 0),
                    AssetId = ReadString(item, "AssetId", string.Empty)
                });
            }
            return result;
        }

        private static List<int> ReadInt32List(
            Dictionary<string, JsonValue> owner,
            string propertyName,
            bool required)
        {
            JsonValue value;
            if (!owner.TryGetValue(propertyName, out value))
            {
                if (required)
                    throw new InvalidDataException("CAA is missing required field '" + propertyName + "'.");
                return new List<int>();
            }

            List<JsonValue> values = RequireArray(
                value,
                "CAA field '" + propertyName + "' must be a JSON array.");
            List<int> result = new List<int>(values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                result.Add(RequireInt32(
                    values[i],
                    "CAA " + propertyName + "[" + i + "] must be a 32-bit integer."));
            }
            return result;
        }

        private static Dictionary<string, JsonValue> RequireObject(
            JsonValue value,
            string errorMessage)
        {
            if (value == null || value.Kind != JsonValueKind.Object || value.ObjectValue == null)
                throw new InvalidDataException(errorMessage);
            return value.ObjectValue;
        }

        private static List<JsonValue> RequireArray(JsonValue value, string errorMessage)
        {
            if (value == null || value.Kind != JsonValueKind.Array || value.ArrayValue == null)
                throw new InvalidDataException(errorMessage);
            return value.ArrayValue;
        }

        private static void RequireProperties(
            Dictionary<string, JsonValue> values,
            string[] propertyNames,
            string ownerName)
        {
            for (int i = 0; i < propertyNames.Length; i++)
            {
                string propertyName = propertyNames[i];
                if (!values.ContainsKey(propertyName))
                {
                    throw new InvalidDataException(
                        ownerName + " is missing required field '" + propertyName + "'.");
                }
            }
        }

        private static void RequireProperty(
            Dictionary<string, JsonValue> values,
            string propertyName)
        {
            if (!values.ContainsKey(propertyName))
                throw new InvalidDataException("The CAA album save document is missing required field '" + propertyName + "'.");
        }

        private static int RequireInt32(
            Dictionary<string, JsonValue> values,
            string propertyName)
        {
            JsonValue value;
            if (!values.TryGetValue(propertyName, out value))
                throw new InvalidDataException("CAA is missing required field '" + propertyName + "'.");
            return RequireInt32(value, "CAA field '" + propertyName + "' must be a 32-bit integer.");
        }

        private static int RequireInt32(JsonValue value, string errorMessage)
        {
            int result;
            if (value == null || value.Kind != JsonValueKind.Number ||
                !int.TryParse(value.NumberValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                throw new InvalidDataException(errorMessage);
            }
            return result;
        }

        private static int ReadInt32(
            Dictionary<string, JsonValue> values,
            string propertyName,
            int defaultValue)
        {
            JsonValue value;
            if (!values.TryGetValue(propertyName, out value))
                return defaultValue;
            return RequireInt32(value, "CAA field '" + propertyName + "' must be a 32-bit integer.");
        }

        private static long RequireInt64(
            Dictionary<string, JsonValue> values,
            string propertyName)
        {
            JsonValue value;
            if (!values.TryGetValue(propertyName, out value))
                throw new InvalidDataException("CAA is missing required field '" + propertyName + "'.");
            long result;
            if (value == null || value.Kind != JsonValueKind.Number ||
                !long.TryParse(value.NumberValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                throw new InvalidDataException("CAA field '" + propertyName + "' must be a 64-bit integer.");
            }
            return result;
        }

        private static bool RequireBoolean(
            Dictionary<string, JsonValue> values,
            string propertyName)
        {
            JsonValue value;
            if (!values.TryGetValue(propertyName, out value))
                throw new InvalidDataException("CAA is missing required field '" + propertyName + "'.");
            if (value == null || value.Kind != JsonValueKind.Boolean)
                throw new InvalidDataException("CAA field '" + propertyName + "' must be a boolean.");
            return value.BooleanValue;
        }

        private static long ReadInt64(
            Dictionary<string, JsonValue> values,
            string propertyName,
            long defaultValue)
        {
            JsonValue value;
            if (!values.TryGetValue(propertyName, out value))
                return defaultValue;
            long result;
            if (value == null || value.Kind != JsonValueKind.Number ||
                !long.TryParse(value.NumberValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
            {
                throw new InvalidDataException("CAA field '" + propertyName + "' must be a 64-bit integer.");
            }
            return result;
        }

        private static float ReadSingle(
            Dictionary<string, JsonValue> values,
            string propertyName,
            float defaultValue)
        {
            JsonValue value;
            if (!values.TryGetValue(propertyName, out value))
                return defaultValue;
            float result;
            if (value == null || value.Kind != JsonValueKind.Number ||
                !float.TryParse(value.NumberValue, NumberStyles.Float, CultureInfo.InvariantCulture, out result) ||
                float.IsNaN(result) || float.IsInfinity(result))
            {
                throw new InvalidDataException("CAA field '" + propertyName + "' must be a finite number.");
            }
            return result;
        }

        private static bool ReadBoolean(
            Dictionary<string, JsonValue> values,
            string propertyName,
            bool defaultValue)
        {
            JsonValue value;
            if (!values.TryGetValue(propertyName, out value))
                return defaultValue;
            if (value == null || value.Kind != JsonValueKind.Boolean)
                throw new InvalidDataException("CAA field '" + propertyName + "' must be a boolean.");
            return value.BooleanValue;
        }

        private static string ReadString(
            Dictionary<string, JsonValue> values,
            string propertyName,
            string defaultValue)
        {
            JsonValue value;
            if (!values.TryGetValue(propertyName, out value))
                return defaultValue;
            if (value == null || value.Kind != JsonValueKind.String)
                throw new InvalidDataException("CAA field '" + propertyName + "' must be a string.");
            return value.StringValue ?? string.Empty;
        }

        private static void AppendPropertyName(StringBuilder builder, string name)
        {
            AppendString(builder, name);
            builder.Append(':');
        }

        private static void AppendInt32(StringBuilder builder, int value)
        {
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendInt64(StringBuilder builder, long value)
        {
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendSingle(StringBuilder builder, float value)
        {
            ValidateFinite(value, "JSON number");
            builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendBoolean(StringBuilder builder, bool value)
        {
            builder.Append(value ? "true" : "false");
        }

        private static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');
            string text = value ?? string.Empty;
            for (int i = 0; i < text.Length; i++)
            {
                char character = text[i];
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < ' ')
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }
                        break;
                }
            }
            builder.Append('"');
        }

        private enum JsonValueKind
        {
            Null,
            Object,
            Array,
            String,
            Number,
            Boolean
        }

        private sealed class JsonValue
        {
            internal JsonValueKind Kind;
            internal Dictionary<string, JsonValue> ObjectValue;
            internal List<JsonValue> ArrayValue;
            internal string StringValue;
            internal string NumberValue;
            internal bool BooleanValue;
        }

        /// <summary>
        /// Small strict JSON parser derived from the parsing strategy used by IM Data Core's
        /// LightweightSidecarJson. It is intentionally schema-agnostic; schema validation lives
        /// in the CAA read helpers above.
        /// </summary>
        private sealed class JsonParser
        {
            private readonly string json;
            private int index;

            internal JsonParser(string jsonText)
            {
                json = jsonText ?? string.Empty;
            }

            internal JsonValue ParseDocument()
            {
                SkipWhitespace();
                JsonValue value = ParseValue();
                SkipWhitespace();
                if (index != json.Length)
                    throw Error("Unexpected content after the JSON document.");
                return value;
            }

            private JsonValue ParseValue()
            {
                SkipWhitespace();
                if (index >= json.Length)
                    throw Error("Unexpected end of JSON input.");

                char character = json[index];
                switch (character)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"':
                        return new JsonValue
                        {
                            Kind = JsonValueKind.String,
                            StringValue = ParseString()
                        };
                    case 't':
                        ReadLiteral("true");
                        return new JsonValue { Kind = JsonValueKind.Boolean, BooleanValue = true };
                    case 'f':
                        ReadLiteral("false");
                        return new JsonValue { Kind = JsonValueKind.Boolean, BooleanValue = false };
                    case 'n':
                        ReadLiteral("null");
                        return new JsonValue { Kind = JsonValueKind.Null };
                    default:
                        if (character == '-' || (character >= '0' && character <= '9'))
                        {
                            return new JsonValue
                            {
                                Kind = JsonValueKind.Number,
                                NumberValue = ParseNumber()
                            };
                        }
                        throw Error("Unexpected JSON token.");
                }
            }

            private JsonValue ParseObject()
            {
                Expect('{');
                SkipWhitespace();
                Dictionary<string, JsonValue> values =
                    new Dictionary<string, JsonValue>(StringComparer.Ordinal);
                if (TryConsume('}'))
                {
                    return new JsonValue { Kind = JsonValueKind.Object, ObjectValue = values };
                }

                while (true)
                {
                    SkipWhitespace();
                    if (index >= json.Length || json[index] != '"')
                        throw Error("Expected a JSON object property name.");
                    string name = ParseString();
                    SkipWhitespace();
                    Expect(':');
                    JsonValue value = ParseValue();
                    if (values.ContainsKey(name))
                        throw Error("Duplicate JSON object property '" + name + "'.");
                    values.Add(name, value);
                    SkipWhitespace();
                    if (TryConsume('}'))
                        break;
                    Expect(',');
                }

                return new JsonValue { Kind = JsonValueKind.Object, ObjectValue = values };
            }

            private JsonValue ParseArray()
            {
                Expect('[');
                SkipWhitespace();
                List<JsonValue> values = new List<JsonValue>();
                if (TryConsume(']'))
                {
                    return new JsonValue { Kind = JsonValueKind.Array, ArrayValue = values };
                }

                while (true)
                {
                    values.Add(ParseValue());
                    SkipWhitespace();
                    if (TryConsume(']'))
                        break;
                    Expect(',');
                }

                return new JsonValue { Kind = JsonValueKind.Array, ArrayValue = values };
            }

            private string ParseString()
            {
                Expect('"');
                StringBuilder builder = new StringBuilder();
                while (index < json.Length)
                {
                    char character = json[index++];
                    if (character == '"')
                        return builder.ToString();
                    if (character == '\\')
                    {
                        if (index >= json.Length)
                            throw Error("Unterminated JSON escape sequence.");
                        char escaped = json[index++];
                        switch (escaped)
                        {
                            case '"': builder.Append('"'); break;
                            case '\\': builder.Append('\\'); break;
                            case '/': builder.Append('/'); break;
                            case 'b': builder.Append('\b'); break;
                            case 'f': builder.Append('\f'); break;
                            case 'n': builder.Append('\n'); break;
                            case 'r': builder.Append('\r'); break;
                            case 't': builder.Append('\t'); break;
                            case 'u': builder.Append(ParseUnicodeEscape()); break;
                            default: throw Error("Invalid JSON escape sequence.");
                        }
                        continue;
                    }
                    if (character < ' ')
                        throw Error("Unescaped control character in JSON string.");
                    builder.Append(character);
                }
                throw Error("Unterminated JSON string.");
            }

            private char ParseUnicodeEscape()
            {
                if (index + 4 > json.Length)
                    throw Error("Incomplete JSON unicode escape.");
                int value = 0;
                for (int offset = 0; offset < 4; offset++)
                {
                    char character = json[index++];
                    value <<= 4;
                    if (character >= '0' && character <= '9') value += character - '0';
                    else if (character >= 'a' && character <= 'f') value += character - 'a' + 10;
                    else if (character >= 'A' && character <= 'F') value += character - 'A' + 10;
                    else throw Error("Invalid JSON unicode escape.");
                }
                return (char)value;
            }

            private string ParseNumber()
            {
                int start = index;
                if (json[index] == '-')
                {
                    index++;
                    if (index >= json.Length)
                        throw Error("Incomplete JSON number.");
                }

                if (json[index] == '0')
                {
                    index++;
                }
                else
                {
                    if (json[index] < '1' || json[index] > '9')
                        throw Error("Invalid JSON number.");
                    while (index < json.Length && json[index] >= '0' && json[index] <= '9')
                        index++;
                }

                if (index < json.Length && json[index] == '.')
                {
                    index++;
                    int fractionStart = index;
                    while (index < json.Length && json[index] >= '0' && json[index] <= '9')
                        index++;
                    if (fractionStart == index)
                        throw Error("Invalid JSON number fraction.");
                }

                if (index < json.Length && (json[index] == 'e' || json[index] == 'E'))
                {
                    index++;
                    if (index < json.Length && (json[index] == '+' || json[index] == '-'))
                        index++;
                    int exponentStart = index;
                    while (index < json.Length && json[index] >= '0' && json[index] <= '9')
                        index++;
                    if (exponentStart == index)
                        throw Error("Invalid JSON number exponent.");
                }

                return json.Substring(start, index - start);
            }

            private void ReadLiteral(string literal)
            {
                if (index + literal.Length > json.Length ||
                    string.CompareOrdinal(json, index, literal, 0, literal.Length) != 0)
                {
                    throw Error("Invalid JSON literal.");
                }
                index += literal.Length;
            }

            private void SkipWhitespace()
            {
                while (index < json.Length)
                {
                    char character = json[index];
                    if (character == ' ' || character == '\t' || character == '\r' || character == '\n')
                        index++;
                    else
                        break;
                }
            }

            private bool TryConsume(char expected)
            {
                SkipWhitespace();
                if (index < json.Length && json[index] == expected)
                {
                    index++;
                    return true;
                }
                return false;
            }

            private void Expect(char expected)
            {
                SkipWhitespace();
                if (index >= json.Length || json[index] != expected)
                    throw Error("Expected '" + expected + "'.");
                index++;
            }

            private InvalidDataException Error(string message)
            {
                return new InvalidDataException(message + " At character " + index + ".");
            }
        }
    }
}
