import type { SectionGroup } from "./types";
import type { BrandField } from "./themeLook";

// White-labeling wins over the theme (docs/contracts/theme-snapshot.md, Brand; preview-protocol.md, `resolved`): the preview reports
// the fields it set for the visitor, and the designer marks them, so that the editor sees why a change does not show.

export type WhiteLabelingField = `brand.${BrandField}` | "palette" | "menus.header" | "menus.footer";

export const brandField = (field: BrandField): WhiteLabelingField => `brand.${field}`;

/** The item whose settings are edited: a section of a group, or a block of one; none for a page's own items */
export interface GroupItem {
  group: SectionGroup;
  type: string;
  kind: "section" | "block";
}

const same = (a: GroupItem, b: GroupItem) => a.group === b.group && a.kind === b.kind && a.type === b.type;

/** The menu settings white-labeling replaces: its main menu the header's category menu, not the icons menu */
const MENU_FIELDS: readonly (GroupItem & { field: string; overriddenBy: WhiteLabelingField })[] = [
  { group: "header", kind: "section", type: "site-header", field: "catalog_menu", overriddenBy: "menus.header" },
];

/** The items white-labeling replaces whole: its footer links are the footer's columns, heading and links included */
const REPLACED_ITEMS: readonly (GroupItem & { overriddenBy: WhiteLabelingField })[] = [
  { group: "footer", kind: "block", type: "footer-menu", overriddenBy: "menus.footer" },
];

/** The white-labeling field a `menu` setting of the item is, when white-labeling replaces it */
export const menuField = (item: GroupItem | undefined, field: string | undefined): WhiteLabelingField | undefined =>
  item && MENU_FIELDS.find((x) => same(x, item) && x.field === field)?.overriddenBy;

/** The white-labeling field the whole item is, when white-labeling replaces it */
export const itemField = (item: GroupItem | undefined): WhiteLabelingField | undefined =>
  item && REPLACED_ITEMS.find((x) => same(x, item))?.overriddenBy;

/** Whether white-labeling set the field for the visitor the preview shows */
export const isOverridden = (overridden: readonly string[] | undefined, field: WhiteLabelingField | undefined) =>
  !!field && !!overridden?.includes(field);
