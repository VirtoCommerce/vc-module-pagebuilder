import type { Item, PageDocument, SectionGroup } from "./types";

export const GROUPS: SectionGroup[] = ["header", "footer"];

// Shopify's section groups: the theme's header and footer sections, shown on every page. While a page is designed
// they are in its document, tagged with their group, the header's before the page's own sections and the footer's
// after them: selected, edited, undone like the page's. Saving writes them to the theme's group files.
export const groupOf = (item: Item) => item.__group;
/** the page's own document: no group sections */
export const pageOnly = (doc: PageDocument): PageDocument => ({ ...doc, content: doc.content.filter((x) => !groupOf(x)) });
/** a group's sections as its file keeps them */
export const groupItems = (doc: PageDocument, group: SectionGroup): Item[] =>
  doc.content.filter((x) => groupOf(x) === group).map(({ __group: _group, ...x }) => x as Item);
/** the page's document with the groups' sections around its own */
export const withGroups = (page: PageDocument, groups: Partial<Record<SectionGroup, Item[]>>): PageDocument => ({
  ...page,
  content: [
    ...(groups.header ?? []).map((x) => ({ ...x, __group: "header" as const })),
    ...page.content.filter((x) => !groupOf(x)),
    ...(groups.footer ?? []).map((x) => ({ ...x, __group: "footer" as const })),
  ],
});
