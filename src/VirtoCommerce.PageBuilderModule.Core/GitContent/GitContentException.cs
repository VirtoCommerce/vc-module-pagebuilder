using System;

namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>Thrown when GitHub fails a git content request or the git content setup cannot be used.</summary>
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
}
