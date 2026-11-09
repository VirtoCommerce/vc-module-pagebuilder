using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

/// <summary>Runs the tests that wire the platform's static BackgroundJob facade one at a time, since each sets it for the process.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class StaticBackgroundJobCollection
{
    public const string Name = "Static BackgroundJob";
}
