namespace VirtoCommerce.PageBuilderModule.Core.Models;

/// <summary>
/// Outcome of a batch action (Publish All, Archive All) over the language versions of a <see cref="MultilanguagePage"/>.
/// </summary>
public class MultilanguagePageBatchResult
{
    public const string NoDraftReason = "NoDraft";
    public const string AlreadyArchivedReason = "AlreadyArchived";
    public const string FailedReason = "Failed";

    public IList<string> ProcessedCultureNames { get; set; } = [];

    public IList<MultilanguagePageBatchSkip> Skipped { get; set; } = [];
}

public class MultilanguagePageBatchSkip
{
    public string CultureName { get; set; }

    public string Reason { get; set; }
}
