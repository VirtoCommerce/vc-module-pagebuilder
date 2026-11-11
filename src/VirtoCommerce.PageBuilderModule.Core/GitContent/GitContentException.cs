using System;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

public class GitContentException : Exception
{
    public GitContentException(string message)
        : base(message)
    {
    }

    public GitContentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public static GitContentException RepositoryUnreachable(string repository) =>
        new($"The GitHub repository \"{repository}\" does not exist, or the platform has no access to it.");
}
