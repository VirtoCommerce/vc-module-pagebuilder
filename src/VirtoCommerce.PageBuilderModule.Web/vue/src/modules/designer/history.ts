import type { PageVersion } from "./api";
import type { SectionGroup } from "./types";

export type ThemePart = "rules" | SectionGroup | "brand" | "palette" | "darkPalette";

export type HistoryFile = "page" | ThemePart;

export const groupPath = (theme: string, group: SectionGroup) => `${theme}/sections/${group}-group.json`;

export const THEME_FILES: Readonly<Record<ThemePart, (theme: string) => string>> = {
  rules: (theme) => `${theme}/config/template_rules.json`,
  header: (theme) => groupPath(theme, "header"),
  footer: (theme) => groupPath(theme, "footer"),
  brand: (theme) => `${theme}/config/brand.json`,
  palette: (theme) => `${theme}/config/palette.json`,
  darkPalette: (theme) => `${theme}/config/palette.dark.json`,
};

export const THEME_PARTS = Object.keys(THEME_FILES) as ThemePart[];

/** The preview shows these theme files as they are now, so a version can restore them but not preview them. */
export const THEME_ONLY_FILES: HistoryFile[] = THEME_PARTS.filter((part) => part !== "header" && part !== "footer");

/** setupRequired: the GitHub App or the store's repository is missing. */
export type HistoryStatus = "enabled" | "setupRequired" | "unavailable";

export const rulesPath = THEME_FILES.rules;

export const PART_LABELS: Readonly<Record<HistoryFile, string>> = {
  page: "PAGE_BUILDER.DESIGNER.HISTORY.PAGE",
  rules: "PAGE_BUILDER.DESIGNER.HISTORY.RULES",
  header: "PAGE_BUILDER.DESIGNER.GROUPS.HEADER",
  footer: "PAGE_BUILDER.DESIGNER.GROUPS.FOOTER",
  brand: "PAGE_BUILDER.DESIGNER.THEME.BRAND",
  palette: "PAGE_BUILDER.DESIGNER.THEME.LIGHT_COLORS",
  darkPalette: "PAGE_BUILDER.DESIGNER.THEME.DARK_COLORS",
};

export const partLabel = (part: HistoryFile, template: boolean) => (part === "page" && template ? "PAGE_BUILDER.DESIGNER.GROUPS.TEMPLATE" : PART_LABELS[part]);

export const pageItem = (groupId: string) => `page:${groupId}`;
export const themeItem = (path: string) => `theme:${path}`;

export interface HistoryVersion {
  id: string;
  label: string;
  date?: string;
  description?: string;
  author?: { name?: string; login?: string; email?: string };
  published?: boolean;
  mine?: boolean;
  items?: string[];
}

export function historyItems(open: {
  groupId?: string;
  templatePath?: string;
  theme?: string;
  parts: ThemePart[];
}): { file: HistoryFile; item: string }[] {
  const page = open.groupId ? pageItem(open.groupId) : open.templatePath ? themeItem(open.templatePath) : undefined;
  const items: { file: HistoryFile; item: string }[] = page ? [{ file: "page", item: page }] : [];
  const theme = open.theme;
  if (!theme) return items;
  return [...items, ...THEME_PARTS.filter((part) => open.parts.includes(part)).map((part) => ({ file: part, item: themeItem(THEME_FILES[part](theme)) }))];
}

const historyApi = "/api/pagebuilder/history";

export const historyStatusUrl = (storeId: string) => `${historyApi}/status?${new URLSearchParams({ storeId })}`;

export function historyUrl(storeId: string, items: string[], cursor?: string) {
  const query = new URLSearchParams({ storeId });
  for (const item of items) query.append("item", item);
  if (cursor) query.set("cursor", cursor);
  return `${historyApi}?${query}`;
}

export const versionUrl = (storeId: string, versionId: string, item: string) =>
  `${historyApi}/${encodeURIComponent(versionId)}?${new URLSearchParams({ storeId, item })}`;

export function restoreUrl(storeId: string, versionId: string, items: string[]) {
  const query = new URLSearchParams({ storeId });
  for (const item of items) query.append("item", item);
  return `${historyApi}/${encodeURIComponent(versionId)}/restore?${query}`;
}

const STATUSES: HistoryStatus[] = ["enabled", "setupRequired", "unavailable"];

export const parseHistoryStatus = (value: unknown): HistoryStatus =>
  STATUSES.includes(value as HistoryStatus) ? (value as HistoryStatus) : "unavailable";

const LABEL_LENGTH = 7;

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

export const fromGitVersion = (v: PageVersion): HistoryVersion => ({
  id: v.sha,
  label: v.shortSha ?? v.sha.slice(0, LABEL_LENGTH),
  date: v.date,
  description: v.message,
  author: { ...v.author, name: v.vcUser || v.author?.name },
  published: v.published,
  mine: v.mine,
});

export function historyOffer(open: {
  shared: boolean;
  filePage: boolean;
  gitFlow: boolean;
  site: boolean;
  status?: HistoryStatus;
}): "history" | "enable" | "none" {
  if (open.shared) return "none";
  if (open.filePage) return open.gitFlow ? "history" : "enable";
  if (!open.site) return "none";
  return open.status === "enabled" ? "history" : open.status === "setupRequired" ? "enable" : "none";
}
