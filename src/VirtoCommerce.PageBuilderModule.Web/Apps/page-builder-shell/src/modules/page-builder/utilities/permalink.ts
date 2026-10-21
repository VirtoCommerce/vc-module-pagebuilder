// Mirrors PageBuilderPermalink.IsValid on the server: the permalink is opened as written, so '?' and '#' would
// start the query and the fragment, '&' separates query parameters and whitespace must be encoded.
// Unicode letters and percent-encoded sequences are allowed.
const forbiddenCharacters = /[\s\p{Cc}?#&\\]/u;

export function isValidPermalink(value: string | null | undefined): boolean {
  if (!value) {
    return true;
  }

  if (forbiddenCharacters.test(value) || value.includes("//")) {
    return false;
  }

  return !value.split("/").some((segment) => segment === "." || segment === "..");
}
