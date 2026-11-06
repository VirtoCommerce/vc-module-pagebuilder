using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>Source of the GitHub connection.</summary>
[JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
public enum GitHubConnectionSource
{
    /// <summary>The <see cref="GitContentOptions"/> configuration, which wins when it is complete.</summary>
    Configuration,

    /// <summary>The GitHub App of the one-click setup.</summary>
    App,
}
