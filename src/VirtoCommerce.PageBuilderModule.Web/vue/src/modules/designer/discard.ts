import type { HistoryFile, ThemePart } from "./history";
import { partPermission } from "./permissions";

// Discard throws saved drafts away. The page's is this editor's; a theme part's (the header, the footer, the rules, the
// theme settings, the brand, the palettes) is the store's, shared by every page and maybe saved by someone else: the
// editor picks which go, and only those they may edit can be picked.

/** A draft the Discard dialog offers */
export interface DiscardOption {
  part: HistoryFile;
  /** ticked at first: the page's draft, never a theme part's */
  checked: boolean;
  /** the editor may discard it: they may save it */
  allowed: boolean;
}

/** What the drafts are: the page's or template's, and the theme parts with one */
export function discardOptions(input: {
  page: boolean;
  theme: ThemePart[];
  /** the page part is a template */
  template: boolean;
  hasAccess: (permission: string) => boolean;
}): DiscardOption[] {
  const allowed = (part: HistoryFile) => input.hasAccess(partPermission(part, input.template));
  const page: DiscardOption[] = input.page ? [{ part: "page", checked: allowed("page"), allowed: allowed("page") }] : [];
  return [...page, ...input.theme.map((part) => ({ part, checked: false, allowed: allowed(part) }))];
}

/** What a Discard throws away of the ticked parts: those offered and allowed */
export function discardChoice(options: DiscardOption[], ticked: HistoryFile[]): { page: boolean; theme: ThemePart[] } {
  const picked = options.filter((x) => x.allowed && ticked.includes(x.part)).map((x) => x.part);
  return { page: picked.includes("page"), theme: picked.filter((x): x is ThemePart => x !== "page") };
}
