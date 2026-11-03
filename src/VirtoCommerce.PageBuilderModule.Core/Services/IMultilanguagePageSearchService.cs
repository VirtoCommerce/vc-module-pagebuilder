using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.GenericCrud;

namespace VirtoCommerce.PageBuilderModule.Core.Services;

public interface IMultilanguagePageSearchService
    : ISearchService<MultilanguagePageSearchCriteria, MultilanguagePageSearchResult, MultilanguagePage>;
