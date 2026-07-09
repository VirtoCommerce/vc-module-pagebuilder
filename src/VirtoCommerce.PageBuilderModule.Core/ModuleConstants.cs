using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.PageBuilderModule.Core
{
    public static class ModuleConstants
    {
        public const string DefaultPageContent = "{ \"settings\": {}, \"content\": [] }";

        public static class PageStatuses
        {
            public const string Draft = "Draft";
            public const string Published = "Published";
            public const string Archived = "Archived";
        }

        public static class PageLifecycleFilters
        {
            public const string Drafts = "drafts";
            public const string Pending = "pending";
            public const string Active = "active";
            public const string Archived = "archived";
        }

        public static class Security
        {
            public static class Permissions
            {
                public const string Theme = "builder:theme";
                public const string Templates = "builder:templates";
                public const string Access = "builder:access";
                public const string Create = "builder:create";
                public const string Read = "builder:read";
                public const string Update = "builder:update";
                public const string Delete = "builder:delete";
                public const string Publish = "builder:publish";

                public static string[] AllPermissions { get; } =
                [
                    Theme,
                    Templates,
                    Access,
                    Create,
                    Read,
                    Update,
                    Delete,
                    Publish,
                ];
            }
        }

        public static class Settings
        {
            /// <summary>
            /// Object type store-level settings are registered and looked up under. Matches
            /// <c>nameof(Store)</c>, kept here so layers that only need the key do not have to
            /// reference the store module.
            /// </summary>
            public const string StoreSettingsObjectType = "Store";

            public static class General
            {
                public static SettingDescriptor StoreUrl { get; } = new()
                {
                    Name = "VirtoCommerce.PageBuilderModule.General.StoreUrl",
                    GroupName = "CMS|General",
                    ValueType = SettingValueType.ShortText
                };

                public static SettingDescriptor StorePreviewPath { get; } = new()
                {
                    Name = "VirtoCommerce.PageBuilderModule.General.StorePreviewPath",
                    ValueType = SettingValueType.ShortText,
                    GroupName = "CMS|General",
                    DefaultValue = "/designer-preview"
                };

                public static SettingDescriptor OzAgentUrl { get; } = new()
                {
                    Name = "VirtoCommerce.PageBuilderModule.General.OzAgentUrl",
                    GroupName = "CMS|General",
                    ValueType = SettingValueType.ShortText,
                    DefaultValue = "",
                };

                public static IEnumerable<SettingDescriptor> AllGeneralSettings
                {
                    get
                    {
                        yield return StoreUrl;
                        yield return StorePreviewPath;
                        yield return OzAgentUrl;
                    }
                }
            }

            public static class Migration
            {
                public static SettingDescriptor MetadataFromContentMigrated { get; } = new()
                {
                    Name = "VirtoCommerce.PageBuilderModule.Migration.MetadataFromContentMigrated",
                    GroupName = "CMS|Migration",
                    ValueType = SettingValueType.Boolean,
                    DefaultValue = false,
                };
                public static IEnumerable<SettingDescriptor> AllMigrationSettings
                {
                    get
                    {
                        yield return MetadataFromContentMigrated;
                    }
                }
            }

            public static class StoreLevelSettings
            {
                public static SettingDescriptor PreviewUserIds { get; } = new()
                {
                    Name = "VirtoCommerce.PageBuilderModule.Store.PreviewUserIds",
                    GroupName = "CMS|Page builder",
                    ValueType = SettingValueType.LongText,
                    DefaultValue = "",
                };

                /// <summary>
                /// Turns the git content flow on for this store: pages are edited on git work branches
                /// and published by merging into the production branch, instead of being written straight
                /// to blob storage. Off by default — a store keeps its current behaviour until someone
                /// opts it in. The connection itself (repository, token, base branch) lives in
                /// appsettings under <see cref="GitContent.GitContentOptions.SectionName"/>; this switch
                /// only decides which stores use it.
                /// </summary>
                public static SettingDescriptor GitContentEnabled { get; } = new()
                {
                    Name = "VirtoCommerce.PageBuilderModule.Store.GitContentEnabled",
                    GroupName = "CMS|Page builder",
                    ValueType = SettingValueType.Boolean,
                    DefaultValue = false,
                };

                public static IEnumerable<SettingDescriptor> AllStoreLevelSettings
                {
                    get
                    {
                        yield return PreviewUserIds;
                        yield return GitContentEnabled;
                    }
                }
            }

            public static IEnumerable<SettingDescriptor> AllSettings =>
                General.AllGeneralSettings
                    .Union(Migration.AllMigrationSettings)
                    .Union(StoreLevelSettings.AllStoreLevelSettings);
        }
    }
}
