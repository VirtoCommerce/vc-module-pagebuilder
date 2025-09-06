using Newtonsoft.Json;
using VirtoCommerce.Platform.Core.Common;
using static VirtoCommerce.PageBuilderModule.Core.ModuleConstants.PageStatuses;

namespace VirtoCommerce.PageBuilderModule.Core.Models;

public class GroupedPageBuilderPage : AuditableEntity, IHasStoreId, ICloneable
{
    public string StoreId { get; set; }

    [JsonIgnore]
    public IList<PageBuilderPage> Pages { get; set; } = [];

    public string CultureName => _currentPage?.CultureName;

    public string Name => _currentPage?.Name;

    public string Permalink => _currentPage?.Permalink;

    public string Status => _publishedPage != null ? Published
        : _draftPage != null ? Draft
        : _currentPage != null ? Archived : null;

    public bool Visibility => _currentPage?.Visibility ?? false;
    public string UserGroups => _currentPage?.UserGroups;
    public DateTime? StartDate => _currentPage?.StartDate;
    public DateTime? EndDate => _currentPage?.EndDate;

    [JsonIgnore]
    public PageBuilderPage CurrentPage => _currentPage;
    [JsonIgnore]
    public PageBuilderPage DraftPage => _draftPage;
    [JsonIgnore]
    public PageBuilderPage PublishedPage => _publishedPage;

    public bool HasChanges => _draftPage != null;

    private PageBuilderPage _draftPage;
    private PageBuilderPage _publishedPage;
    private PageBuilderPage _currentPage;

    public void PrepareData(bool edit = false)
    {
        _draftPage = Pages?.FirstOrDefault(x => x.Status == Draft);
        _publishedPage = Pages?.FirstOrDefault(x => x.Status == Published);
        _currentPage = edit
            ? _draftPage ?? _publishedPage
            : _publishedPage ?? _draftPage;
        if (_currentPage == null)
        {
            _currentPage = Pages?.MaxBy(x => x.ModifiedDate);
        }
    }

    public object Clone()
    {
        return MemberwiseClone();
    }
}
