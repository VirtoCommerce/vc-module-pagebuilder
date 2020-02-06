using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.PageBuilderModule.Web
{
    public class Module : IModule
    {
        public ManifestModuleInfo ModuleInfo { get; set; }

        public void Initialize(IServiceCollection serviceCollection)
        {
            // Method intentionally left empty.
        }

        public void PostInitialize(IApplicationBuilder appBuilder)
        {
            var settingsRegistrar = appBuilder.ApplicationServices.GetRequiredService<ISettingsRegistrar>();
            settingsRegistrar.RegisterSettings(ModuleConstants.Settings.AllSettings, ModuleInfo.Id);
        }

        public void Uninstall()
        {
            // Method intentionally left empty.
        }
    }
}
