using System;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Core.Services;
using VirtoCommerce.PageBuilderModule.Core.Theme;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.PageBuilderModule.Tests;

internal static class SiteHistories
{
    // All histories share one server's keys, so a cursor one history wrote can be read by the next.
    private static readonly IDataProtectionProvider Keys = new EphemeralDataProtectionProvider();

    public static IOptions<GitContentOptions> Options() => Microsoft.Extensions.Options.Options.Create(new GitContentOptions
    {
        Enabled = true,
        Organization = "acme",
        Token = "t0ken",
        BaseBranch = "main",
        GraphQlUrl = new Uri("https://api.github.test/graphql"),
    });

    public static GitHubSiteHistory Create(GitHubScript github, IThemeStorageFactory themes = null, IGroupedPageService pages = null,
        ISettingsManager settings = null, IGitContentOwnership ownership = null, IOptions<GitContentOptions> options = null, IGitContentProvisioner provisioner = null)
    {
        options ??= Options();
        var policy = GitHubConnections.CreatePolicy(options, settings ?? new FakeSettingsManager(true), ownership ?? new FakeGitContentOwnership());
        var repository = new GitHubContentRepository(github, new MemoryCache(new MemoryCacheOptions()), options);

        return new GitHubSiteHistory(policy, repository, new GitHubApiClient(github), GitHubConnections.CreateService(options.Value), pages, themes,
            provisioner ?? new FakeGitContentProvisioner(), options, Keys, NullLogger<GitHubSiteHistory>.Instance);
    }
}
