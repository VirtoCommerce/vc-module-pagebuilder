using System;
using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Data.GitContent;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.PageBuilderModule.Tests;

internal static class GitHubConnections
{
    public static readonly string AppPrivateKey = RSA.Create(2048).ExportPkcs8PrivateKeyPem();

    public static GitHubConnectionService CreateService(GitContentOptions options, ISettingsManager settings = null, IDataProtectionProvider keys = null,
        GitHubScript github = null, IConfiguration configuration = null) =>
        new(Options.Create(options), configuration ?? new ConfigurationBuilder().Build(), settings ?? new FakeSettingsManager(),
            keys ?? new EphemeralDataProtectionProvider(), new GitHubApiClient(github ?? new GitHubScript()), NullLogger<GitHubConnectionService>.Instance);

    public static GitHubConnectionService CreateService(ISettingsManager settings, IDataProtectionProvider keys = null, GitHubScript github = null,
        Action<GitContentOptions> configure = null, IConfiguration configuration = null)
    {
        var options = new GitContentOptions();
        configure?.Invoke(options);
        return CreateService(options, settings, keys, github, configuration);
    }

    public static GitContentPolicy CreatePolicy(IOptions<GitContentOptions> options, ISettingsManager settings, IGitContentOwnership ownership = null) =>
        new(CreateService(options.Value), options, settings, ownership ?? new FakeGitContentOwnership());

    public static GitHubScript CreateManifestConversion(string owner) => new(("POST", "app-manifests/c0de/conversions", (_, _) =>
        GitHubScript.Json(new JObject
        {
            ["id"] = 77,
            ["slug"] = "page-builder-acme",
            ["pem"] = AppPrivateKey,
            ["owner"] = new JObject { ["login"] = owner },
        }.ToString(), HttpStatusCode.Created)));
}
