import type { PageVersion } from "./api";
import type { SectionGroup } from "./types";

// The site's history (docs/contracts/history-api.md): one version per save, listing the items it changed — the page or
// template open, the rules, the header and footer, the theme settings. A file page in git keeps its own history instead.

/** What the history shows a version changed: the page or template open, or a theme file it is shown with */
export type HistoryFile = "page" | "rules" | "settings" | SectionGroup;

/** enabled: history is kept; setupRequired: the GitHub App or the store's repository is missing; unavailable: none is kept */
export type HistoryStatus = "enabled" | "setupRequired" | "unavailable";

/** The store's template rules file */
export const rulesPath = (theme: string) => `${theme}/config/template_rules.json`;

/** A section group's file (Shopify's sections/header-group.json) */
export const groupPath = (theme: string, group: SectionGroup) => `${theme}/sections/${group}-group.json`;

/** The theme's settings file */
export const settingsPath = (theme: string) => `${theme}/config/settings_data.json`;

export const pageItem = (groupId: string) => `page:${groupId}`;
export const themeItem = (path: string) => `theme:${path}`;

/** A version as the panel lists it: a save of the site's history, or a version of a file page in git */
export interface HistoryVersion {
  /** the versionId, or the git sha */
  id: string;
  /** git: the short sha; the site's history: the start of the versionId, as a restore names it */
  label: string;
  date?: string;
  description?: string;
  author?: { name?: string; login?: string; email?: string };
  published?: boolean;
  /** git: this editor's draft */
  mine?: boolean;
  /** the site's history: the items asked for that the version changed */
  items?: string[];
}

/**
 * The items a Save of what is open writes, so the history covers: the database page or the template, a template's
 * rules, the header and footer it shows with, the theme settings
 */
export function historyItems(open: {
  groupId?: string;
  templatePath?: string;
  theme?: string;
  rules: boolean;
  groups: boolean;
  settings: boolean;
}): { file: HistoryFile; item: string }[] {
  const page = open.groupId ? pageItem(open.groupId) : open.templatePath ? themeItem(open.templatePath) : undefined;
  const items: { file: HistoryFile; item: string }[] = page ? [{ file: "page", item: page }] : [];
  if (!open.theme) return items;
  if (open.rules) items.push({ file: "rules", item: themeItem(rulesPath(open.theme)) });
  if (open.groups) items.push(...(["header", "footer"] as const).map((g) => ({ file: g, item: themeItem(groupPath(open.theme!, g)) })));
  if (open.settings) items.push({ file: "settings", item: themeItem(settingsPath(open.theme)) });
  return items;
}

const historyApi = "/api/pagebuilder/history";

export const historyStatusUrl = (storeId: string) => `${historyApi}/status?${new URLSearchParams({ storeId })}`;

/** The versions that changed any of the items, newest first; `cursor` continues with older ones */
export function historyUrl(storeId: string, items: string[], cursor?: string) {
  const query = new URLSearchParams({ storeId });
  for (const item of items) query.append("item", item);
  if (cursor) query.set("cursor", cursor);
  return `${historyApi}?${query}`;
}

/** An item as of a version */
export const versionUrl = (storeId: string, versionId: string, item: string) =>
  `${historyApi}/${encodeURIComponent(versionId)}?${new URLSearchParams({ storeId, item })}`;

/** Makes the items as of a version their drafts again, as one save */
export function restoreUrl(storeId: string, versionId: string, items: string[]) {
  const query = new URLSearchParams({ storeId });
  for (const item of items) query.append("item", item);
  return `${historyApi}/${encodeURIComponent(versionId)}/restore?${query}`;
}

const STATUSES: HistoryStatus[] = ["enabled", "setupRequired", "unavailable"];

/** A status as the server answers it; anything else reads as unavailable */
export const parseHistoryStatus = (value: unknown): HistoryStatus =>
  STATUSES.includes(value as HistoryStatus) ? (value as HistoryStatus) : "unavailable";

const LABEL_LENGTH = 7;

/** A page of the site's history as the server answers it */
export function parseHistoryPage(body: unknown): { status: HistoryStatus; versions: HistoryVersion[]; cursor?: string } {
  const value = (body ?? {}) as { status?: unknown; versions?: unknown; cursor?: unknown };
  const versions = (Array.isArray(value.versions) ? value.versions : [])
    .filter((x): x is Record<string, unknown> => !!x && typeof (x as { versionId?: unknown }).versionId === "string")
    .map((x) => ({
      id: x.versionId as string,
      label: (x.versionId as string).slice(0, LABEL_LENGTH),
      date: typeof x.date === "string" ? x.date : undefined,
      description: typeof x.description === "string" ? x.description : undefined,
      author: (x.author ?? undefined) as HistoryVersion["author"],
      published: x.published === true,
      items: Array.isArray(x.items) ? x.items.filter((i): i is string => typeof i === "string") : [],
    }));
  return { status: parseHistoryStatus(value.status), versions, cursor: typeof value.cursor === "string" && value.cursor ? value.cursor : undefined };
}

/** A version of a file page in git as the panel lists it */
export const fromGitVersion = (v: PageVersion): HistoryVersion => ({
  id: v.sha,
  label: v.shortSha ?? v.sha.slice(0, LABEL_LENGTH),
  date: v.date,
  description: v.message,
  author: { ...v.author, name: v.vcUser || v.author?.name },
  published: v.published,
  mine: v.mine,
});

/**
 * What the toolbar offers for history: the panel; turning history on (the store's git flow for a file page, the
 * site's repository for a database page or template); or nothing
 */
export function historyOffer(open: {
  shared: boolean;
  /** a page kept as a file, not a template */
  filePage: boolean;
  gitFlow: boolean;
  /** a database page or a template */
  site: boolean;
  status?: HistoryStatus;
}): "history" | "enable" | "none" {
  if (open.shared) return "none";
  if (open.filePage) return open.gitFlow ? "history" : "enable";
  if (!open.site) return "none";
  return open.status === "enabled" ? "history" : open.status === "setupRequired" ? "enable" : "none";
}
