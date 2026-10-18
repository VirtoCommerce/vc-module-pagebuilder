import type { Item, PageDocument, SectionGroup } from "./types";
import { fromDocument, toDocument } from "./document";
import { templateKeyOf } from "./templates";
import { groupPath, rulesPath, themeHistoryUrl, type HistoryFile } from "./history";
import { getAuthHeaders } from "../page-builder/composables/usePageContentApi";

// The designer's platform calls (the generated client types the content endpoints as void), as the shell user.

/** The designer action the calls being made are part of (asOneSave); every change the site's history records carries it */
let currentSave: string | undefined;

// a plain-http host has no crypto.randomUUID, and a Save must not fail for want of an id
const newSaveId = () => globalThis.crypto?.randomUUID?.() ?? `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 12)}`;

/** Makes what `action` writes one version of the site's history: "Saved · Template, Header", restored as a whole */
export async function asOneSave<T>(action: () => Promise<T>): Promise<T> {
  if (currentSave) return action();
  currentSave = newSaveId();
  try {
    return await action();
  } finally {
    currentSave = undefined;
  }
}

async function call(url: string, init: RequestInit = {}) {
  const save: Record<string, string> = currentSave ? { "X-PageBuilder-Save": currentSave } : {};
  const res = await fetch(url, { ...init, headers: { ...(await getAuthHeaders()), ...save, ...init.headers } });
  if (!res.ok) throw new ApiError(res.status, await res.text().catch(() => ""));
  return res;
}

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly body: string,
  ) {
    super(`${status} ${body}`);
  }
}

const groupUrl = (groupId: string) => `/api/page-builder-pages/grouped/${encodeURIComponent(groupId)}`;

/** A page's content (draft, else published) and its version. 404/403 throw: an unknown page isn't an empty one. */
export async function loadPage(groupId: string): Promise<{ doc: PageDocument; eTag?: string }> {
  const res = await call(`${groupUrl(groupId)}/content?includeVersion=true`);
  const body = (await res.json()) as { content?: string; eTag?: string };
  return { doc: toDocument(body.content ?? ""), eTag: body.eTag ?? res.headers.get("etag") ?? undefined };
}

/** Saves the page as its draft if nobody changed it since `eTag` (412 if they did); returns the new version. */
export async function savePage(groupId: string, doc: PageDocument, eTag: string): Promise<string | undefined> {
  const res = await call(`${groupUrl(groupId)}/content?includeVersion=true`, {
    method: "POST",
    headers: { "Content-Type": "application/json", "If-Match": eTag },
    body: JSON.stringify(fromDocument(doc), null, 2),
  });
  const body = (await res.json().catch(() => ({}))) as { eTag?: string };
  return body.eTag ?? res.headers.get("etag") ?? undefined;
}

export interface PublishStatus {
  published?: boolean;
  hasChanges?: boolean;
  /** git flow: a pull request is open and will merge on its own */
  pending?: boolean;
  /** git flow: a pull request is open but needs Publish again to merge */
  awaitingMerge?: boolean;
  flow?: "git" | "blob";
  /** git flow with a production branch: its state for this page */
  production?: { published?: boolean; behind?: boolean; pending?: boolean; awaitingMerge?: boolean } | null;
}

export async function publishStatus(groupId: string): Promise<PublishStatus> {
  return ((await (await call(`/api/page-builder-pages/grouped/publish-status/${encodeURIComponent(groupId)}`)).json()) ?? {
    published: true,
    hasChanges: false,
  }) as PublishStatus;
}

/** Makes the saved draft live, or takes the page off the site. */
export async function setPublished(groupId: string, publish: boolean) {
  await call(`/api/page-builder-pages/grouped/publishing/${encodeURIComponent(groupId)}?publish=${publish}`, { method: "POST" });
}

export interface SharedComponent {
  id: string;
  name?: string;
  usageCount?: number;
}

/** The store's shared components matching a keyword (the first 20) */
export async function searchSharedComponents(storeId: string, keyword?: string): Promise<SharedComponent[]> {
  const res = await call("/api/page-builder-shared-components/search", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ storeId, keyword: keyword?.trim() || undefined, skip: 0, take: 20 }),
  });
  return ((await res.json()) as { results?: SharedComponent[] }).results ?? [];
}

/** A new shared component of the store, holding these sections */
export async function createSharedComponent(storeId: string, name: string, content: PageDocument): Promise<SharedComponent> {
  const res = await call("/api/page-builder-shared-components", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ storeId, name: name.trim(), content }),
  });
  return (await res.json()) as SharedComponent;
}

export async function sharedComponent(id: string): Promise<SharedComponent> {
  return (await (await call(`/api/page-builder-shared-components/${encodeURIComponent(id)}`)).json()) as SharedComponent;
}

export async function sharedComponentContent(id: string): Promise<PageDocument> {
  return toDocument(await (await call(`/api/page-builder-shared-components/${encodeURIComponent(id)}/content`)).text());
}

/** Saves a shared component's sections: every page that places it shows them (400 when they can't be shared) */
export async function saveSharedComponentContent(id: string, doc: PageDocument) {
  await call(`/api/page-builder-shared-components/${encodeURIComponent(id)}/content`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(fromDocument(doc)),
  });
}

async function setting(name: string): Promise<string | undefined> {
  const res = await fetch(`/api/platform/settings/${encodeURIComponent(name)}`, { headers: await getAuthHeaders() });
  if (!res.ok) return undefined;
  const value = (await res.json()) as { value?: unknown; defaultValue?: unknown };
  return (value.value ?? value.defaultValue ?? undefined) as string | undefined;
}

/** Where the store's storefront runs (the store's URL, else the module's StoreUrl setting) and its preview path. */
export async function loadStorefront(storeId: string) {
  const store = (await (await call(`/api/stores/${encodeURIComponent(storeId)}`)).json()) as {
    url?: string;
    secureUrl?: string;
    name?: string;
    defaultLanguage?: string;
    defaultCurrency?: string;
  };
  const [fallbackUrl, previewPath] = await Promise.all([
    store.url || store.secureUrl ? undefined : setting("VirtoCommerce.PageBuilderModule.General.StoreUrl"),
    setting("VirtoCommerce.PageBuilderModule.General.StorePreviewPath"),
  ]);
  return {
    url: store.url || store.secureUrl || fallbackUrl,
    previewPath: previewPath || "/designer-preview",
    // what the catalog lookups (products, categories) ask in
    catalog: { storeId, cultureName: store.defaultLanguage || "en-US", currencyCode: store.defaultCurrency || "USD" } as CatalogContext,
  };
}

// ── pages kept as files (a site's Files › Pages, *.page): blob storage, or git when the store is on the git flow ──

export interface FilePage {
  storeId: string;
  theme: string;
  /** the file's path in its content folder, as the content module lists it */
  path: string;
  /** "pages" or "blogs" */
  type: string;
}

const fileArgs = (page: FilePage, extra: Record<string, string> = {}) =>
  new URLSearchParams({ storeId: page.storeId, path: page.path, type: page.type, ...extra }).toString();

/** The store's theme (its DefaultThemeName dynamic property, else "default") */
export async function storeTheme(storeId: string): Promise<string> {
  const store = (await (await call(`/api/stores/${encodeURIComponent(storeId)}`)).json()) as {
    dynamicProperties?: { name?: string; values?: { value?: unknown }[] }[];
  };
  const value = store.dynamicProperties?.find((x) => x.name === "DefaultThemeName")?.values?.[0]?.value;
  return (typeof value === "string" && value) || "default";
}

// ── theme files (section groups, templates, the template rules): saved as drafts ("-draft"), put live by Publish ──

/** A theme file's JSON as last saved (its draft, else what is live), or undefined when the theme has none */
export async function loadThemeFile(storeId: string, theme: string, path: string): Promise<unknown> {
  const query = new URLSearchParams({ storeId, theme, type: "themes", path, draft: "true" });
  const res = await fetch(`/api/pagebuilder/template?${query}`, { headers: await getAuthHeaders() });
  if (res.status === 404) return undefined;
  if (!res.ok) throw new ApiError(res.status, await res.text().catch(() => ""));
  const text = await res.text();
  return text.trim() ? JSON.parse(text) : undefined;
}

/** Saves theme files as drafts: the site keeps the live ones until they are published (publishThemeFiles) */
export async function saveThemeFiles(storeId: string, theme: string, files: { path: string; content: unknown }[]) {
  await call(`/api/pagebuilder/save?${new URLSearchParams({ storeId, theme, draft: "true" })}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ files: JSON.stringify(files.map((x) => ({ ...x, type: "themes" }))) }),
  });
}

const themeFileUrl = (storeId: string, path: string, action: "publish-status" | "publishing", query: Record<string, string> = {}) =>
  `/api/content/themes/${encodeURIComponent(storeId)}/${action}?${new URLSearchParams({ relativeUrl: path, ...query })}`;

/** The theme files with a saved draft the site doesn't show yet (the content module: hasChanges = a draft exists) */
export async function themeFileDrafts(storeId: string, paths: string[]): Promise<string[]> {
  const drafts = await Promise.all(
    paths.map(async (path) => {
      const status = (await (await call(themeFileUrl(storeId, path, "publish-status"))).json()) as PublishStatus | null;
      return status?.hasChanges ? path : undefined;
    }),
  );
  return drafts.filter((x): x is string => !!x);
}

/** Puts theme file drafts live */
export async function publishThemeFiles(storeId: string, paths: string[]) {
  await Promise.all(paths.map((path) => call(themeFileUrl(storeId, path, "publishing", { publish: "true" }), { method: "POST" })));
}

/**
 * A theme's section group (Shopify's sections/header-group.json): its sections as last saved (the draft, else what is
 * live), or undefined when the theme has none
 */
export async function loadSectionGroup(storeId: string, theme: string, group: SectionGroup): Promise<Item[] | undefined> {
  const file = (await loadThemeFile(storeId, theme, groupPath(theme, group))) as { content?: unknown } | undefined;
  if (file === undefined) return undefined;
  return Array.isArray(file?.content) ? (file.content as Item[]) : [];
}

/**
 * Saves section groups as drafts of the theme's files ("-draft"): the site keeps its live header and footer until the
 * page is published (publishSectionGroups)
 */
export async function saveSectionGroups(storeId: string, theme: string, groups: Partial<Record<SectionGroup, Item[]>>) {
  await saveThemeFiles(
    storeId,
    theme,
    Object.entries(groups).map(([group, content]) => ({
      path: groupPath(theme, group as SectionGroup),
      content: { type: group, name: group === "header" ? "Header" : "Footer", content },
    })),
  );
}

/** The section groups with a saved draft the site doesn't show yet */
export async function sectionGroupDrafts(storeId: string, theme: string, groups: SectionGroup[]): Promise<SectionGroup[]> {
  const drafts = await themeFileDrafts(storeId, groups.map((g) => groupPath(theme, g)));
  return groups.filter((g) => drafts.includes(groupPath(theme, g)));
}

/**
 * Throws a draft kept as a "-draft" file away (a page of a store without git, a theme file such as a section group):
 * the file is what is published again. 409 when nothing is published (the draft is the only copy).
 */
export async function discardBlobDraft(storeId: string, type: string, path: string, theme?: string) {
  await call(`/api/pagebuilder/discard-draft?${new URLSearchParams({ storeId, type, path, ...(theme ? { theme } : {}) })}`, { method: "POST" });
}

/** Throws section group drafts away: the site keeps the header and footer it shows */
export async function discardSectionGroupDrafts(storeId: string, theme: string, groups: SectionGroup[]) {
  await Promise.all(groups.map((group) => discardBlobDraft(storeId, "themes", groupPath(theme, group), theme)));
}

/** Puts section group drafts live */
export async function publishSectionGroups(storeId: string, theme: string, groups: SectionGroup[]) {
  await publishThemeFiles(storeId, groups.map((g) => groupPath(theme, g)));
}

/** The theme's templates, by key ("product", "product.printer", …): its templates/ folder, drafts included */
export async function templateKeys(storeId: string, theme: string): Promise<string[]> {
  const query = new URLSearchParams({ folderUrl: `${theme}/templates` });
  const res = await fetch(`/api/content/themes/${encodeURIComponent(storeId)}/search?${query}`, { headers: await getAuthHeaders() });
  if (!res.ok) return [];
  const items = ((await res.json()) ?? []) as { name?: string }[];
  // a template saved only as a draft is listed as name.json-draft
  return [...new Set(items.map((x) => templateKeyOf(`templates/${x.name ?? ""}`)).filter((x): x is string => !!x))].sort();
}

/** Whether the store's pages are in git: the builder settings then carry the history descriptors */
export async function isGitFlow(storeId: string, theme: string): Promise<boolean> {
  const res = await fetch(`/api/pagebuilder/settings?${new URLSearchParams({ storeId, theme })}`, { headers: await getAuthHeaders() });
  if (!res.ok) return false;
  const settings = (await res.json().catch(() => ({}))) as { history?: unknown };
  return !!settings.history;
}

/** A file page: its draft (in git: this editor's branch), else what is published; a new page when there is none */
export async function loadFilePage(page: FilePage): Promise<PageDocument> {
  return (await findFilePage(page)) ?? toDocument({ settings: { type: "settings" }, content: [] });
}

/** A file page as loadFilePage reads it, or undefined when there is no such file */
export async function findFilePage(page: FilePage): Promise<PageDocument | undefined> {
  const res = await fetch(`/api/pagebuilder/template?${fileArgs(page, { theme: page.theme, draft: "true" })}`, { headers: await getAuthHeaders() });
  if (res.status === 404) return undefined;
  if (!res.ok) throw new ApiError(res.status, await res.text().catch(() => ""));
  return toDocument(await res.text());
}

/** Saves a file page's draft (in git: a commit on this editor's work branch). 400 lists what the page is missing. */
export async function saveFilePage(page: FilePage, doc: PageDocument) {
  // git validates the envelope: the settings item is typed "settings"
  const content = fromDocument(doc.version === 1 ? doc : { ...doc, settings: { ...doc.settings, type: doc.settings.type || "settings" } });
  await call(`/api/pagebuilder/save?${new URLSearchParams({ storeId: page.storeId, theme: page.theme, draft: "true" })}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ files: JSON.stringify([{ path: page.path, type: page.type, content }]) }),
  });
}

export async function filePublishStatus(page: FilePage, git: boolean): Promise<PublishStatus> {
  const url = git
    ? `/api/pagebuilder/git/publish-status?${fileArgs(page)}`
    : `/api/content/${encodeURIComponent(page.type)}/${encodeURIComponent(page.storeId)}/publish-status?${new URLSearchParams({ relativeUrl: page.path })}`;
  return ((await (await call(url)).json()) ?? { published: true, hasChanges: false }) as PublishStatus;
}

/** Publish/unpublish a file page; in git, publishing opens (and merges) a pull request. 409: someone published first. */
export async function setFilePublished(page: FilePage, git: boolean, publish: boolean, rebase = false) {
  let url = `/api/content/${encodeURIComponent(page.type)}/${encodeURIComponent(page.storeId)}/publishing?${new URLSearchParams({ relativeUrl: page.path, publish: String(publish) })}`;
  if (git) {
    const action = publish ? "publish" : "unpublish";
    url = `/api/pagebuilder/git/${action}?${fileArgs(page, rebase ? { rebase: "true" } : {})}`;
  }
  await call(url, { method: "POST" });
}

/** git: brings the published page to the production branch */
export async function promoteFilePage(page: FilePage) {
  await call(`/api/pagebuilder/git/promote?${fileArgs(page)}`, { method: "POST" });
}

export interface PageVersion {
  sha: string;
  /** the designer action it is part of (its changes of other files share it) */
  saveId?: string;
  /** a designer action of the site's history: its id, and the change of each file it wrote */
  id?: string;
  files?: { file: HistoryFile; sha: string }[];
  shortSha?: string;
  date?: string;
  message?: string;
  author?: { name?: string; email?: string; login?: string };
  vcUser?: string;
  branches?: string[];
  published?: boolean;
  mine?: boolean;
  bulk?: boolean;
}

export interface PageHistory {
  versions: PageVersion[];
  truncated?: boolean;
  endCursor?: string;
  otherDraftCount?: number;
}

/** git: the page's versions, newest first (a page at a time; `after` continues) */
export async function fileHistory(page: FilePage, after?: string): Promise<PageHistory> {
  const res = await call(`/api/pagebuilder/git/history?${fileArgs(page, after ? { after } : {})}`);
  return ((await res.json()) ?? { versions: [] }) as PageHistory;
}

/** git: throws this editor's draft away; the page is what is published again */
export async function discardFileDraft(page: FilePage) {
  await call(`/api/pagebuilder/git/discard-draft?${fileArgs(page)}`, { method: "POST" });
}

const historyUrl = (groupId: string) => `${groupUrl(groupId)}/history`;

interface SiteHistory {
  versions: PageVersion[];
  hasMore?: boolean;
  endCursor?: string;
}

/** A version list of the site's repository (a database page's, a theme file's), its authors as a file page's are */
async function siteHistory(res: Response): Promise<SiteHistory> {
  const body = (await res.json()) as {
    versions?: (PageVersion & { authorName?: string; authorEmail?: string; authorLogin?: string })[];
    hasMore?: boolean;
    endCursor?: string;
  };
  return {
    ...body,
    versions: (body.versions ?? []).map(({ authorName, authorEmail, authorLogin, ...v }) => ({
      ...v,
      author: { name: authorName, email: authorEmail, login: authorLogin },
    })),
  };
}

/** A database page's versions in its site's repository, newest first (`after` continues); 404 without history */
export async function groupHistory(groupId: string, after?: string): Promise<SiteHistory> {
  const query = after ? `?${new URLSearchParams({ after })}` : "";
  return siteHistory(await call(`${historyUrl(groupId)}${query}`));
}

/** A database page as it was at a version */
export async function groupVersion(groupId: string, sha: string): Promise<PageDocument> {
  return toDocument(await (await call(`${historyUrl(groupId)}/${encodeURIComponent(sha)}`)).text());
}

/** Makes a version the database page's draft again */
export async function restoreGroupVersion(groupId: string, sha: string, version?: string) {
  await call(`${historyUrl(groupId)}/${encodeURIComponent(sha)}/restore${version ? `?${new URLSearchParams({ version })}` : ""}`, { method: "POST" });
}

/** Throws the database page's unpublished changes away */
export async function discardGroupDraft(groupId: string) {
  await call(`${historyUrl(groupId)}/discard`, { method: "POST" });
}

/** A theme file's versions in its site's repository (a template, the rules, a section group), newest first; 404 without history */
export async function themeFileHistory(storeId: string, path: string, after?: string): Promise<SiteHistory> {
  return siteHistory(await call(themeHistoryUrl(storeId, path, undefined, undefined, after ? { after } : {})));
}

/** A theme file as it was at a version */
export async function themeFileVersion(storeId: string, path: string, sha: string): Promise<unknown> {
  return JSON.parse(await (await call(themeHistoryUrl(storeId, path, sha))).text());
}

/** Makes a version the theme file's draft again; Publish puts it live */
export async function restoreThemeFileVersion(storeId: string, path: string, sha: string, version?: string) {
  await call(themeHistoryUrl(storeId, path, sha, "restore", version ? { version } : {}), { method: "POST" });
}

/** git: makes a version this editor's draft again */
export async function restoreFileVersion(page: FilePage, sha: string) {
  await call(`/api/pagebuilder/git/restore-version?${fileArgs(page, { sha })}`, { method: "POST" });
}

/** Where a file page's preview opens: git redirects to the storefront at a version (or the draft) */
export function filePreviewUrl(page: FilePage, git: boolean, storefrontUrl?: string, draft = false, sha?: string) {
  if (git) return `/api/pagebuilder/git/preview?${fileArgs(page, sha ? { ref: sha } : {})}`;
  if (!storefrontUrl) return undefined;
  // the content module's document id of the file (its draft blob while there is one)
  const id = btoa(`${page.storeId}::${page.type}::${page.path}${draft && !page.path.endsWith("-draft") ? "-draft" : ""}`).replace(/=/g, "-");
  return `${storefrontUrl.replace(/\/+$/, "")}/designer-preview?pageId=${encodeURIComponent(id)}`;
}

// ── what templates preview with, and what rules name: the store's catalog (xAPI) and pages ──

/** The store and the language and currency the catalog is asked in */
export interface CatalogContext {
  storeId: string;
  cultureName: string;
  currencyCode: string;
}

export interface NamedEntity {
  id: string;
  name: string;
}

async function graphql<T>(query: string, variables: Record<string, unknown>): Promise<T> {
  const res = await call("/graphql", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ query, variables }) });
  const body = (await res.json()) as { data?: T; errors?: unknown[] };
  if (!body.data) throw new ApiError(res.status, JSON.stringify(body.errors ?? []));
  return body.data;
}

/** Products by keyword (the first 20), or the given ones */
export async function searchProducts(ctx: CatalogContext, keyword?: string, ids?: string[]): Promise<NamedEntity[]> {
  const data = await graphql<{ products: { items: NamedEntity[] } }>(
    `query ($storeId: String!, $cultureName: String, $currencyCode: String!, $query: String, $ids: [String]) {
      products(storeId: $storeId, cultureName: $cultureName, currencyCode: $currencyCode, query: $query, productIds: $ids, first: 20) { items { id name } }
    }`,
    { ...ctx, query: keyword?.trim() || undefined, ids: ids?.length ? ids : undefined },
  );
  return data.products.items;
}

/** Categories by keyword (the first 20), or the given ones */
export async function searchCategories(ctx: CatalogContext, keyword?: string, ids?: string[]): Promise<NamedEntity[]> {
  const data = await graphql<{ categories: { items: NamedEntity[] } }>(
    `query ($storeId: String!, $cultureName: String, $query: String, $ids: [String]) {
      categories(storeId: $storeId, cultureName: $cultureName, query: $query, categoryIds: $ids, first: 20) { items { id name } }
    }`,
    { storeId: ctx.storeId, cultureName: ctx.cultureName, query: keyword?.trim() || undefined, ids: ids?.length ? ids : undefined },
  );
  return data.categories.items;
}

/** A category's subcategories (the catalog's top ones without a parent), each saying whether it has its own */
export async function childCategories(ctx: CatalogContext, parentId?: string): Promise<(NamedEntity & { hasChildren: boolean })[]> {
  const data = await graphql<{ childCategories: { childCategories?: (NamedEntity & { childCategories?: unknown[] })[] } }>(
    `query ($storeId: String!, $cultureName: String, $categoryId: String) {
      childCategories(storeId: $storeId, cultureName: $cultureName, categoryId: $categoryId, maxLevel: 2) { childCategories { id name childCategories { id } } }
    }`,
    { storeId: ctx.storeId, cultureName: ctx.cultureName, categoryId: parentId },
  );
  return (data.childCategories.childCategories ?? []).map((x) => ({ id: x.id, name: x.name, hasChildren: !!x.childCategories?.length }));
}

/** What a product's rules can be made of: its categories (on all its catalog paths), its type and its properties */
export interface ProductFacts extends NamedEntity {
  productType?: string;
  categories: NamedEntity[];
  properties: { name: string; label?: string; value: unknown }[];
}

export async function productFacts(ctx: CatalogContext, id: string): Promise<ProductFacts | undefined> {
  const data = await graphql<{
    product?: {
      id: string;
      name: string;
      productType?: string;
      outlines?: { items?: (NamedEntity & { seoObjectType?: string })[] }[];
      properties?: { name: string; label?: string; value: unknown; propertyType?: string }[];
    };
  }>(
    `query ($storeId: String!, $cultureName: String, $currencyCode: String!, $id: String!) {
      product(id: $id, storeId: $storeId, cultureName: $cultureName, currencyCode: $currencyCode) {
        id name productType outlines { items { id name seoObjectType } } properties { name label value propertyType }
      }
    }`,
    { ...ctx, id },
  );
  const product = data.product;
  if (!product) return undefined;
  const categories = new Map<string, NamedEntity>();
  // each path starts with its catalog and ends with the product itself: the categories between
  for (const outline of product.outlines ?? [])
    for (const item of outline.items ?? []) if (item.seoObjectType === "Category") categories.set(item.id, { id: item.id, name: item.name });
  const properties = (product.properties ?? []).filter((x) => x.propertyType !== "VARIATION" && x.value != null && x.value !== "");
  return { id: product.id, name: product.name, productType: product.productType, categories: [...categories.values()], properties };
}

/** The store's pages (database pages) by keyword: their name and permalink */
export async function searchPages(storeId: string, keyword?: string): Promise<{ name: string; permalink: string }[]> {
  const res = await call("/api/page-builder-pages/search", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ storeId, keyword: keyword?.trim() || undefined, skip: 0, take: 20 }),
  });
  const results = ((await res.json()) as { results?: { name?: string; permalink?: string }[] }).results ?? [];
  return results.filter((x) => x.permalink).map((x) => ({ name: x.name || x.permalink!, permalink: `/${x.permalink!.replace(/^\/+/, "")}` }));
}
