using System.Text.RegularExpressions;
using FluentValidation;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Data.Validation;

public class GitHubAppSetupRequestValidator : AbstractValidator<GitHubAppSetupRequest>
{
    // a GitHub account name: it goes into the address of GitHub's form
    private static readonly Regex OrganizationPattern = new("^[A-Za-z0-9][A-Za-z0-9-]{0,38}$", RegexOptions.None, TimeSpan.FromSeconds(1));

    public GitHubAppSetupRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Organization)
            .NotEmpty()
            .Matches(OrganizationPattern)
            .WithMessage("Organization \"{PropertyValue}\" is not a GitHub organization name.");
    }
}
