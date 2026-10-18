import type { PageVersion } from "./api";
import type { SectionGroup } from "./types";

// The version history of what one Save writes: the page or template open, a template's rules, the header and the
// footer. Each file has its own history in the site's repository (a database page under "database-pages/", theme files
// under "theme/"), every change tagged with the designer action it was part of; the panel lists the actions, Shopify's
// "one save" — "Saved · Template, Header" — and restores them as a whole.

/** A file of the history: the page or template open, or a theme file it is shown with */
export type HistoryFile = "page" | "rules" | SectionGroup;

/** The store's template rules file */
export const rulesPath = (theme: string) => `${theme}/config/template_rules.json`;

/** A section group's file (Shopify's sections/header-group.json) */
export const groupPath = (theme: string, group: SectionGroup) => `${theme}/sections/${group}-group.json`;

/**
 * The files a Save of what is open writes, so the history covers: the page's own always; with the site keeping
 * versions, a template's rules and the header and footer the page shows with
 */
export function historyFiles(at: { versioned: boolean; rules: boolean; groups: boolean }): HistoryFile[] {
  if (!at.versioned) return ["page"];
  return ["page", ...(at.rules ? (["rules"] as const) : []), ...(at.groups ? (["header", "footer"] as const) : [])];
}

/**
 * The theme file a history file is: a template is one itself (`templatePath`); undefined for a database page's own
 * history
 */
export function historyPath(file: HistoryFile, theme: string | undefined, templatePath?: string): string | undefined {
  if (file === "page") return templatePath;
  if (!theme) return undefined;
  return file === "rules" ? rulesPath(theme) : groupPath(theme, file);
}

/** A theme file's history endpoint: its versions, one of them (`/{sha}`), or restoring one (`/{sha}/restore`) */
export const themeHistoryUrl = (storeId: string, path: string, sha?: string, action?: "restore", query: Record<string, string> = {}) =>
  `/api/pagebuilder/theme-history${sha ? `/${encodeURIComponent(sha)}` : ""}${action ? `/${action}` : ""}?${new URLSearchParams({ storeId, path, ...query })}`;

/** One file's versions as far as they are read, newest first */
export interface FileVersions {
  file: HistoryFile;
  versions: PageVersion[];
  /** older ones are still to be read */
  hasMore?: boolean;
}

const time = (date?: string) => (date ? Date.parse(date) : 0);

/**
 * The designer actions in the files' versions, newest first: the changes sharing a save id (or a change of its own,
 * made before saves had one) are one version, holding each file's change; it is published when every file's change it
 * holds is. Where a file has older versions still to read, the list stops at its oldest one read, so no action shows
 * with only some of its files.
 */
export function mergeSaves(sources: FileVersions[]): PageVersion[] {
  const cutoff = Math.max(-Infinity, ...sources.filter((x) => x.hasMore && x.versions.length).map((x) => time(x.versions[x.versions.length - 1].date)));
  const saves = new Map<string, PageVersion & { files: NonNullable<PageVersion["files"]> }>();
  const live = new Map<string, boolean>();
  for (const { file, versions } of sources) {
    for (const version of versions) {
      if (time(version.date) < cutoff) continue;
      const id = version.saveId ?? version.sha;
      const save = saves.get(id);
      if (save?.files.some((x) => x.file === file)) continue;
      if (!save) saves.set(id, { ...version, id, files: [{ file, sha: version.sha }] });
      else {
        save.files.push({ file, sha: version.sha });
        // the action's newest change names it
        if (time(version.date) > time(save.date)) Object.assign(save, { ...version, id, files: save.files });
      }
      live.set(id, (live.get(id) ?? true) && !!version.published);
    }
  }
  return [...saves.values()].map((x) => ({ ...x, published: live.get(x.id!)! })).sort((a, b) => time(b.date) - time(a.date));
}

/**
 * Reads the files' histories a page at a time and lists them as designer actions (mergeSaves): `more` reads the next
 * page of every file that has one, and the list is made again from all that was read.
 */
export function savesHistory(files: { file: HistoryFile; read: (after?: string) => Promise<{ versions: PageVersion[]; hasMore?: boolean; endCursor?: string }> }[]) {
  let read: (FileVersions & { cursor?: string })[] = [];
  return async (more = false) => {
    if (!more) read = files.map(({ file }) => ({ file, versions: [], hasMore: true }));
    await Promise.all(
      files.map(async ({ read: next }, i) => {
        const source = read[i];
        if (!source.hasMore) return;
        const page = await next(more ? source.cursor : undefined);
        const known = new Set(source.versions.map((x) => x.sha));
        source.versions.push(...page.versions.filter((x) => !known.has(x.sha)));
        source.hasMore = !!page.hasMore && !!page.endCursor;
        source.cursor = page.endCursor;
      }),
    );
    return { versions: mergeSaves(read), hasMore: read.some((x) => x.hasMore) };
  };
}
