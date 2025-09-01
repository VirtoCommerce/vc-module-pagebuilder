using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Data.Extensions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using static VirtoCommerce.Platform.Data.Infrastructure.DbContextBase;

namespace VirtoCommerce.PageBuilderModule.Data.Models
{
    public class GroupedPageBuilderPageEntity : AuditableEntity, IDataEntity<GroupedPageBuilderPageEntity, GroupedPageBuilderPage>
    {
        [StringLength(IdLength)]
        public string StoreId { get; set; }

        public virtual ObservableCollection<PageBuilderPageEntity> Pages { get; set; } = new NullCollection<PageBuilderPageEntity>();

        //[StringLength(Length128)]
        //public string Status { get; set; }

        public GroupedPageBuilderPage ToModel(GroupedPageBuilderPage model)
        {
            model.Id = Id;
            model.CreatedBy = CreatedBy;
            model.CreatedDate = CreatedDate;
            model.ModifiedBy = ModifiedBy;
            model.ModifiedDate = ModifiedDate;
            model.StoreId = StoreId;

            model.Pages = Pages.Select(x => x.ToModel(AbstractTypeFactory<PageBuilderPage>.TryCreateInstance())).ToList();

            model.ApplyForView();

            return model;
        }

        public GroupedPageBuilderPageEntity FromModel(GroupedPageBuilderPage model, PrimaryKeyResolvingMap pkMap)
        {
            pkMap.AddPair(model, this);

            Id = model.Id;
            CreatedBy = model.CreatedBy;
            CreatedDate = model.CreatedDate;
            ModifiedBy = model.ModifiedBy;
            ModifiedDate = model.ModifiedDate;

            StoreId = model.StoreId;

            if (model.Pages != null)
            {
                Pages = new ObservableCollection<PageBuilderPageEntity>(model.Pages.Select(x => AbstractTypeFactory<PageBuilderPageEntity>.TryCreateInstance().FromModel(x, pkMap)));
            }

            return this;
        }

        public void Patch(GroupedPageBuilderPageEntity target)
        {
            target.StoreId = StoreId;

            if (!Pages.IsNullCollection())
            {
                Pages.Patch(target.Pages, (sourcePage, targetPage) => sourcePage.Patch(targetPage));
            }
        }
    }
}
