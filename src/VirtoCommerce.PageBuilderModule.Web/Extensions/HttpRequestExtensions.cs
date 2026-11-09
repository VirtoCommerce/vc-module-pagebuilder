using System;
using Microsoft.AspNetCore.Http;
using VirtoCommerce.PageBuilderModule.Core;
using VirtoCommerce.PageBuilderModule.Data.Validation;

namespace VirtoCommerce.PageBuilderModule.Web.Extensions;

public static class HttpRequestExtensions
{
    /// <summary>Returns <c>null</c> when the request sends none or a malformed one.</summary>
    public static string GetSaveId(this HttpRequest request)
    {
        var value = request?.Headers[ModuleConstants.Headers.SaveId].ToString();
        return PageBuilderContentPaths.IsSaveId(value) ? value : null;
    }

    public static string GetOrCreateSaveId(this HttpRequest request) => request.GetSaveId() ?? Guid.NewGuid().ToString("N");
}
