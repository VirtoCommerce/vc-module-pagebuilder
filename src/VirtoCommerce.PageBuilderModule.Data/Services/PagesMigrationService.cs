using Hangfire;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants;

namespace VirtoCommerce.PageBuilderModule.Data.Services;

public class PagesMigrationService(
    IGroupedPageSearchService groupedPageSearchService,
    IGroupedPageService groupedPageService,
    ISettingsManager settingsManager
    ) : IPagesMigrationService
{
    private static readonly object _lockObject = new();

    public void StartMigration()
    {
        lock (_lockObject)
        {
            var migrationCompleted = settingsManager.GetValue<bool>(Settings.Migration.MigrateMetadataFromContent);
            if (!migrationCompleted)
            {
                BackgroundJob.Enqueue(() => MigratePages());
            }
        }
    }

    private async Task MigratePages()
    {
        try
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
        finally
        {
            await settingsManager.SetValueAsync(Settings.Migration.MigrateMetadataFromContent.Name, true);
        }
    }

    private static void MigrateGroup(GroupedPageBuilderPage model, string contentAsString)
    {
        try
        {
            var content = JObject.Parse(contentAsString.IsNullOrEmpty() ? "{}" : contentAsString);
            var settings = content["settings"];
            model.Visibility = (settings?.Value<bool?>("visibility") ?? true);
            model.UserGroups = settings?.Value<string>("userGroups");
            model.StartDate = settings?.Value<DateTime?>("startDate");
            model.EndDate = settings?.Value<DateTime?>("endDate");
        }
        catch (Exception)
        {
            // ignore errors
        }
    }
}
