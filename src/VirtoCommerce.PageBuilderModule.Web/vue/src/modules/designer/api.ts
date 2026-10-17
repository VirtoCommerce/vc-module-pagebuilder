import type { Item, PageDocument, SectionGroup } from "./types";
import { fromDocument, toDocument } from "./document";
import { getAuthHeaders } from "../page-builder/composables/usePageContentApi";

// The designer's platform calls (the generated client types the content endpoints as void), as the shell user.

async function call(url: string, init: RequestInit = {}) {
  const res = await fetch(url, { ...init, headers: { ...(await getAuthHeaders()), ...init.headers } });
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
  const store = (await (await call(`/api/stores/${encodeURIComponent(storeId)}`)).json()) as { url?: string; secureUrl?: string; name?: string };
  const [fallbackUrl, previewPath] = await Promise.all([
    store.url || store.secureUrl ? undefined : setting("VirtoCommerce.PageBuilderModule.General.StoreUrl"),
    setting("VirtoCommerce.PageBuilderModule.General.StorePreviewPath"),
  ]);
  return { url: store.url || store.secureUrl || fallbackUrl, previewPath: previewPath || "/designer-preview" };
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

const groupPath = (theme: string, group: SectionGroup) => `${theme}/sections/${group}-group.json`;

/**
 * A theme's section group (Shopify's sections/header-group.json): its sections as last saved (the draft, else what is
 * live), or undefined when the theme has none
 */
export async function loadSectionGroup(storeId: string, theme: string, group: SectionGroup): Promise<Item[] | undefined> {
  const query = new URLSearchParams({ storeId, theme, type: "themes", path: groupPath(theme, group), draft: "true" });
  const res = await fetch(`/api/pagebuilder/template?${query}`, { headers: await getAuthHeaders() });
  if (res.status === 404) return undefined;
  if (!res.ok) throw new ApiError(res.status, await res.text().catch(() => ""));
  const text = await res.text();
  const content = text.trim() ? (JSON.parse(text) as { content?: unknown }).content : undefined;
  return Array.isArray(content) ? (content as Item[]) : [];
}

/**
 * Saves section groups as drafts of the theme's files ("-draft"): the site keeps its live header and footer until the
 * page is published (publishSectionGroups)
 */
export async function saveSectionGroups(storeId: string, theme: string, groups: Partial<Record<SectionGroup, Item[]>>) {
  const files = Object.entries(groups).map(([group, content]) => ({
    path: groupPath(theme, group as SectionGroup),
    type: "themes",
    content: { type: group, name: group === "header" ? "Header" : "Footer", content },
  }));
  await call(`/api/pagebuilder/save?${new URLSearchParams({ storeId, theme, draft: "true" })}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ files: JSON.stringify(files) }),
  });
}

const groupFileUrl = (storeId: string, theme: string, group: SectionGroup, action: "publish-status" | "publishing", query: Record<string, string> = {}) =>
  `/api/content/themes/${encodeURIComponent(storeId)}/${action}?${new URLSearchParams({ relativeUrl: groupPath(theme, group), ...query })}`;

/** The section groups with a saved draft the site doesn't show yet (the content module: hasChanges = a draft exists) */
export async function sectionGroupDrafts(storeId: string, theme: string, groups: SectionGroup[]): Promise<SectionGroup[]> {
  const drafts = await Promise.all(
    groups.map(async (group) => {
      const status = (await (await call(groupFileUrl(storeId, theme, group, "publish-status"))).json()) as PublishStatus | null;
      return status?.hasChanges ? group : undefined;
    }),
  );
  return drafts.filter((x): x is SectionGroup => !!x);
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
  await Promise.all(groups.map((group) => call(groupFileUrl(storeId, theme, group, "publishing", { publish: "true" }), { method: "POST" })));
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
  const res = await fetch(`/api/pagebuilder/template?${fileArgs(page, { theme: page.theme, draft: "true" })}`, { headers: await getAuthHeaders() });
  if (res.status === 404) return toDocument({ settings: { type: "settings" }, content: [] });
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

/** A database page's versions in its site's repository, newest first (`after` continues); 404 without history */
export async function groupHistory(groupId: string, after?: string): Promise<{ versions: PageVersion[]; hasMore?: boolean; endCursor?: string }> {
  const query = after ? `?${new URLSearchParams({ after })}` : "";
  const res = await call(`${historyUrl(groupId)}${query}`);
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

/** A database page as it was at a version */
export async function groupVersion(groupId: string, sha: string): Promise<PageDocument> {
  return toDocument(await (await call(`${historyUrl(groupId)}/${encodeURIComponent(sha)}`)).text());
}

/** Makes a version the database page's draft again */
export async function restoreGroupVersion(groupId: string, sha: string) {
  await call(`${historyUrl(groupId)}/${encodeURIComponent(sha)}/restore`, { method: "POST" });
}

/** Throws the database page's unpublished changes away */
export async function discardGroupDraft(groupId: string) {
  await call(`${historyUrl(groupId)}/discard`, { method: "POST" });
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
