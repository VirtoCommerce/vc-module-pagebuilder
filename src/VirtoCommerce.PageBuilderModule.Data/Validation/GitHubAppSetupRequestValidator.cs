using System.Text.RegularExpressions;
using FluentValidation;
using VirtoCommerce.PageBuilderModule.Core.GitContent;

namespace VirtoCommerce.PageBuilderModule.Data.Validation;

public class GitHubAppSetupRequestValidator : AbstractValidator<GitHubAppSetupRequest>
{
    // A GitHub account name: it ends up in the address of GitHub's form.
    private static readonly Regex OrganizationPattern = new("^[A-Za-z0-9][A-Za-z0-9-]{0,38}$", RegexOptions.None, TimeSpan.FromSeconds(1));

    public GitHubAppSetupRequestValidator()
    {
        RuleFor(x => x.Organization)
            .Must(organization => !string.IsNullOrEmpty(organization) && OrganizationPattern.IsMatch(organization))
            .WithMessage("Enter the name of a GitHub organization, as in github.com/<organization>.");
    }
}
