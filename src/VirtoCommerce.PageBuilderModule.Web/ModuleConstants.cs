using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.PageBuilderModule.Web
{
    public static class ModuleConstants
    {
        public static class Security
        {
            public static class Permissions
            {
                public const string Theme = "builder:theme";
                public const string Templates = "builder:templates";

                public static string[] AllPermissions = { Theme, Templates };
            }
        }

        public static class Settings
        {
            public static class General
            {
                public static SettingDescriptor StoreUrl { get; } = new()
                {
                    Name = "VirtoCommerce.PageBuilderModule.General.StoreUrl",
                    GroupName = "CMS Content|General",
                    ValueType = SettingValueType.ShortText
                };

                public static SettingDescriptor StorePreviewPath { get; } = new()
                {
                    Name = "VirtoCommerce.PageBuilderModule.General.StorePreviewPath",
                    ValueType = SettingValueType.ShortText,
                    GroupName = "CMS Content|General",
                    DefaultValue = "/designer-preview"
                };

                public static IEnumerable<SettingDescriptor> AllGeneralSettings
                {
                    get
                    {
                        yield return StoreUrl;
                        yield return StorePreviewPath;
                    }
                }
            }

            public static IEnumerable<SettingDescriptor> AllSettings => General.AllGeneralSettings;
        }
    }
}
