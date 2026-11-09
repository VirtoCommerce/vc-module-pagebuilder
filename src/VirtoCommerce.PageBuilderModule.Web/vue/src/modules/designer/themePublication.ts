export interface ThemeStatus {
  publication?: number;
  drafts: string[];
  newDrafts?: string[];
}

const paths = (value: unknown) => (Array.isArray(value) ? value.filter((x): x is string => typeof x === "string") : []);

export function parseThemeStatus(body: unknown): ThemeStatus {
  const value = (body ?? {}) as { publication?: unknown; drafts?: unknown; newDrafts?: unknown };
  return {
    publication: typeof value.publication === "number" ? value.publication : undefined,
    drafts: paths(value.drafts),
    newDrafts: paths(value.newDrafts),
  };
}

export const hasThemeDraft = (status: ThemeStatus | undefined, theme: string, path: string) =>
  !!status?.drafts.some((x) => `${theme}/${x}` === path);

/** Such a draft is the file's only copy, so a Discard keeps it. */
export const isNewThemeDraft = (status: ThemeStatus | undefined, theme: string, path: string) =>
  !!status?.newDrafts?.some((x) => `${theme}/${x}` === path);

export function templateStatus(status: ThemeStatus | undefined, theme: string, path: string): { published: boolean; hasChanges: boolean } {
  if (!status) return { published: true, hasChanges: false };
  return { published: status.publication !== undefined, hasChanges: hasThemeDraft(status, theme, path) };
}

export function publishThemeUrl(storeId: string, catalogHash?: string) {
  const query = new URLSearchParams({ storeId });
  if (catalogHash) query.set("catalogHash", catalogHash);
  return `/api/pagebuilder/theme/publish?${query}`;
}

export function publishActions(open: { template: boolean; shared: boolean; mayPublish: boolean }): { page: boolean; theme: boolean } {
  return { page: !open.shared && !open.template && open.mayPublish, theme: !open.shared && open.mayPublish };
}

export function themePublishButton(input: { status?: ThemeStatus; unsaved: boolean; busy: boolean }): {
  label: { key: string; count?: number };
  disabled: boolean;
} {
  const count = input.status?.drafts.length ?? 0;
  return {
    label: count ? { key: "PAGE_BUILDER.DESIGNER.PUBLISH_THEME_COUNT", count } : { key: "PAGE_BUILDER.DESIGNER.PUBLISH_THEME" },
    disabled: !count || input.unsaved || input.busy,
  };
}

export function themeErrors(status: number, body: string): string[] {
  if (status !== 400) return [];
  try {
    const value = JSON.parse(body) as { error?: unknown; errors?: unknown };
    const errors = Array.isArray(value.errors) ? value.errors.filter((x): x is string => typeof x === "string" && !!x) : [];
    if (errors.length) return errors;
    return typeof value.error === "string" && value.error ? [value.error] : [];
  } catch {
    return [];
  }
}
