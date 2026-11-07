import type { HistoryFile } from "./history";

// What the designer offers each editor, by the permissions the server checks on a designer Save and Discard
// (docs/contracts/history-api.md, "Save"): the page needs builder:update, a template or the rules builder:templates, the
// other theme files builder:theme; Publish theme needs builder:publish.

export const PERMISSIONS = {
  update: "builder:update",
  templates: "builder:templates",
  theme: "builder:theme",
  publish: "builder:publish",
} as const;

/** The permission a part's save or discard needs; the page part of an open template is the template */
export function partPermission(part: HistoryFile, template: boolean): string {
  if (part === "page") return template ? PERMISSIONS.templates : PERMISSIONS.update;
  return part === "rules" ? PERMISSIONS.templates : PERMISSIONS.theme;
}

/** The parts a refused Save (403) had that the editor may not save, each once, in their order */
export const refusedParts = (parts: HistoryFile[], template: boolean, hasAccess: (permission: string) => boolean): HistoryFile[] =>
  [...new Set(parts)].filter((part) => !hasAccess(partPermission(part, template)));
