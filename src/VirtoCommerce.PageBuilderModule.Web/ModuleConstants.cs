using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.PageBuilderModule.Web
{
    public static class ModuleConstants
    {
        //public static class Security
        //{
        //    public static class Permissions
        //    {
        //        public const string Access = "catalog:access",
        //          Create = "catalog:create",
        //          Read = "catalog:read",
        //          Update = "catalog:update",
        //          Delete = "catalog:delete",
        //          Export = "catalog:export",
        //          Import = "catalog:import",
        //          CatalogBrowseFiltersRead = "catalog:BrowseFilters:Read",
        //          CatalogBrowseFiltersUpdate = "atalog:BrowseFilters:Update";

        //        public static string[] AllPermissions = new[] { Access, Create, Read, Update, Delete, Export, Import, CatalogBrowseFiltersRead, CatalogBrowseFiltersUpdate };
        //    }
        //}

        public static class Settings
        {
            public static class General
            {
                public static SettingDescriptor StoreUrl = new SettingDescriptor
                {
                    Name = "VirtoCommerce.PageBuilderModule.General.StoreUrl",
                    GroupName = "CMS Content|General",
                    ValueType = SettingValueType.ShortText
                };

                public static SettingDescriptor StorePreviewPath = new SettingDescriptor
                {
                    Name = "VirtoCommerce.PageBuilderModule.General.StorePreviewPath",
                    ValueType = SettingValueType.ShortText,
                    GroupName = "CMS Content|General",
                    DefaultValue = "/designer-preview"
                };

                public static SettingDescriptor TokenUrl = new SettingDescriptor
                {
                    Name = "VirtoCommerce.PageBuilderModule.General.TokenUrl",
                    ValueType = SettingValueType.ShortText,
                    GroupName = "CMS Content|General",
                    DefaultValue = "/connect/token"
                };

                public static SettingDescriptor AssetsPath = new SettingDescriptor
                {
                    Name = "VirtoCommerce.PageBuilderModule.General.AssetsPath",
                    GroupName = "CMS Content|General",
                    ValueType = SettingValueType.ShortText,
                    DefaultValue = "assets/pages"
                };

                public static IEnumerable<SettingDescriptor> AllSettings
                {
                    get
                    {
                        return new List<SettingDescriptor>
                        {
                            StoreUrl,
                            StorePreviewPath,
                            TokenUrl,
                            AssetsPath
                        };
                    }
                }
            }

            //public static class Search
            //{
            //    public static SettingDescriptor UseCatalogIndexedSearchInManager = new SettingDescriptor
            //    {
            //        Name = "Catalog.Search.UseCatalogIndexedSearchInManager",
            //        GroupName = "Catalog|Search",
            //        ValueType = SettingValueType.Boolean,
            //        DefaultValue = true
            //    };

            //    public static SettingDescriptor UseFullObjectIndexStoring = new SettingDescriptor
            //    {
            //        Name = "Catalog.Search.UseFullObjectIndexStoring",
            //        GroupName = "Catalog|Search",
            //        ValueType = SettingValueType.Boolean,
            //        DefaultValue = false
            //    };

            //    public static SettingDescriptor IndexationDateProduct = new SettingDescriptor
            //    {
            //        Name = "VirtoCommerce.Search.IndexingJobs.IndexationDate.Product",
            //        GroupName = "Catalog|Search",
            //        ValueType = SettingValueType.DateTime,
            //        DefaultValue = default(DateTime)
            //    };

            //    public static SettingDescriptor IndexationDateCategory = new SettingDescriptor
            //    {
            //        Name = "VirtoCommerce.Search.IndexingJobs.IndexationDate.Category",
            //        GroupName = "Catalog|Search",
            //        ValueType = SettingValueType.DateTime,
            //        DefaultValue = default(DateTime)
            //    };

            //    public static IEnumerable<SettingDescriptor> AllSettings
            //    {
            //        get
            //        {
            //            yield return UseCatalogIndexedSearchInManager;
            //            yield return UseFullObjectIndexStoring;
            //            yield return IndexationDateProduct;
            //            yield return IndexationDateCategory;
            //        }
            //    }
            //}

            public static IEnumerable<SettingDescriptor> AllSettings
            {
                get
                {
                    //return General.AllSettings.Concat(Search.AllSettings);
                    return General.AllSettings;
                }
            }
        }
    }
}
