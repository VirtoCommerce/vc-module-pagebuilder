export const IMPERSONATE_PERMISSION = "platform:security:loginOnBehalf";
export const PREVIEW_USERS_SETTING = "VirtoCommerce.PageBuilderModule.Store.PreviewUserIds";

export interface PreviewUser {
  id: string;
  name: string;
}

export function previewUserIds(store: { settings?: { name?: string; value?: unknown }[] } | undefined): string[] {
  const value = store?.settings?.find((x) => x.name === PREVIEW_USERS_SETTING)?.value;
  if (typeof value !== "string") return [];
  return [...new Set(value.split(/\r?\n/).map((x) => x.trim()).filter(Boolean))];
}

export function previewUsers(ids: string[], found: { id?: string; userName?: string; email?: string }[]): PreviewUser[] {
  return ids.map((id) => {
    const user = found.find((x) => x.id === id);
    return { id, name: user?.userName || user?.email || id };
  });
}

/** No `offline_access`, so no refresh token is issued for the preview. */
export const impersonateBody = (userId: string) => new URLSearchParams({ grant_type: "impersonate", user_id: userId }).toString();

export interface PreviewToken {
  userId: string;
  accessToken: string;
  expiresAt: number;
}

export const TOKEN_MARGIN_MS = 60_000;

export const usableToken = (token: PreviewToken | undefined, userId: string, now: number) =>
  token && token.userId === userId && token.expiresAt - TOKEN_MARGIN_MS > now ? token : undefined;

export function readToken(userId: string, answer: unknown, now: number): PreviewToken | undefined {
  const { access_token: accessToken, expires_in: expiresIn } = (answer ?? {}) as { access_token?: unknown; expires_in?: unknown };
  if (typeof accessToken !== "string" || !accessToken) return undefined;
  return { userId, accessToken, expiresAt: now + (typeof expiresIn === "number" && expiresIn > 0 ? expiresIn * 1000 : 0) };
}
