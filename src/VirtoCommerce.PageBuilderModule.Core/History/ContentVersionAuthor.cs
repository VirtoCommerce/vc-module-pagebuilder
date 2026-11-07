namespace VirtoCommerce.PageBuilderModule.Core.History;

public class ContentVersionAuthor
{
    /// <summary>The platform login, else the commit author's name.</summary>
    public string Name { get; set; }

    public string Login { get; set; }

    public string Email { get; set; }
}
