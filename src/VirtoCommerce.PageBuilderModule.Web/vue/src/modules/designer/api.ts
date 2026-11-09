import type { Item, PageDocument, SectionGroup } from "./types";
import { fromDocument, toDocument } from "./document";
import { templateKeyOf } from "./templates";
import { groupPath, historyStatusUrl, historyUrl, parseHistoryPage, parseHistoryStatus, restoreUrl, versionUrl, type HistoryStatus } from "./history";
import {
  CatalogPendingError,
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

// The generated client types the content endpoints as void, so these calls are written by hand.

/** The version the site's history records is named after this id. */
let currentSave: string | undefined;

// A plain-http host has no crypto.randomUUID, but getRandomValues works there too.
const newSaveId = () =>
  globalThis.crypto.randomUUID?.() ??
  Array.from(globalThis.crypto.getRandomValues(new Uint8Array(16)), (byte) => byte.toString(16).padStart(2, "0")).join("");

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

async function find(url: string): Promise<Response | undefined> {
  try {
    return await call(url);
  } catch (e) {
    if (e instanceof ApiError && e.status === 404) return undefined;
    throw e;
  }
}

const jsonOf = async (res: Response): Promise<unknown> => {
  const text = await res.text();
  return text.trim() ? JSON.parse(text) : undefined;
};

const groupUrl = (groupId: string) => `/api/page-builder-pages/grouped/${encodeURIComponent(groupId)}`;

/** 404 and 403 throw: an unknown page is not an empty one. */
export async function loadPage(groupId: string): Promise<{ doc: PageDocument; eTag?: string }> {
  const res = await call(`${groupUrl(groupId)}/content?includeVersion=true`);
  const body = (await res.json()) as { content?: string; eTag?: string };
  return { doc: toDocument(body.content ?? ""), eTag: body.eTag ?? res.headers.get("etag") ?? undefined };
}

/** 412 when someone changed the page since `eTag`. */
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
  /** git flow: a pull request is open and will merge on its own. */
  pending?: boolean;
  /** git flow: a pull request is open but needs Publish again to merge. */
  awaitingMerge?: boolean;
  flow?: "git" | "blob";
  production?: { published?: boolean; behind?: boolean; pending?: boolean; awaitingMerge?: boolean } | null;
  /** git flow: the live page is not where the production branch puts it, after a merge done on GitHub. */
  liveBehind?: boolean;
}

export async function publishStatus(groupId: string): Promise<PublishStatus> {
  return ((await (await call(`/api/page-builder-pages/grouped/publish-status/${encodeURIComponent(groupId)}`)).json()) ?? {
    published: true,
    hasChanges: false,
  }) as PublishStatus;
}

export async function setPublished(groupId: string, publish: boolean) {
  await call(`/api/page-builder-pages/grouped/publishing/${encodeURIComponent(groupId)}?publish=${publish}`, { method: "POST" });
}

export interface SharedComponent {
  id: string;
  name?: string;
  usageCount?: number;
}

export async function searchSharedComponents(storeId: string, keyword?: string): Promise<SharedComponent[]> {
  const res = await call("/api/page-builder-shared-components/search", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ storeId, keyword: keyword?.trim() || undefined, skip: 0, take: 20 }),
  });
  return ((await res.json()) as { results?: SharedComponent[] }).results ?? [];
}

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

/** 400 when the sections cannot be shared. */
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

/** Resolved as the server's preview links are, https first because an http frame in the https admin is blocked. */
export async function loadStorefront(storeId: string) {
  const store = (await (await call(`/api/stores/${encodeURIComponent(storeId)}`)).json()) as {
    url?: string;
    secureUrl?: string;
    name?: string;
    defaultLanguage?: string;
    defaultCurrency?: string;
    languages?: string[];
  };
  const [configuredUrl, previewPath] = await Promise.all([
    setting("VirtoCommerce.PageBuilderModule.General.StoreUrl"),
    setting("VirtoCommerce.PageBuilderModule.General.StorePreviewPath"),
  ]);
  return {
    url: configuredUrl?.trim() || store.secureUrl?.trim() || store.url?.trim() || undefined,
    previewPath: previewPath || "/designer-preview",
    catalog: { storeId, cultureName: store.defaultLanguage || "en-US", currencyCode: store.defaultCurrency || "USD" } as CatalogContext,
    languages: Array.isArray(store.languages) ? store.languages.filter((x): x is string => typeof x === "string") : [],
  };
}

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

export interface FilePage {
  storeId: string;
  theme: string;
  path: string;
  type: string;
}

const fileArgs = (page: FilePage, extra: Record<string, string> = {}) =>
  new URLSearchParams({ storeId: page.storeId, path: page.path, type: page.type, ...extra }).toString();

export async function storeTheme(storeId: string): Promise<string> {
  const store = (await (await call(`/api/stores/${encodeURIComponent(storeId)}`)).json()) as {
    dynamicProperties?: { name?: string; values?: { value?: unknown }[] }[];
  };
  const value = store.dynamicProperties?.find((x) => x.name === "DefaultThemeName")?.values?.[0]?.value;
  return (typeof value === "string" && value) || "default";
}

const catalogUrl = (query: Record<string, string>) => `/api/pagebuilder/catalog?${new URLSearchParams(query)}`;

export async function hasCatalog(storeId: string, hash: string): Promise<boolean> {
  return !!(await find(catalogUrl({ storeId, hash })));
}

export async function uploadCatalog(storeId: string, catalog: object) {
  await call(catalogUrl({ storeId }), { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(catalog) });
}

export async function generatePalette(storeId: string, tokens: PaletteTokens, primary: string, secondary?: string | null): Promise<GeneratedPalettes> {
  const res = await call(generatePaletteUrl(storeId), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(generateBody(tokens, primary, secondary)),
  });
  return parseGenerated(await res.json());
}

export async function checkPalette(storeId: string, tokens: PaletteTokens, look: ThemeLook): Promise<PaletteCheck> {
  const res = await call(checkPaletteUrl(storeId), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(checkBody(tokens, look)),
  });
  return parseCheck(await res.json());
}

export interface ThemeFile {
  content: unknown;
  /** Save sends it back (412 when someone saved since), and a file the theme lacks has one too. */
  eTag: string;
}

export async function loadThemeFile(storeId: string, path: string): Promise<ThemeFile> {
  const res = await call(`/api/pagebuilder/designer/theme-file?${new URLSearchParams({ storeId, path })}`);
  const body = ((await jsonOf(res)) ?? {}) as { content?: string | null; eTag?: string };
  return { content: body.content?.trim() ? JSON.parse(body.content) : undefined, eTag: body.eTag ?? "" };
}

export async function themeStatus(storeId: string): Promise<ThemeStatus> {
  return parseThemeStatus(await (await call(`/api/pagebuilder/theme/status?${new URLSearchParams({ storeId })}`)).json());
}

/** 400 `{ error, errors }`: no catalog is named, or a file does not fit it. */
export async function publishTheme(storeId: string, catalogHash?: string): Promise<number> {
  const res = await call(publishThemeUrl(storeId, catalogHash), { method: "POST" });
  return ((await res.json()) as { publication: number }).publication;
}

export async function loadSectionGroup(storeId: string, theme: string, group: SectionGroup): Promise<{ items?: Item[]; eTag: string }> {
  const file = await loadThemeFile(storeId, groupPath(theme, group));
  const content = file.content as { content?: unknown } | undefined;
  if (content === undefined) return { eTag: file.eTag };
  return { items: Array.isArray(content?.content) ? (content.content as Item[]) : [], eTag: file.eTag };
}

export const sectionGroupFiles = (
  theme: string,
  groups: Partial<Record<SectionGroup, Item[]>>,
  eTags: Readonly<Record<string, string>>,
): DesignerSaveFile[] =>
  Object.entries(groups).map(([group, content]) => {
    const path = groupPath(theme, group as SectionGroup);
    return { path, eTag: eTags[path] ?? "", content: { type: group, name: group === "header" ? "Header" : "Footer", content } };
  });

/**
 * 412: the page or a theme file changed since it was read. 428: one went without its version. 400 `{ error, errors }`:
 * a file does not fit the named catalog. 403: a part needs a permission the user lacks.
 */
export async function saveDesigner(
  storeId: string,
  save: { page?: DesignerSavePage; themeFiles: DesignerSaveFile[] },
  catalogHash?: string,
): Promise<{ saveId?: string; eTag?: string; themeFiles?: { path: string; eTag: string }[] }> {
  if (save.themeFiles.length && !catalogHash) throw new CatalogPendingError();
  const res = await call(designerSaveUrl(storeId, catalogHash), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(designerSaveBody(save)),
  });
  return ((await res.json().catch(() => ({}))) ?? {}) as { saveId?: string; eTag?: string; themeFiles?: { path: string; eTag: string }[] };
}

/** A file never published keeps its draft, its only copy. 400: the page was never published. */
export async function discardDesigner(storeId: string, discard: { page?: DesignerDiscardPage; themeFiles: string[] }) {
  await call(designerDiscardUrl(storeId), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(designerDiscardBody(discard)),
  });
}

export async function storeMenus(storeId: string): Promise<unknown[]> {
  const text = await (await call(storeMenusUrl(storeId))).text();
  const lists: unknown = text.trim() ? JSON.parse(text) : [];
  return Array.isArray(lists) ? lists : [];
}

export async function templateKeys(storeId: string, theme: string): Promise<string[]> {
  const query = new URLSearchParams({ folderUrl: `${theme}/templates` });
  const res = await find(`/api/content/themes/${encodeURIComponent(storeId)}/search?${query}`);
  if (!res) return [];
  const items = ((await res.json()) ?? []) as { name?: string }[];
  // A template saved only as a draft is listed as name.json-draft.
  return [...new Set(items.map((x) => templateKeyOf(`templates/${x.name ?? ""}`)).filter((x): x is string => !!x))].sort();
}

export async function gitFlowOf(storeId: string, theme: string): Promise<{ on: boolean }> {
  const res = await fetch(`/api/pagebuilder/settings?${new URLSearchParams({ storeId, theme })}`, { headers: await getAuthHeaders() });
  if (!res.ok) return { on: false };
  const settings = (await res.json().catch(() => ({}))) as { history?: unknown };
  return { on: !!settings.history };
}

export async function loadFilePage(page: FilePage): Promise<PageDocument> {
  return (await findFilePage(page)) ?? toDocument({ settings: { type: "settings" }, content: [] });
}

export async function findFilePage(page: FilePage): Promise<PageDocument | undefined> {
  const res = await find(`/api/pagebuilder/template?${fileArgs(page, { theme: page.theme, draft: "true" })}`);
  return res && toDocument(await res.text());
}

export async function findTemplate(page: FilePage): Promise<{ doc?: PageDocument; eTag: string }> {
  const file = await loadThemeFile(page.storeId, page.path);
  return { doc: file.content === undefined ? undefined : toDocument(file.content), eTag: file.eTag };
}

/** git validates the envelope, whose settings item must be typed "settings". */
export const filePageContent = (doc: PageDocument) =>
  fromDocument(doc.version === 1 ? doc : { ...doc, settings: { ...doc.settings, type: doc.settings.type || "settings" } });

export async function filePublishStatus(page: FilePage): Promise<PublishStatus> {
  return ((await (await call(`/api/pagebuilder/git/publish-status?${fileArgs(page)}`)).json()) ?? { published: true, hasChanges: false }) as PublishStatus;
}

/** Goes through the builder, which checks the publish permission for every store. 409: someone published first. */
export async function setFilePublished(page: FilePage, publish: boolean, rebase = false) {
  await call(`/api/pagebuilder/git/${publish ? "publish" : "unpublish"}?${fileArgs(page, rebase ? { rebase: "true" } : {})}`, { method: "POST" });
}

export async function promoteFilePage(page: FilePage) {
  await call(`/api/pagebuilder/git/promote?${fileArgs(page)}`, { method: "POST" });
}

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

export async function fileHistory(page: FilePage, after?: string): Promise<PageHistory> {
  const res = await call(`/api/pagebuilder/git/history?${fileArgs(page, after ? { after } : {})}`);
  return ((await res.json()) ?? { versions: [] }) as PageHistory;
}

export async function historyStatus(storeId: string): Promise<HistoryStatus> {
  return parseHistoryStatus(((await (await call(historyStatusUrl(storeId))).json()) as { status?: unknown })?.status);
}

export async function contentHistory(storeId: string, items: string[], cursor?: string) {
  return parseHistoryPage(await (await call(historyUrl(storeId, items, cursor))).json());
}

export async function contentVersion(storeId: string, versionId: string, item: string): Promise<unknown> {
  const res = await find(versionUrl(storeId, versionId, item));
  return res && JSON.parse(await res.text());
}

export async function restoreContentVersion(storeId: string, versionId: string, items: string[]) {
  await call(restoreUrl(storeId, versionId, items), { method: "POST" });
}

export async function restoreFileVersion(page: FilePage, sha: string) {
  await call(`/api/pagebuilder/git/restore-version?${fileArgs(page, { sha })}`, { method: "POST" });
}

export function filePreviewUrl(page: FilePage, git: boolean, storefront?: { url?: string; previewPath: string }, draft = false, sha?: string) {
  if (git) return `/api/pagebuilder/git/preview?${fileArgs(page, sha ? { ref: sha } : {})}`;
  const url = storefront && storefrontPage(storefront.url, storefront.previewPath);
  if (!url) return undefined;
  // Must match the content module's document id: base64 of the UTF-8 bytes, as the server encodes it.
  const path = page.path.replace(/\\/g, "/").replace(/^\/+/, "");
  const raw = `${page.storeId}::${page.type || "pages"}::${path}${draft && !path.endsWith("-draft") ? "-draft" : ""}`;
  const id = btoa(Array.from(new TextEncoder().encode(raw), (byte) => String.fromCharCode(byte)).join("")).replace(/=/g, "-");
  url.searchParams.set("pageId", id);
  return url.toString();
}

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

export async function searchProducts(ctx: CatalogContext, keyword?: string, ids?: string[]): Promise<NamedEntity[]> {
  const data = await graphql<{ products: { items: NamedEntity[] } }>(
    `query ($storeId: String!, $cultureName: String, $currencyCode: String!, $query: String, $ids: [String]) {
      products(storeId: $storeId, cultureName: $cultureName, currencyCode: $currencyCode, query: $query, productIds: $ids, first: 20) { items { id name } }
    }`,
    { ...ctx, query: keyword?.trim() || undefined, ids: ids?.length ? ids : undefined },
  );
  return data.products.items;
}

export async function searchCategories(ctx: CatalogContext, keyword?: string, ids?: string[]): Promise<NamedEntity[]> {
  const data = await graphql<{ categories: { items: NamedEntity[] } }>(
    `query ($storeId: String!, $cultureName: String, $query: String, $ids: [String]) {
      categories(storeId: $storeId, cultureName: $cultureName, query: $query, categoryIds: $ids, first: 20) { items { id name } }
    }`,
    { storeId: ctx.storeId, cultureName: ctx.cultureName, query: keyword?.trim() || undefined, ids: ids?.length ? ids : undefined },
  );
  return data.categories.items;
}

export async function childCategories(ctx: CatalogContext, parentId?: string): Promise<(NamedEntity & { hasChildren: boolean })[]> {
  const data = await graphql<{ childCategories: { childCategories?: (NamedEntity & { childCategories?: unknown[] })[] } }>(
    `query ($storeId: String!, $cultureName: String, $categoryId: String) {
      childCategories(storeId: $storeId, cultureName: $cultureName, categoryId: $categoryId, maxLevel: 2) { childCategories { id name childCategories { id } } }
    }`,
    { storeId: ctx.storeId, cultureName: ctx.cultureName, categoryId: parentId },
  );
  return (data.childCategories.childCategories ?? []).map((x) => ({ id: x.id, name: x.name, hasChildren: !!x.childCategories?.length }));
}

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
  // Each path starts with its catalog and ends with the product itself, so the categories are between.
  for (const outline of product.outlines ?? [])
    for (const item of outline.items ?? []) if (item.seoObjectType === "Category") categories.set(item.id, { id: item.id, name: item.name });
  const properties = (product.properties ?? []).filter((x) => x.propertyType !== "VARIATION" && x.value != null && x.value !== "");
  return { id: product.id, name: product.name, productType: product.productType, categories: [...categories.values()], properties };
}

export async function searchPages(storeId: string, keyword?: string): Promise<{ name: string; permalink: string }[]> {
  const res = await call("/api/page-builder-pages/search", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ storeId, keyword: keyword?.trim() || undefined, skip: 0, take: 20 }),
  });
  const results = ((await res.json()) as { results?: { name?: string; permalink?: string }[] }).results ?? [];
  return results.filter((x) => x.permalink).map((x) => ({ name: x.name || x.permalink!, permalink: `/${x.permalink!.replace(/^\/+/, "")}` }));
}
