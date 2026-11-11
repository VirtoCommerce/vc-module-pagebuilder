// Mirrors the server's permalink check: these characters break a URL as written or cannot be seen.
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

// Like the server, this accepts a stored permalink: an old one must not block saving the page's other fields.
export function getPermalinkRules(
  permalink: string | null | undefined,
  storedPermalink: string | null | undefined,
): string {
  return storedPermalink != null && permalink === storedPermalink ? "required" : "required|permalink";
}
