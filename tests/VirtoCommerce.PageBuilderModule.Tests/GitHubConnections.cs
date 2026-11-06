using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Tests;

/// <summary>The GitHub connection and the policy over it, built from a configuration as the module builds them.</summary>
internal static class GitHubConnections
{
    public static GitHubConnectionService Service(GitContentOptions options, ISettingsManager settings = null, IDataProtectionProvider keys = null,
        GitHubScript github = null, IConfiguration configuration = null) =>
        new(Options.Create(options), configuration ?? new ConfigurationBuilder().Build(), settings ?? new MemorySettingsManager(),
            keys ?? new EphemeralDataProtectionProvider(), new GitHubApiClient(github ?? new GitHubScript()), NullLogger<GitHubConnectionService>.Instance);

    public static GitContentPolicy Policy(IOptions<GitContentOptions> options, ISettingsManager settings, IGitContentOwnership ownership = null,
        IStoreService stores = null) =>
        new(Service(options.Value), options, settings, stores, ownership ?? new FakeGitContentOwnership());
}
