// Mirrors PageBuilderPermalink.IsValid on the server: '?', '#', '&' and '%' would start the query, the fragment,
// a query parameter or an escape sequence, so a permalink could not be opened as written; the other characters
// are not allowed unencoded in a URL path, and whitespace, control and formatting characters cannot be seen.
// Letters of any language are allowed as they are.
const forbiddenCharacters = /[\s\p{Cc}\p{Cf}?#&%\\"<>{}|^`]/u;
const maxLength = 2048;

export function isValidPermalink(value: string | null | undefined): boolean {
  if (!value) {
    return true;
  }

  if (value.length > maxLength || forbiddenCharacters.test(value) || value.includes("//")) {
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
