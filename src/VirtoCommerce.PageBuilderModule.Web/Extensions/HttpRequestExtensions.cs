using System;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using VirtoCommerce.PageBuilderModule.Core;

namespace VirtoCommerce.PageBuilderModule.Web.Extensions;

public static class HttpRequestExtensions
{
    // the id goes into a commit trailer
    private static readonly Regex SaveIdRegex = new("^[A-Za-z0-9-]{8,64}$", RegexOptions.None, TimeSpan.FromSeconds(1));

    /// <summary>Gets the id of the designer save the request belongs to; <c>null</c> when it sends none or a malformed one.</summary>
    public static string GetSaveId(this HttpRequest request)
    {
        var value = request?.Headers[ModuleConstants.Headers.SaveId].ToString();
        return !string.IsNullOrEmpty(value) && SaveIdRegex.IsMatch(value) ? value : null;
    }
}
