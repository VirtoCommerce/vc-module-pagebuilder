namespace VirtoCommerce.PageBuilderModule.Core.GitContent;

/// <summary>A page file in a store repository: the repository ("owner/name"), the file path in it and the editor's branch.</summary>
public sealed record GitPageFileLocation(string Repository, string RepoPath, string Branch);
