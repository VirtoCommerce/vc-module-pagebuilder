using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.PageBuilderModule.Web
{
    public static class ModuleConstants
    {
        public static class Settings
        {
            public static class General
            {
                public static SettingDescriptor StoreUrl => new SettingDescriptor
                {
                    Name = "VirtoCommerce.PageBuilderModule.General.StoreUrl",
                    GroupName = "CMS Content|General",
                    ValueType = SettingValueType.ShortText
                };

                public static SettingDescriptor StorePreviewPath => new SettingDescriptor
                {
                    Name = "VirtoCommerce.PageBuilderModule.General.StorePreviewPath",
                    ValueType = SettingValueType.ShortText,
                    GroupName = "CMS Content|General",
                    DefaultValue = "/designer-preview"
                };

                public static SettingDescriptor TokenUrl => new SettingDescriptor
                {
                    Name = "VirtoCommerce.PageBuilderModule.General.TokenUrl",
                    ValueType = SettingValueType.ShortText,
                    GroupName = "CMS Content|General",
                    DefaultValue = "/connect/token"
                };

                public static SettingDescriptor AssetsPath => new SettingDescriptor
                {
                    Name = "VirtoCommerce.PageBuilderModule.General.AssetsPath",
                    GroupName = "CMS Content|General",
                    ValueType = SettingValueType.ShortText,
                    DefaultValue = "assets/pages"
                };

                public static SettingDescriptor UseGlobalAssets => new SettingDescriptor
                {
                    Name = "VirtoCommerce.PageBuilderModule.General.UseGlobalAssets",
                    GroupName = "CMS Content|General",
                    ValueType = SettingValueType.Boolean,
                    DefaultValue = false
                };

                public static SettingDescriptor Layouts => new SettingDescriptor
                {
                    Name = "VirtoCommerce.PageBuilderModule.General.Layouts",
                    GroupName = "CMS Content|General",
                    ValueType = SettingValueType.Json,
                    DefaultValue = "[{ \"label\": \"Theme\", \"value\": \"theme\" }, { \"label\": \"Empty\", \"value\": \"empty\" }]"
                };

                public static IEnumerable<SettingDescriptor> AllSettings =>
                    new List<SettingDescriptor>
                    {
                        StoreUrl,
                        StorePreviewPath,
                        TokenUrl,
                        AssetsPath,
                        UseGlobalAssets,
                        Layouts
                    };
            }

            public static IEnumerable<SettingDescriptor> AllSettings => General.AllSettings;
        }
    }
}
