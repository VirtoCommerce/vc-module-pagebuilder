using System.Security.Claims;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

public static class GitContentOptionsExtensions
{
    public static GitCommitAuthor GetCommitAuthor(this GitContentOptions options, ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(options);

        var userName = user?.Identity?.Name;

        var author = AbstractTypeFactory<GitCommitAuthor>.TryCreateInstance();
        author.Name = string.IsNullOrEmpty(userName) ? options.FallbackAuthorName : userName;
        author.Email = user?.FindFirstValue(ClaimTypes.Email) ?? options.FallbackAuthorEmail;
        return author;
    }

    /// <summary>Path and branch come from the same content path, so a page and a blog article that share a relative url stay two files on two branches.</summary>
    public static GitPageFileLocation GetPageLocation(this GitContentOptions options, string repository, string contentType, string path, string userName)
    {
        ArgumentNullException.ThrowIfNull(options);

        var contentPath = GitPageLocation.ContentPath(contentType, path);

        return new GitPageFileLocation(
            repository,
            GitPageLocation.RepoPath(options.PagesRoot, contentPath),
            GitPageLocation.BranchFor(options.BranchTemplate, userName, contentPath));
    }
}
