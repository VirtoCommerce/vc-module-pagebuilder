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
    private static readonly object LockObject = new();

    public void StartMigration()
    {
        lock (LockObject)
        {
            var migrationCompleted = settingsManager.GetValue<bool>(Settings.Migration.MetadataFromContentMigrated);
            if (!migrationCompleted)
            {
                BackgroundJob.Enqueue(() => MigratePages());
            }
        }
    }

    public async Task MigratePages()
    {
        try
        {
            const int step = 20;
            var criteria = new PageBuilderPageSearchCriteria
            {
                Statuses = $"{PageStatuses.Draft},{PageStatuses.Published}",
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
            await settingsManager.SetValueAsync(Settings.Migration.MetadataFromContentMigrated.Name, true);
        }
    }

    private static void MigrateGroup(GroupedPageBuilderPage model, string contentAsString)
    {
        try
        {
            var content = JObject.Parse(contentAsString.IsNullOrEmpty() ? "{}" : contentAsString);
            var settings = (JObject)content["settings"];
            SetValue(settings, "visibility", (bool v) => model.Visibility = v);
            SetValue(settings, "userGroups", (string v) => model.UserGroups = v);
            SetValue(settings, "startDate", (DateTime v) => model.StartDate = v);
            SetValue(settings, "endDate", (DateTime v) => model.EndDate = v);
        }
        catch (Exception)
        {
            // ignore errors
        }
    }

    private static void SetValue<T>(JObject settings, string propertyName, Action<T> setter)
    {
        var token = settings?.GetValue(propertyName);
        if (token == null || token.Type == JTokenType.Null)
        {
            return;
        }

        var value = token.ToObject<T>();
        if (value != null)
        {
            setter(value);
        }
    }
}
