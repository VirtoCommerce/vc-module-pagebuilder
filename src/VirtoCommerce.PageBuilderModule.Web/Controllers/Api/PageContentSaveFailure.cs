using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.PageBuilderModule.Core.Models;

namespace VirtoCommerce.PageBuilderModule.Web.Controllers.Api;

/// <summary>Why a page's content was not saved, as the caller is told it: none of these is the server's fault.</summary>
internal static class PageContentSaveFailure
{
    /// <summary>The answer to <paramref name="failure"/>; <c>null</c> when it is not one of a save's refusals.</summary>
    public static ActionResult AnswerTo(ControllerBase controller, Exception failure) => failure switch
    {
        // somebody saved the page since it was read
        PageBuilderContentConflictException conflict => controller.StatusCode(StatusCodes.Status412PreconditionFailed, conflict.Message),
        KeyNotFoundException => controller.NotFound(),
        // the content names something that is not there, such as a deleted shared component
        InvalidDataException invalid => controller.BadRequest(invalid.Message),
        _ => null,
    };
}
