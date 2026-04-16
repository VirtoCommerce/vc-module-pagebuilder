using System;
using System.Collections.Generic;

namespace VirtoCommerce.PageBuilderModule.Data.ExportImport;

public class PageBuilderExportPage
{
    public string GroupId { get; set; }
    public string Name { get; set; }
    public string Permalink { get; set; }
    public string StoreId { get; set; }
    public string CultureName { get; set; }
    public bool Visibility { get; set; }
    public string UserGroups { get; set; }
    public string OrganizationId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public IList<PageBuilderExportPageVariant> Variants { get; set; } = [];
}

public class PageBuilderExportPageVariant
{
    public string PageId { get; set; }
    public string Status { get; set; }
    public string Content { get; set; }
}
