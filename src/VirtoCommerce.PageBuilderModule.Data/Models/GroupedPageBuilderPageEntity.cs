using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using static VirtoCommerce.Platform.Data.Infrastructure.DbContextBase;

namespace VirtoCommerce.PageBuilderModule.Data.Models
{
    public class GroupedPageBuilderPageEntity : AuditableEntity, IDataEntity<GroupedPageBuilderPageEntity, GroupedPageBuilderPage>
    {
        [StringLength(IdLength)]
        public string StoreId { get; set; }

        [StringLength(CultureNameLength)]
        public string CultureName { get; set; }

        [StringLength(IdLength)]
        public string OrganizationId { get; set; }

        [StringLength(1024)]
        public string Name { get; set; }

        [StringLength(2048)]
        public string Permalink { get; set; }

        public bool Visibility { get; set; }

        [StringLength(Length1024)]
        public string UserGroups { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public virtual ObservableCollection<PageBuilderPageEntity> Pages { get; set; } = new NullCollection<PageBuilderPageEntity>();

        public GroupedPageBuilderPage ToModel(GroupedPageBuilderPage model)
        {
            model.Id = Id;
            model.CreatedBy = CreatedBy;
            model.CreatedDate = CreatedDate;
            model.ModifiedBy = ModifiedBy;
            model.ModifiedDate = ModifiedDate;
            model.StoreId = StoreId;
            model.OrganizationId = OrganizationId;

            model.CultureName = CultureName;
            model.Name = Name;
            model.Permalink = Permalink;
            model.Visibility = Visibility;
            model.UserGroups = UserGroups;
            model.StartDate = StartDate;
            model.EndDate = EndDate;

            model.Pages = Pages.Select(x => x.ToModel(AbstractTypeFactory<PageBuilderPage>.TryCreateInstance())).ToList();

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
            CultureName = model.CultureName;
            Name = model.Name;
            Permalink = model.Permalink;
            OrganizationId = model.OrganizationId;

            Visibility = model.Visibility;
            UserGroups = model.UserGroups;
            StartDate = model.StartDate;
            EndDate = model.EndDate;

            if (model.Pages != null)
            {
                Pages = new ObservableCollection<PageBuilderPageEntity>(model.Pages.Select(x => AbstractTypeFactory<PageBuilderPageEntity>.TryCreateInstance().FromModel(x, pkMap)));
            }

            return this;
        }

        public void Patch(GroupedPageBuilderPageEntity target)
        {
            target.StoreId = StoreId;
            target.CultureName = CultureName;
            target.Name = Name;
            target.Permalink = Permalink;

            target.Visibility = Visibility;
            target.UserGroups = UserGroups;
            target.StartDate = StartDate;
            target.EndDate = EndDate;
            target.OrganizationId = OrganizationId;

            if (!Pages.IsNullCollection())
            {
                Pages.Patch(target.Pages, (sourcePage, targetPage) => sourcePage.Patch(targetPage));
            }
        }
    }
}
