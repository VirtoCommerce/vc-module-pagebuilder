using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Core.Services;

namespace VirtoCommerce.PageBuilderModule.Web.Services;

public class GroupedPageContentWriter(PageBuilderPageContentService pageContentService) : IGroupedPageContentWriter
{
    public virtual Task<PageBuilderConditionalContentWriteResult> SaveContentAsync(GroupedPageBuilderPage group, string content, string expectedETag,
        CancellationToken cancellationToken = default) =>
        pageContentService.SaveConditionalContentAsync(group, content, expectedETag, cancellationToken);
}
