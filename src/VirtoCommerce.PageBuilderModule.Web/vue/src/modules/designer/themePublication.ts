// The theme's publication (docs/contracts/theme-snapshot.md): every theme file is saved as a draft, and one Publish
// theme puts all the drafts live at once, as a new snapshot the storefront reads.

/** theme/status: the current publication (none before the first) and the theme files with a draft, by their path in the theme */
export interface ThemeStatus {
  publication?: number;
  drafts: string[];
}

/** theme/status as the server answers it; what isn't a list of paths reads as no drafts */
export function parseThemeStatus(body: unknown): ThemeStatus {
  const value = (body ?? {}) as { publication?: unknown; drafts?: unknown };
  return {
    publication: typeof value.publication === "number" ? value.publication : undefined,
    drafts: Array.isArray(value.drafts) ? value.drafts.filter((x): x is string => typeof x === "string") : [],
  };
}

/** Whether a theme file, named as saves name it ("default/sections/header-group.json"), has a draft */
export const hasThemeDraft = (status: ThemeStatus | undefined, theme: string, path: string) =>
  !!status?.drafts.some((x) => `${theme}/${x}` === path);

/** A template's state: changed while it has a draft; nothing is published before the theme's first publication */
export function templateStatus(status: ThemeStatus | undefined, theme: string, path: string): { published: boolean; hasChanges: boolean } {
  if (!status) return { published: true, hasChanges: false };
  return { published: status.publication !== undefined, hasChanges: hasThemeDraft(status, theme, path) };
}

/** The builder's save of drafts; theme files name the catalog of the storefront previewed, which the server checks them against */
export function saveUrl(storeId: string, theme: string, catalogHash?: string) {
  const query = new URLSearchParams({ storeId, theme, draft: "true" });
  if (catalogHash) query.set("catalogHash", catalogHash);
  return `/api/pagebuilder/save?${query}`;
}

/** A file page's save: a template, a theme file, names the catalog; a page or a blog article doesn't */
export const fileSaveUrl = (page: { storeId: string; theme: string; type: string }, catalogHash?: string) =>
  saveUrl(page.storeId, page.theme, page.type === "themes" ? catalogHash : undefined);

/** The theme's publication, checked against the catalog of the storefront previewed */
export function publishThemeUrl(storeId: string, catalogHash?: string) {
  const query = new URLSearchParams({ storeId });
  if (catalogHash) query.set("catalogHash", catalogHash);
  return `/api/pagebuilder/theme/publish?${query}`;
}

/**
 * The toolbar's publish actions: a page has its own Publish and Unpublish; a template has none of its own, Publish
 * theme takes their place (a template goes live with the rest of the theme). A shared component has neither.
 */
export function publishActions(open: { template: boolean; shared: boolean; mayPublishTheme: boolean }): { page: boolean; theme: boolean } {
  return { page: !open.shared && !open.template, theme: !open.shared && open.mayPublishTheme };
}

/** Publish theme: its label counts the theme files waiting; enabled while some wait, nothing is unsaved and nothing runs */
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

/** What a refused theme save or publication (400 `{ error, errors }`) lists; nothing for any other failure */
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
