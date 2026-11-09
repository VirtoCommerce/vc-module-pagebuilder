using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.PageBuilderModule.Core
{
    public static class ModuleConstants
    {
        public const string DefaultPageContent = "{ \"settings\": {}, \"content\": [] }";

        /// <summary>Appended to a page's blob name to name its draft in the flow that came before git.</summary>
        public const string DraftSuffix = "-draft";

        public static class ContentProviders
        {
            /// <summary>
            /// <see cref="VirtoCommerce.Pages.Core.ContentProviders.IPageContentProvider.ProviderName"/> of the Page Builder
            /// content provider. Consumers (for example, the standalone preview in XCMS) select the provider by this name,
            /// so it is a public contract and must not change.
            /// </summary>
            public const string PageBuilder = "PageBuilder";
        }

        public static class SharedComponents
        {
            public const string ReferenceType = "componentRef";
            public const string ReferenceProperty = "componentRef";
            public const int NameMaxLength = 128;
        }

        public static class Theme
        {
            public const string DefaultThemeName = "default";
            public const string ThemeNamePropertyName = "DefaultThemeName";
            public const string FallbackLanguage = "en-US";
        }

        public static class Palette
        {
            public const string Light = "light";
            public const string Dark = "dark";

            /// <summary>WCAG 2.2 AA minimum contrast of normal text.</summary>
            public const double TextContrastRatio = 4.5;

            /// <summary>WCAG 2.2 AA minimum contrast of large text, icons and UI components.</summary>
            public const double NonTextContrastRatio = 3;
        }

        public static class GitHistory
        {
            public const int DefaultPageSize = 20;

            public const string SavedSummary = "Saved";
            public const string PublishedSummary = "Published";
            public const string DiscardedSummary = "Discarded unpublished changes";
            public const string RestoredSummary = "Restored";

            public const int RestoredVersionLength = 7;
        }

        public static class Headers
        {
            public const string SaveId = "X-PageBuilder-Save";
        }

        public static class PageStatuses
        {
            public const string Draft = "Draft";
            public const string Published = "Published";
            public const string Archived = "Archived";
        }

        public static class PageTypes
        {
            public const string Landing = "Landing";
            public const string Multilanguage = "Multilanguage";
        }

        public static class TranslationStatuses
        {
            /// <summary>A store language (or the requested one) has no version.</summary>
            public const string Missing = "Missing";

            /// <summary>A translation is behind the source language version.</summary>
            public const string Outdated = "Outdated";
        }

        public static class SchemaKinds
        {
            public const string Sections = "sections";
            public const string Templates = "templates";
            public const string Blocks = "blocks";
            public const string Objects = "objects";
            public const string Shared = "shared";

            public static IReadOnlyList<string> All { get; } = [Sections, Templates, Blocks, Objects, Shared];
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
                public const string SharedComponentsRead = "builder:shared-components:read";
                public const string SharedComponentsCreate = "builder:shared-components:create";
                public const string SharedComponentsUpdate = "builder:shared-components:update";
                public const string SharedComponentsDelete = "builder:shared-components:delete";

                /// <summary>
                /// Promoting a page to production. Deliberately separate from <see cref="Publish"/>: that
                /// one writes to the environment editors work against, this one changes the public site.
                /// Folding them together would make "may edit pages" mean "may change the live site".
                /// </summary>
                public const string Promote = "builder:promote";

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
                    Promote,
                    SharedComponentsRead,
                    SharedComponentsCreate,
                    SharedComponentsUpdate,
                    SharedComponentsDelete,
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

                public static SettingDescriptor AssetReferenceIndexMigrated { get; } = new()
                {
                    Name = "VirtoCommerce.PageBuilderModule.Migration.AssetReferenceIndexMigrated",
                    GroupName = "CMS|Migration",
                    ValueType = SettingValueType.Boolean,
                    DefaultValue = false,
                };

                public static IEnumerable<SettingDescriptor> AllMigrationSettings
                {
                    get
                    {
                        yield return MetadataFromContentMigrated;
                        yield return AssetReferenceIndexMigrated;
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

                public static SettingDescriptor GitContentRepository { get; } = new()
                {
                    Name = "VirtoCommerce.PageBuilderModule.Store.GitContentRepository",
                    GroupName = "CMS|Page builder",
                    ValueType = SettingValueType.ShortText,
                    DefaultValue = "",
                };

                /// <summary>Public, so the storefront sees it in the store's module settings.</summary>
                public static SettingDescriptor NewPageBuilder { get; } = new()
                {
                    Name = "VirtoCommerce.PageBuilderModule.Store.NewPageBuilder",
                    GroupName = "CMS|Page builder",
                    ValueType = SettingValueType.Boolean,
                    DefaultValue = false,
                    IsPublic = true,
                };

                public static IEnumerable<SettingDescriptor> AllStoreLevelSettings
                {
                    get
                    {
                        yield return PreviewUserIds;
                        yield return NewPageBuilder;
                        yield return GitContentEnabled;
                        yield return GitContentRepository;
                    }
                }
            }

            public static class GitHub
            {
                /// <summary>Encrypted with the data protection keys.</summary>
                public static SettingDescriptor AppConnection { get; } = new()
                {
                    Name = "VirtoCommerce.PageBuilderModule.GitHub.AppConnection",
                    GroupName = "CMS|Page builder",
                    ValueType = SettingValueType.LongText,
                    DefaultValue = "",
                    IsHidden = true,
                };

                public static IEnumerable<SettingDescriptor> AllGitHubSettings
                {
                    get
                    {
                        yield return AppConnection;
                    }
                }
            }

            public static IEnumerable<SettingDescriptor> AllSettings =>
                General.AllGeneralSettings
                    .Union(Migration.AllMigrationSettings)
                    .Union(GitHub.AllGitHubSettings)
                    .Union(StoreLevelSettings.AllStoreLevelSettings);
        }
    }
}
