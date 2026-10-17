using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.PageBuilderModule.Data.GitContent
{
    /// <summary>
    /// The git content connection every service gets as <c>IOptions&lt;GitContentOptions&gt;</c>: the
    /// environment's configuration when that is complete, else the configuration with the GitHub App of the
    /// one-click setup filled in.
    /// <para>
    /// The App is kept in a hidden platform setting, protected with the platform's data protection keys.
    /// Those keys are not in the database (they are in Redis when the platform has it, else in the server's
    /// own key folder), so a database restored on another environment cannot use the App there. A server
    /// whose keys were lost reports the connection as unreadable and the site panel offers the setup again.
    /// </para>
    /// </summary>
    public sealed class GitContentConnection : IOptions<GitContentOptions>
    {
        /// <summary>The client of the setup's own GitHub call, which carries no credential.</summary>
        public const string HttpClientName = "PageBuilderGitHubSetup";

        /// <summary>The site repositories' branch when configuration names none: GitHub's default for a new repository.</summary>
        public const string DefaultBaseBranch = "main";

        private const string ProtectionPurpose = "VirtoCommerce.PageBuilderModule.GitHubAppConnection";

        private readonly IOptionsFactory<GitContentOptions> _optionsFactory;
        private readonly ISettingsManager _settingsManager;
        private readonly IDataProtector _protector;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GitContentConnection> _logger;
        private readonly Lazy<State> _configured;
        private State _stored;

        public GitContentConnection(IOptionsFactory<GitContentOptions> optionsFactory, ISettingsManager settingsManager,
            IDataProtectionProvider dataProtectionProvider, IHttpClientFactory httpClientFactory, ILogger<GitContentConnection> logger)
        {
            _optionsFactory = optionsFactory;
            _settingsManager = settingsManager;
            _protector = dataProtectionProvider.CreateProtector(ProtectionPurpose);
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _configured = new Lazy<State>(() => new State(null, optionsFactory.Create(Options.DefaultName), null, null));
        }

        /// <summary>
        /// The connection in effect. Never throws: services read it in their constructors, and an unreadable
        /// App must leave Page Builder working without git rather than failing every request.
        /// </summary>
        public GitContentOptions Value => Current().Options;

        /// <summary>The environment's configuration holds a complete connection; the one-click setup is not used then.</summary>
        public bool FromConfiguration => _configured.Value.Options.IsConfigured;

        /// <summary>The one-click setup's App in effect; <c>null</c> when configuration wins or there is none.</summary>
        public GitHubAppConnection App => Current().App;

        /// <summary>Why the saved App cannot be used, in words an administrator can act on.</summary>
        public string Error => Current().Error;

        /// <summary>
        /// Finishes the one-click setup: GitHub hands over the App it has just created for
        /// <paramref name="organization"/> against the one-time <paramref name="code"/>, and the platform
        /// keeps it. Refuses an App that belongs to anyone else.
        /// </summary>
        public async Task<GitHubAppConnection> CreateAppAsync(string code, string organization, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(code);

            using var client = _httpClientFactory.CreateClient(HttpClientName);
            // the one GitHub call without a credential: the code itself proves the App was just made
            using var response = await client.PostAsync($"app-manifests/{Uri.EscapeDataString(code)}/conversions", content: null, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"GitHub did not hand the new App over ({(int)response.StatusCode} {response.ReasonPhrase}). " +
                    "Its link works once and only for an hour: start the setup again from the site.");
            }

            var created = JObject.Parse(body);
            var app = new GitHubAppConnection
            {
                Organization = created["owner"]?["login"]?.Value<string>(),
                AppId = created["id"]?.Value<string>(),
                AppSlug = created["slug"]?.Value<string>(),
                PrivateKey = created["pem"]?.Value<string>(),
            };

            if (!string.Equals(app.Organization, organization, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"The App was created for \"{app.Organization}\", not for \"{organization}\". Start the setup again from the site.");
            }

            if (string.IsNullOrWhiteSpace(app.AppId) || string.IsNullOrWhiteSpace(app.AppSlug) || string.IsNullOrWhiteSpace(app.PrivateKey))
            {
                throw new InvalidOperationException("GitHub's answer has no App id, name or key. Start the setup again from the site.");
            }

            await _settingsManager.SetValueAsync(ModuleConstants.Settings.GitHub.AppConnection.Name, _protector.Protect(JsonConvert.SerializeObject(app)));
            return app;
        }

        private State Current()
        {
            if (FromConfiguration)
            {
                return _configured.Value;
            }

            string stored;
            try
            {
                stored = _settingsManager.GetValue<string>(ModuleConstants.Settings.GitHub.AppConnection);
            }
            catch (Exception failure)
            {
                _logger.LogWarning(failure, "The saved GitHub connection could not be read.");
                return _configured.Value with { Error = "The saved GitHub connection could not be read. Try again in a moment." };
            }

            // read and decrypted again only when the setting changed (settings are cached and kept in step across servers)
            var state = _stored;
            if (state == null || !string.Equals(state.Stored, stored, StringComparison.Ordinal))
            {
                state = Read(stored);
                _stored = state;
            }

            return state;
        }

        private State Read(string stored)
        {
            if (string.IsNullOrWhiteSpace(stored))
            {
                return _configured.Value with { Stored = stored };
            }

            GitHubAppConnection app;
            try
            {
                app = JsonConvert.DeserializeObject<GitHubAppConnection>(_protector.Unprotect(stored));
            }
            catch (Exception failure) when (failure is CryptographicException or JsonException)
            {
                _logger.LogWarning(failure, "The saved GitHub connection cannot be decrypted on this server.");
                return new State(stored, _configured.Value.Options, null,
                    "The saved GitHub connection cannot be read on this server: it was made on another one, or this server's " +
                    "data protection keys have changed. Set it up again.");
            }

            // a fresh copy of the configuration (the tuning values stay), with the App's connection in it
            var options = _optionsFactory.Create(Options.DefaultName);
            options.Enabled = true;
            options.Repository = null;
            options.Organization = app.Organization;
            options.AppId = app.AppId;
            options.PrivateKey = app.PrivateKey;
            options.BaseBranch = string.IsNullOrWhiteSpace(options.BaseBranch) ? DefaultBaseBranch : options.BaseBranch;
            // what the setup promises: a new site gets its repository and history straight away
            options.AutoCreateRepositories = true;

            return new State(stored, options, app, null);
        }

        private sealed record State(string Stored, GitContentOptions Options, GitHubAppConnection App, string Error);
    }
}
