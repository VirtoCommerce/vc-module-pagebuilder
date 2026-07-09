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
