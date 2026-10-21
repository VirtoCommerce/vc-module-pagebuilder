// Mirrors PageBuilderPermalink.IsValid on the server: the permalink is opened as written, so '?' and '#' would
// start the query and the fragment, '&' separates query parameters and whitespace must be encoded.
// Letters of any language and '%' are allowed; percent-escape sequences are not checked.
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

// Like the server, leave a stored permalink as it is: one saved before the rule existed must not block
// saving or publishing the page's other fields.
export function getPermalinkRules(
  permalink: string | null | undefined,
  storedPermalink: string | null | undefined,
): string {
  return storedPermalink != null && permalink === storedPermalink ? "required" : "required|permalink";
}
