using Microsoft.AspNetCore.Authorization;

namespace VirtoCommerce.PageBuilderModule.Data.Authorization
{
    public sealed class PageBuilderAuthorizationRequirement : IAuthorizationRequirement
    {
        /// <summary>
        /// Lets through a user who is not bound to any store: a back-office manager usually has no store
        /// on the account, and the action is still guarded by its permission. A user bound to a store
        /// stays limited to that store.
        /// </summary>
        public bool AllowUsersWithoutStore { get; init; }
    }
}
