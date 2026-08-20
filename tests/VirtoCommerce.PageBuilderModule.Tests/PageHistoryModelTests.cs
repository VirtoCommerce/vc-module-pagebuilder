using System;
using System.Linq;
using VirtoCommerce.PageBuilderModule.Core.GitContent;
using VirtoCommerce.PageBuilderModule.Web.Controllers.Api;
using VirtoCommerce.PageBuilderModule.Web.Models;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    /// <summary>
    /// What the history endpoint adds to the repository's answer: whose version each one is, and how many
    /// of them are somebody else's unpublished work.
    /// </summary>
    public class PageHistoryModelTests
    {
        private const string MyBranch = "designer/john/about-us-1a2b3c4";

        [Fact]
        public void Mine_IsAVersionOnMyOwnWorkBranch()
        {
            var history = History(
                Version("aaa", branches: [MyBranch]),
                Version("bbb", branches: ["designer/kate/about-us-1a2b3c4"]));

            var model = PageHistoryModel.From(history, MyBranch);

            Assert.True(model.Versions.Single(version => version.Sha == "aaa").Mine);
            Assert.False(model.Versions.Single(version => version.Sha == "bbb").Mine);
        }

        [Fact]
        public void Mine_ACommitSharedWithMyBranch_IsStillMine()
        {
            // branches are cut from one another, so a commit can sit on several at once; if one of them is
            // mine, continuing from it is not adopting somebody else's work
            var history = History(Version("aaa", branches: ["content/hero-copy", MyBranch]));

            Assert.True(PageHistoryModel.From(history, MyBranch).Versions.Single().Mine);
        }

        [Fact]
        public void Mine_IsFalseForAnEditMadeOutsideTheBuilder()
        {
            // the identity increment is what will mark these mine — until then a content/* commit is
            // somebody's, and the server will not guess whose
            var history = History(Version("aaa", branches: ["content/hero-copy"]));

            Assert.False(PageHistoryModel.From(history, MyBranch).Versions.Single().Mine);
        }

        [Fact]
        public void OtherDraftCount_CountsUnpublishedVersionsThatAreNotMine()
        {
            var history = History(
                Version("published", branches: ["master"], published: true),
                Version("mine", branches: [MyBranch]),
                Version("theirs", branches: ["content/hero-copy"]),
                Version("also-theirs", branches: ["designer/kate/about-us-1a2b3c4"]));

            Assert.Equal(2, PageHistoryModel.From(history, MyBranch).OtherDraftCount);
        }

        [Fact]
        public void OtherDraftCount_IgnoresBulkCommits()
        {
            // measured on the content repository: the only unpublished "version" of several pages was a
            // 366-file import. Counting it would put a badge on every page in the repository.
            var history = History(
                Version("import", branches: ["content/seed"], bulk: true, changedFiles: 366),
                Version("theirs", branches: ["content/hero-copy"]));

            var model = PageHistoryModel.From(history, MyBranch);

            Assert.Equal(1, model.OtherDraftCount);
            Assert.True(model.Versions.Single(version => version.Sha == "import").Bulk);
        }

        [Fact]
        public void From_CarriesTruncationAndTheBranchOfMyDraft()
        {
            var history = new GitPageHistory { Versions = [], Truncated = true, EndCursor = "cursor" };

            var model = PageHistoryModel.From(history, MyBranch);

            Assert.True(model.Truncated);
            Assert.Equal("cursor", model.EndCursor);
            Assert.Equal(MyBranch, model.MyDraftBranch);
        }

        [Fact]
        public void From_PassesAuthorshipThrough()
        {
            var history = History(new GitPageVersion
            {
                Sha = "70a3c34b5c6d7e8f90a1b2c3d4e5f60718293a4b",
                Date = new DateTime(2026, 8, 11, 14, 2, 42, DateTimeKind.Utc),
                Message = "Epicor: publish current constructor draft",
                AuthorName = "Svetlana Tumanova",
                AuthorEmail = "svetlana.tumanova@virtoworks.com",
                AuthorLogin = "svetlana-virto",
                VcUser = "svetlana.tumanova@virtoworks.com",
                Branches = ["content/hero-copy"],
            });

            var version = PageHistoryModel.From(history, MyBranch).Versions.Single();

            Assert.Equal("70a3c34", version.ShortSha);
            Assert.Equal("Svetlana Tumanova", version.Author.Name);
            Assert.Equal("svetlana-virto", version.Author.Login);
            Assert.Equal("svetlana.tumanova@virtoworks.com", version.VcUser);
        }

        [Fact]
        public void Empty_IsAnAnswerRatherThanAFailure()
        {
            // a page the designer is about to create has no versions and no branch of its own yet
            Assert.Empty(PageHistoryModel.Empty.Versions);
            Assert.Equal(0, PageHistoryModel.Empty.OtherDraftCount);
            Assert.False(PageHistoryModel.Empty.Truncated);
        }

        [Fact]
        public void GitBuilderDescriptors_OfferHistoryPreviewAndRestore()
        {
            var descriptors = PageBuilderController.GitBuilderDescriptors();

            var history = descriptors["history"];
            Assert.NotNull(history);
            Assert.Contains("/api/pagebuilder/git/history?", (string)history["url"]);
            // the panel substitutes the row's sha into both, so the placeholder has to survive into the config
            Assert.Contains("ref={{sha}}", (string)history["preview"]["url"]);
            Assert.Contains("sha={{sha}}", (string)history["restore"]["url"]);
            Assert.Equal("POST", (string)history["restore"]["method"]);
        }

        [Fact]
        public void GitBuilderDescriptors_OfferUnpublish()
        {
            // Taking a page down means deleting it from the production branch — still a merge, so it is
            // offered next to publish. The toolbar shows the button precisely because the descriptor is
            // there, so withholding it is how a store that cannot unpublish is expressed.
            var unpublish = PageBuilderController.GitBuilderDescriptors()["publish"]!["unpublish"];

            Assert.NotNull(unpublish);
            Assert.Contains("/api/pagebuilder/git/unpublish?", (string)unpublish["url"]);
            Assert.Equal("POST", (string)unpublish["method"]);
        }

        [Fact]
        public void GitBuilderDescriptors_CarryTheContentTypeIntoEveryPublishUrl()
        {
            // pages and blogs are the same kind of file in two folders, so a url without the type is one
            // the server cannot answer
            var publish = PageBuilderController.GitBuilderDescriptors()["publish"];

            Assert.Contains("type={{type}}", (string)publish["status"]);
            Assert.Contains("type={{type}}", (string)publish["publish"]["url"]);
            Assert.Contains("type={{type}}", (string)publish["unpublish"]["url"]);
        }

        private static GitPageHistory History(params GitPageVersion[] versions) => new() { Versions = versions };

        private static GitPageVersion Version(string sha, string[] branches, bool published = false, bool bulk = false, int? changedFiles = 1) => new()
        {
            Sha = sha,
            Date = new DateTime(2026, 8, 11, 14, 40, 10, DateTimeKind.Utc),
            Message = "designer: save /about-us.page",
            Branches = branches,
            Published = published,
            Bulk = bulk,
            ChangedFiles = changedFiles,
        };
    }

    public class GitCommitMessageTests
    {
        [Fact]
        public void WithVcUser_RecordsTheLoginUnderTheSummary()
        {
            var message = GitCommitMessage.WithVcUser("designer: save /about-us.page (store: vccom, by: john)", "john");

            Assert.Equal("designer: save /about-us.page (store: vccom, by: john)\n\nVC-User: john", message);
            // and it reads back out of the body, which is how the version list attributes it
            Assert.Equal("john", GitCommitMessage.ReadTrailer(message, GitCommitMessage.VcUserTrailer));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void WithVcUser_NoLogin_LeavesTheMessageAlone(string vcUser)
        {
            // an empty trailer would look like an attribution while saying nothing
            Assert.Equal("publish /about-us.page", GitCommitMessage.WithVcUser("publish /about-us.page", vcUser));
        }

        [Fact]
        public void ReadTrailer_CommitFromOutsideTheModule_HasNone()
        {
            Assert.Null(GitCommitMessage.ReadTrailer("Epicor bento: wire CTAs via native button", GitCommitMessage.VcUserTrailer));
            Assert.Null(GitCommitMessage.ReadTrailer(null, GitCommitMessage.VcUserTrailer));
        }

        [Fact]
        public void ReadTrailer_IgnoresAnEmptyValueAndTakesTheLastOne()
        {
            // an amend or a rebase can leave an older trailer above the current one
            Assert.Equal("kate", GitCommitMessage.ReadTrailer("summary\n\nVC-User: john\nVC-User: kate", GitCommitMessage.VcUserTrailer));
            Assert.Null(GitCommitMessage.ReadTrailer("summary\n\nVC-User:", GitCommitMessage.VcUserTrailer));
        }

        [Fact]
        public void ReadTrailer_ToleratesWindowsLineEndings()
        {
            Assert.Equal("john", GitCommitMessage.ReadTrailer("summary\r\n\r\nVC-User: john\r\n", GitCommitMessage.VcUserTrailer));
        }
    }
}
