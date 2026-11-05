using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.PageBuilderModule.Core.Models;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests;

/// <summary>A save of a page's content that does not go through, as a save and a restore both answer it.</summary>
public class PageContentSaveFailureTests
{
    private readonly ControllerBase _controller = new Answers();

    [Fact]
    public void Somebody_saving_the_page_since_it_was_read_is_a_failed_precondition()
    {
        var answer = Assert.IsType<ObjectResult>(PageContentSaveFailure.AnswerTo(_controller, new PageBuilderContentConflictException()));

        Assert.Equal(412, answer.StatusCode);
    }

    [Fact]
    public void A_page_that_is_gone_is_not_found()
    {
        Assert.IsType<NotFoundResult>(PageContentSaveFailure.AnswerTo(_controller, new KeyNotFoundException()));
    }

    [Fact]
    public void Content_naming_what_is_not_there_is_a_bad_request()
    {
        var answer = Assert.IsType<BadRequestObjectResult>(PageContentSaveFailure.AnswerTo(_controller, new InvalidDataException("Shared Component 'x' was not found.")));

        Assert.Equal("Shared Component 'x' was not found.", answer.Value);
    }

    [Fact]
    public void Any_other_failure_is_left_to_the_server()
    {
        Assert.Null(PageContentSaveFailure.AnswerTo(_controller, new InvalidOperationException()));
    }

    private sealed class Answers : ControllerBase;
}
