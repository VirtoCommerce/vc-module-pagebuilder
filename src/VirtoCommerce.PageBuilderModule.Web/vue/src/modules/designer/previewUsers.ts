// Preview as a customer, as the Angular designer does (shared/components/impersonate-as): the store setting
// PreviewUserIds lists the accounts, the platform's impersonate grant gives an access token for one, and the preview
// gets it in an `auth` message. The grant needs the platform's login-on-behalf permission.

/** What the platform requires of the editor for the impersonate grant */
export const IMPERSONATE_PERMISSION = "platform:security:loginOnBehalf";
/** The store setting listing the accounts to preview as, one id per line */
export const PREVIEW_USERS_SETTING = "VirtoCommerce.PageBuilderModule.Store.PreviewUserIds";

export interface PreviewUser {
  id: string;
  name: string;
}

/** The account ids the store lists, in order, each once */
export function previewUserIds(store: { settings?: { name?: string; value?: unknown }[] } | undefined): string[] {
  const value = store?.settings?.find((x) => x.name === PREVIEW_USERS_SETTING)?.value;
  if (typeof value !== "string") return [];
  return [...new Set(value.split(/\r?\n/).map((x) => x.trim()).filter(Boolean))];
}

/** The accounts by the ids, named as the platform knows them; an id the search did not return keeps its id as its name */
export function previewUsers(ids: string[], found: { id?: string; userName?: string; email?: string }[]): PreviewUser[] {
  return ids.map((id) => {
    const user = found.find((x) => x.id === id);
    return { id, name: user?.userName || user?.email || id };
  });
}

/** The token request's body: no `offline_access`, so no refresh token is issued for the preview */
export const impersonateBody = (userId: string) => new URLSearchParams({ grant_type: "impersonate", user_id: userId }).toString();

export interface PreviewToken {
  userId: string;
  accessToken: string;
  /** ms since the epoch */
  expiresAt: number;
}

/** A token is asked for anew a minute before it expires */
export const TOKEN_MARGIN_MS = 60_000;

/** The token for the user while it has a minute left */
export const usableToken = (token: PreviewToken | undefined, userId: string, now: number) =>
  token && token.userId === userId && token.expiresAt - TOKEN_MARGIN_MS > now ? token : undefined;

/** The token an impersonate answer gives; undefined without an access token */
export function readToken(userId: string, answer: unknown, now: number): PreviewToken | undefined {
  const { access_token: accessToken, expires_in: expiresIn } = (answer ?? {}) as { access_token?: unknown; expires_in?: unknown };
  if (typeof accessToken !== "string" || !accessToken) return undefined;
  return { userId, accessToken, expiresAt: now + (typeof expiresIn === "number" && expiresIn > 0 ? expiresIn * 1000 : 0) };
}
