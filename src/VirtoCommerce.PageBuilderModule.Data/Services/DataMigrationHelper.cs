using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class DataMigrationHelper(
    IGroupedPageSearchService groupedPageSearchService,
    IGroupedPageService groupedPageService
    )
{
    public async Task MigratePages()
    {
        const int step = 20;
        var criteria = new PageBuilderPageSearchCriteria
        {
            Status = $"{PageStatuses.Draft},{PageStatuses.Published}",
            Skip = 0,
            Take = step,
        };
        var groups = await groupedPageSearchService.SearchAsync(criteria);
        while (groups.Results.Count > 0)
        {
            foreach (var group in groups.Results)
            {
                var page = group.Pages.FirstOrDefault(x => x.Status == PageStatuses.Draft)
                           ?? group.Pages.FirstOrDefault(x => x.Status == PageStatuses.Published);

                if (page == null)
                {
                    continue;
                }

                var content = await groupedPageService.LoadContent(page.Id);
                MigrateGroup(group, content);
            }

            await groupedPageService.SaveChangesAsync(groups.Results);
            criteria.Skip += step;
            groups = await groupedPageSearchService.SearchAsync(criteria);
        }

    }

    private void MigrateGroup(GroupedPageBuilderPage model, string content)
    {
        dynamic pageContent = Newtonsoft.Json.JsonConvert.DeserializeObject(content);
        //model.Visibility = pageContent?.settings?.;
        //model.UserGroups = pageContent?.settings?.;
        //model.StartDate = pageContent?.settings?.;
        //model.EndDate = pageContent?.settings?.;
    }
}
