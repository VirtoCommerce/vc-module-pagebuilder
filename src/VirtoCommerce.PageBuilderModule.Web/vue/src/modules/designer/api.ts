import type { Item, PageDocument, SectionGroup } from "./types";
import { fromDocument, toDocument } from "./document";
import { templateKeyOf } from "./templates";
import { groupPath, historyStatusUrl, historyUrl, parseHistoryPage, parseHistoryStatus, restoreUrl, versionUrl, type HistoryStatus } from "./history";
import {
  designerDiscardBody,
  designerDiscardUrl,
  designerSaveBody,
  designerSaveUrl,
  type DesignerDiscardPage,
  type DesignerSaveFile,
  type DesignerSavePage,
} from "./designerSave";
import { storefrontPage } from "./preview";
import { storeMenusUrl } from "./storeMenus";
import { impersonateBody, previewUserIds, previewUsers, readToken, type PreviewToken, type PreviewUser } from "./previewUsers";
import { parseThemeStatus, publishThemeUrl, type ThemeStatus } from "./themePublication";
import { checkBody, checkPaletteUrl, generateBody, generatePaletteUrl, parseCheck, parseGenerated, type GeneratedPalettes, type PaletteCheck } from "./paletteContrast";
import type { PaletteTokens } from "./previewMessages";
import type { ThemeLook } from "./themeLook";
import { getAuthHeaders } from "../page-builder/composables/usePageContentApi";

// The designer's platform calls (the generated client types the content endpoints as void), as the shell user.

/** The designer action the calls being made are part of (asOneSave); the version the site's history records names it */
let currentSave: string | undefined;

// a plain-http host has no crypto.randomUUID, and a Save must not fail for want of an id
const newSaveId = () => globalThis.crypto?.randomUUID?.() ?? `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 12)}`;

/** Names the calls `action` makes as one designer action */
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
  /** git flow, a repository the platform writes live: the live page is not where the production branch puts it (a merge done on GitHub) */
  liveBehind?: boolean;
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
    languages?: string[];
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
    languages: Array.isArray(store.languages) ? store.languages.filter((x): x is string => typeof x === "string") : [],
  };
}

/**
 * The accounts the store lists to preview as (its PreviewUserIds setting), named as the platform knows them; an
 * editor who may not search users sees their ids
 */
export async function loadPreviewUsers(storeId: string): Promise<PreviewUser[]> {
  const ids = previewUserIds(await (await call(`/api/stores/${encodeURIComponent(storeId)}`)).json());
  if (!ids.length) return [];
  const found = await call("/api/platform/security/users/search", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ objectIds: ids, take: ids.length }),
  })
    .then(async (res) => ((await res.json()) as { results?: { id?: string; userName?: string; email?: string }[] }).results ?? [])
    .catch(() => []);
  return previewUsers(ids, found);
}

/** An access token to preview as the user, by the platform's impersonate grant (login on behalf); never a refresh token */
export async function impersonate(userId: string): Promise<PreviewToken> {
  const res = await call("/connect/token", {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: impersonateBody(userId),
  });
  const token = readToken(userId, await res.json(), Date.now());
  if (!token) throw new ApiError(res.status, "The impersonate grant gave no access token");
  return token;
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

// ── the storefront's schema catalog, kept by the server by its hash ──

const catalogUrl = (query: Record<string, string>) => `/api/pagebuilder/catalog?${new URLSearchParams(query)}`;

/** Whether the server has the store's catalog with that hash */
export async function hasCatalog(storeId: string, hash: string): Promise<boolean> {
  const res = await fetch(catalogUrl({ storeId, hash }), { headers: await getAuthHeaders() });
  if (res.status === 404) return false;
  if (!res.ok) throw new ApiError(res.status, await res.text().catch(() => ""));
  return true;
}

/** Uploads the catalog of the storefront the designer previews (needs builder:theme; 403 without it) */
export async function uploadCatalog(storeId: string, catalog: object) {
  const res = await fetch(catalogUrl({ storeId }), {
    method: "PUT",
    headers: { ...(await getAuthHeaders()), "Content-Type": "application/json" },
    body: JSON.stringify(catalog),
  });
  if (!res.ok) throw new ApiError(res.status, await res.text().catch(() => ""));
}

/** Both palettes built from the brand colors over the storefront's tokens */
export async function generatePalette(storeId: string, tokens: PaletteTokens, primary: string, secondary?: string | null): Promise<GeneratedPalettes> {
  const res = await call(generatePaletteUrl(storeId), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(generateBody(tokens, primary, secondary)),
  });
  return parseGenerated(await res.json());
}

/** The text and background pairs the look's palettes fail */
export async function checkPalette(storeId: string, tokens: PaletteTokens, look: ThemeLook): Promise<PaletteCheck> {
  const res = await call(checkPaletteUrl(storeId), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(checkBody(tokens, look)),
  });
  return parseCheck(await res.json());
}

// ── theme files (section groups, templates, the template rules, the theme settings): saved as drafts ("-draft"), put
// live together by the theme's publication ──

/** A theme file's JSON as last saved (its draft, else what is live), or undefined when the theme has none */
export async function loadThemeFile(storeId: string, theme: string, path: string): Promise<unknown> {
  const query = new URLSearchParams({ storeId, theme, type: "themes", path, draft: "true" });
  const res = await fetch(`/api/pagebuilder/template?${query}`, { headers: await getAuthHeaders() });
  if (res.status === 404) return undefined;
  if (!res.ok) throw new ApiError(res.status, await res.text().catch(() => ""));
  const text = await res.text();
  return text.trim() ? JSON.parse(text) : undefined;
}

/** The theme's current publication and the theme files with a draft the next one puts live */
export async function themeStatus(storeId: string): Promise<ThemeStatus> {
  return parseThemeStatus(await (await call(`/api/pagebuilder/theme/status?${new URLSearchParams({ storeId })}`)).json());
}

/**
 * Puts every theme draft live at once and gives the storefront a new snapshot; returns the publication's number. 400
 * `{ error, errors }`: no catalog named, or a file that doesn't fit it.
 */
export async function publishTheme(storeId: string, catalogHash?: string): Promise<number> {
  const res = await call(publishThemeUrl(storeId, catalogHash), { method: "POST" });
  return ((await res.json()) as { publication: number }).publication;
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

/** The theme files of section groups (Shopify's sections/header-group.json) */
export const sectionGroupFiles = (theme: string, groups: Partial<Record<SectionGroup, Item[]>>): DesignerSaveFile[] =>
  Object.entries(groups).map(([group, content]) => ({
    path: groupPath(theme, group as SectionGroup),
    content: { type: group, name: group === "header" ? "Header" : "Footer", content },
  }));

/**
 * One designer Save: the page and the theme files as drafts, all or nothing, one version of the site's history. 412: the
 * page was saved by someone else since it was read; 400 `{ error, errors }`: a file doesn't fit the catalog named; 403:
 * a part needs a permission the user lacks. Returns the page's new version (a database page).
 */
export async function saveDesigner(
  storeId: string,
  save: { page?: DesignerSavePage; themeFiles: DesignerSaveFile[] },
  catalogHash?: string,
): Promise<{ saveId?: string; eTag?: string }> {
  const res = await call(designerSaveUrl(storeId, catalogHash), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(designerSaveBody(save)),
  });
  return ((await res.json().catch(() => ({}))) ?? {}) as { saveId?: string; eTag?: string };
}

/**
 * One designer Discard: the drafts of the page and of the theme files go, all or nothing, one version of the site's
 * history. A file never published keeps its draft, its only copy. 400: the page was never published.
 */
export async function discardDesigner(storeId: string, discard: { page?: DesignerDiscardPage; themeFiles: string[] }) {
  await call(designerDiscardUrl(storeId), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(designerDiscardBody(discard)),
  });
}

/** The store's link lists (menuOptions reads them); an empty answer is a store without lists */
export async function storeMenus(storeId: string): Promise<unknown[]> {
  const text = await (await call(storeMenusUrl(storeId))).text();
  const lists: unknown = text.trim() ? JSON.parse(text) : [];
  return Array.isArray(lists) ? lists : [];
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

/** The store's git flow, from the builder settings: whether its pages are in git (the settings then carry the history descriptors) */
export async function gitFlowOf(storeId: string, theme: string): Promise<{ on: boolean }> {
  const res = await fetch(`/api/pagebuilder/settings?${new URLSearchParams({ storeId, theme })}`, { headers: await getAuthHeaders() });
  if (!res.ok) return { on: false };
  const settings = (await res.json().catch(() => ({}))) as { history?: unknown };
  return { on: !!settings.history };
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

/** A file page's JSON as saved: git validates the envelope, whose settings item is typed "settings" */
export const filePageContent = (doc: PageDocument) =>
  fromDocument(doc.version === 1 ? doc : { ...doc, settings: { ...doc.settings, type: doc.settings.type || "settings" } });

/** A file page's publish status; the builder answers from git or, for a store without it, from the site's files */
export async function filePublishStatus(page: FilePage): Promise<PublishStatus> {
  return ((await (await call(`/api/pagebuilder/git/publish-status?${fileArgs(page)}`)).json()) ?? { published: true, hasChanges: false }) as PublishStatus;
}

/**
 * Publish/unpublish a file page through the builder, which checks the publish permission for every store; in git,
 * publishing opens (and merges) a pull request. 409: someone published first. A template is published with the theme.
 */
export async function setFilePublished(page: FilePage, publish: boolean, rebase = false) {
  await call(`/api/pagebuilder/git/${publish ? "publish" : "unpublish"}?${fileArgs(page, rebase ? { rebase: "true" } : {})}`, { method: "POST" });
}

/** git: brings the published page to the production branch */
export async function promoteFilePage(page: FilePage) {
  await call(`/api/pagebuilder/git/promote?${fileArgs(page)}`, { method: "POST" });
}

/** git: a version of a file page */
export interface PageVersion {
  sha: string;
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

/** Whether the store keeps the history of its database pages and theme files */
export async function historyStatus(storeId: string): Promise<HistoryStatus> {
  return parseHistoryStatus(((await (await call(historyStatusUrl(storeId))).json()) as { status?: unknown })?.status);
}

/** The versions that changed any of the items, newest first; `cursor` continues. 502: GitHub did not answer. */
export async function contentHistory(storeId: string, items: string[], cursor?: string) {
  return parseHistoryPage(await (await call(historyUrl(storeId, items, cursor))).json());
}

/** An item as of a version (its JSON); undefined when it did not exist then */
export async function contentVersion(storeId: string, versionId: string, item: string): Promise<unknown> {
  const res = await fetch(versionUrl(storeId, versionId, item), { headers: await getAuthHeaders() });
  if (res.status === 404) return undefined;
  if (!res.ok) throw new ApiError(res.status, await res.text().catch(() => ""));
  return JSON.parse(await res.text());
}

/** Makes the items as of a version their drafts again, as one save; Publish puts them live */
export async function restoreContentVersion(storeId: string, versionId: string, items: string[]) {
  await call(restoreUrl(storeId, versionId, items), { method: "POST" });
}

/** git: makes a version this editor's draft again */
export async function restoreFileVersion(page: FilePage, sha: string) {
  await call(`/api/pagebuilder/git/restore-version?${fileArgs(page, { sha })}`, { method: "POST" });
}

/** Where a file page's preview opens: git redirects to the storefront at a version (or the draft) */
export function filePreviewUrl(page: FilePage, git: boolean, storefront?: { url?: string; previewPath: string }, draft = false, sha?: string) {
  if (git) return `/api/pagebuilder/git/preview?${fileArgs(page, sha ? { ref: sha } : {})}`;
  const url = storefront && storefrontPage(storefront.url, storefront.previewPath);
  if (!url) return undefined;
  // the content module's document id of the file (its draft blob while there is one)
  const id = btoa(`${page.storeId}::${page.type}::${page.path}${draft && !page.path.endsWith("-draft") ? "-draft" : ""}`).replace(/=/g, "-");
  url.searchParams.set("pageId", id);
  return url.toString();
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
