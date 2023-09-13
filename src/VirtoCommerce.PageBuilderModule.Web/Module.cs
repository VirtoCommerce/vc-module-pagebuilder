using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.ContentModule.Core.Extensions;
using VirtoCommerce.ContentModule.Core.Search;
using VirtoCommerce.PageBuilderModule.Data.Search;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.PageBuilderModule.Web
{
    public class Module : IModule, IHasConfiguration
    {
        public ManifestModuleInfo ModuleInfo { get; set; }
        public IConfiguration Configuration { get; set; }

        public void Initialize(IServiceCollection serviceCollection)
        {
            var isFullTextSearchEnabled = Configuration.IsContentFullTextSearchEnabled();

            if (isFullTextSearchEnabled)
            {
                serviceCollection.AddTransient<PageBuilderContentItemBuilder>();
            }
        }

        public void PostInitialize(IApplicationBuilder appBuilder)
        {
            var settingsRegistrar = appBuilder.ApplicationServices.GetRequiredService<ISettingsRegistrar>();
            settingsRegistrar.RegisterSettings(ModuleConstants.Settings.AllSettings, ModuleInfo.Id);

            var permissionsProvider = appBuilder.ApplicationServices.GetRequiredService<IPermissionsRegistrar>();
            permissionsProvider.RegisterPermissions(ModuleConstants.Security.Permissions.AllPermissions.Select(x =>
                new Permission
                {
                    GroupName = "Page builder",
                    ModuleId = ModuleInfo.Id,
                    Name = x
                }).ToArray());

            var isFullTextSearchEnabled = Configuration.IsContentFullTextSearchEnabled();

            if (isFullTextSearchEnabled)
            {
                var contentItemTypeRegistrar = appBuilder.ApplicationServices.GetService<IContentItemTypeRegistrar>();
                contentItemTypeRegistrar.RegisterContentItemType(".page", appBuilder.ApplicationServices.GetService<PageBuilderContentItemBuilder>);
                contentItemTypeRegistrar.RegisterContentItemType(".page-draft", appBuilder.ApplicationServices.GetService<PageBuilderContentItemBuilder>);
            }
        }

        public void Uninstall()
        {
            // Method intentionally left empty.
        }
    }
}
