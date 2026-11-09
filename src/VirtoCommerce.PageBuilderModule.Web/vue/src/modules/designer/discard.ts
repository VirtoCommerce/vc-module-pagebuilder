import type { HistoryFile, ThemePart } from "./history";
import { partPermission } from "./permissions";

// A theme part's draft is shared by every page and maybe saved by someone else, so the editor picks which ones go.

export interface DiscardOption {
  part: HistoryFile;
  checked: boolean;
  allowed: boolean;
}

export function discardOptions(input: {
  page: boolean;
  theme: ThemePart[];
  template: boolean;
  hasAccess: (permission: string) => boolean;
}): DiscardOption[] {
  const allowed = (part: HistoryFile) => input.hasAccess(partPermission(part, input.template));
  const page: DiscardOption[] = input.page ? [{ part: "page", checked: allowed("page"), allowed: allowed("page") }] : [];
  return [...page, ...input.theme.map((part) => ({ part, checked: false, allowed: allowed(part) }))];
}

export function discardChoice(options: DiscardOption[], ticked: HistoryFile[]): { page: boolean; theme: ThemePart[] } {
  const picked = options.filter((x) => x.allowed && ticked.includes(x.part)).map((x) => x.part);
  return { page: picked.includes("page"), theme: picked.filter((x): x is ThemePart => x !== "page") };
}
