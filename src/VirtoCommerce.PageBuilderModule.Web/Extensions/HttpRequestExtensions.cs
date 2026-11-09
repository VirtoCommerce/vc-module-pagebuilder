using System;
using Microsoft.AspNetCore.Http;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Data.Validation;

namespace VirtoCommerce.PageBuilderModule.Web.Extensions;

public static class HttpRequestExtensions
{
    /// <summary>Gets the id of the designer save the request belongs to; <c>null</c> when it sends none or a malformed one.</summary>
    public static string GetSaveId(this HttpRequest request)
    {
        var value = request?.Headers[ModuleConstants.Headers.SaveId].ToString();
        return PageBuilderContentPaths.IsSaveId(value) ? value : null;
    }

    /// <summary>Gets the id of the designer save the request belongs to, or a new one.</summary>
    public static string GetOrCreateSaveId(this HttpRequest request) => request.GetSaveId() ?? Guid.NewGuid().ToString("N");
}
