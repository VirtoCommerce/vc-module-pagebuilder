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
}
