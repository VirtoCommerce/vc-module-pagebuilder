using VirtoCommerce.PageBuilderModule.Core.GitContent;
using Xunit;

namespace VirtoCommerce.PageBuilderModule.Tests
{
    public class GitPageLocationTests
    {
        private const string Template = "designer/{user}/{slug}";

        [Theory]
        [InlineData("pages", "docs/foo.page", "pages/docs/foo.page")]
        [InlineData("pages/", "/docs/foo.page", "pages/docs/foo.page")]
        [InlineData("pages", "docs\\foo.page", "pages/docs/foo.page")]
        public void RepoPath_is_the_page_path_under_the_pages_root(string root, string page, string expected)
        {
            Assert.Equal(expected, GitPageLocation.RepoPath(root, page));
        }

        [Theory]
        [InlineData("/foo.page-draft", "pages/foo.page")]
        [InlineData("/docs/foo.page-draft", "pages/docs/foo.page")]
        [InlineData("/docs/foo.page-DRAFT", "pages/docs/foo.page")]
        public void RepoPath_drops_the_blob_draft_suffix(string page, string expected)
        {
            // the blade and the designer open a draft by its "-draft" blob name; in git that is the same
            // file on a work branch, and production only ever holds the canonical page
            Assert.Equal(expected, GitPageLocation.RepoPath("pages", page));
        }

        [Fact]
        public void BranchFor_is_the_same_branch_for_a_page_and_its_draft_path()
        {
            Assert.Equal(
                GitPageLocation.BranchFor(Template, "john", "docs/foo.page"),
                GitPageLocation.BranchFor(Template, "john", "/docs/foo.page-draft"));
        }

        [Theory]
        [InlineData("pages", "/docs/foo.page", "docs/foo.page")]
        [InlineData("blogs", "/assets/foo.page", "blogs/assets/foo.page")]
        [InlineData("Blogs", "/assets/foo.page-draft", "blogs/assets/foo.page")]
        // a blogs path that already carries the folder means the same file, not blogs/blogs/...
        [InlineData("blogs", "/blogs/assets/foo.page", "blogs/assets/foo.page")]
        // the same file reached through the pages root: one page, one content path
        [InlineData("pages", "/blogs/assets/foo.page", "blogs/assets/foo.page")]
        [InlineData(null, "/foo.page", "foo.page")]
        public void ContentPath_puts_a_blog_article_back_under_the_blogs_folder(string contentType, string page, string expected)
        {
            Assert.Equal(expected, GitPageLocation.ContentPath(contentType, page));
        }

        [Fact]
        public void ContentPath_separates_a_page_from_a_blog_article_of_the_same_name()
        {
            // "/foo.page" is Pages/{store}/foo.page as a page and Pages/{store}/blogs/foo.page as a blog
            // article: two files, and therefore two branches
            var page = GitPageLocation.ContentPath("pages", "/foo.page");
            var article = GitPageLocation.ContentPath("blogs", "/foo.page");

            Assert.NotEqual(page, article);
            Assert.NotEqual(
                GitPageLocation.BranchFor(Template, "john", page),
                GitPageLocation.BranchFor(Template, "john", article));
        }

        [Theory]
        [InlineData("pages", true)]
        [InlineData("blogs", true)]
        [InlineData("BLOGS", true)]
        [InlineData("themes", false)]
        [InlineData(null, false)]
        public void IsPageContent_covers_pages_and_blogs_only(string contentType, bool expected)
        {
            Assert.Equal(expected, GitPageLocation.IsPageContent(contentType));
        }

        [Fact]
        public void BranchFor_flattens_the_page_path_instead_of_nesting_it()
        {
            // git refuses to keep "designer/john/industry" and "designer/john/industry/manufacturing"
            // side by side: a ref cannot be both a file and a directory
            var branch = GitPageLocation.BranchFor(Template, "john", "industry/manufacturing.page");

            Assert.StartsWith("designer/john/", branch);
            Assert.DoesNotContain("/", branch["designer/john/".Length..]);
            Assert.Contains("industry-manufacturing.page", branch);
        }

        [Fact]
        public void BranchFor_separates_paths_that_flatten_to_the_same_slug()
        {
            var nested = GitPageLocation.BranchFor(Template, "john", "a/b.page");
            var flat = GitPageLocation.BranchFor(Template, "john", "a-b.page");

            Assert.NotEqual(nested, flat);
        }

        [Fact]
        public void BranchFor_is_stable_for_the_same_page()
        {
            Assert.Equal(
                GitPageLocation.BranchFor(Template, "john", "docs/foo.page"),
                GitPageLocation.BranchFor(Template, "john", "/docs/foo.page"));
        }

        [Fact]
        public void BranchFor_separates_editors()
        {
            Assert.NotEqual(
                GitPageLocation.BranchFor(Template, "john", "docs/foo.page"),
                GitPageLocation.BranchFor(Template, "jane", "docs/foo.page"));
        }

        [Fact]
        public void BranchFor_keeps_a_very_long_path_within_a_usable_ref()
        {
            var branch = GitPageLocation.BranchFor(Template, "john", new string('a', 300) + ".page");

            Assert.True(branch.Length < 120, $"branch was {branch.Length} characters: {branch}");
        }

        [Fact]
        public void BranchFor_without_a_resolvable_user_says_so_rather_than_producing_a_broken_ref()
        {
            // an api key that does not resolve to a platform user lands everyone here — visibly
            Assert.StartsWith("designer/unknown/", GitPageLocation.BranchFor(Template, null, "foo.page"));
        }

        [Theory]
        [InlineData("John Doe", "john-doe")]
        [InlineData("john@example.com", "john-example.com")]
        [InlineData("  ", null)]
        [InlineData(null, null)]
        [InlineData("---", null)]
        public void SanitizeRefComponent_produces_something_git_accepts(string input, string expected)
        {
            Assert.Equal(expected, GitPageLocation.SanitizeRefComponent(input));
        }
    }
}
