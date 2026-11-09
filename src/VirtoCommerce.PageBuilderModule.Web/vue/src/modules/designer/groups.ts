import type { Item, PageDocument, SectionGroup } from "./types";

export const GROUPS: SectionGroup[] = ["header", "footer"];

// The header and footer sections sit in the page's document while it is designed, so they edit and undo like its own.
export const groupOf = (item: Item) => item.__group;
export const pageOnly = (doc: PageDocument): PageDocument => ({ ...doc, content: doc.content.filter((x) => !groupOf(x)) });
export const groupItems = (doc: PageDocument, group: SectionGroup): Item[] =>
  doc.content.filter((x) => groupOf(x) === group).map(({ __group: _group, ...x }) => x as Item);
export const withGroups = (page: PageDocument, groups: Partial<Record<SectionGroup, Item[]>>): PageDocument => ({
  ...page,
  content: [
    ...(groups.header ?? []).map((x) => ({ ...x, __group: "header" as const })),
    ...page.content.filter((x) => !groupOf(x)),
    ...(groups.footer ?? []).map((x) => ({ ...x, __group: "footer" as const })),
  ],
});
