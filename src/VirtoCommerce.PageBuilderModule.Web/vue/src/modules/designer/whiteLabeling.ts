import type { SectionGroup } from "./types";
import type { BrandField } from "./themeLook";

// White-labeling wins over the theme, so the designer marks the fields it set to show why a change does not appear.

export type WhiteLabelingField = `brand.${BrandField}` | "palette" | "menus.header" | "menus.footer";

export const brandField = (field: BrandField): WhiteLabelingField => `brand.${field}`;

export interface GroupItem {
  group: SectionGroup;
  type: string;
  kind: "section" | "block";
}

const same = (a: GroupItem, b: GroupItem) => a.group === b.group && a.kind === b.kind && a.type === b.type;

const MENU_FIELDS: readonly (GroupItem & { field: string; overriddenBy: WhiteLabelingField })[] = [
  { group: "header", kind: "section", type: "site-header", field: "catalog_menu", overriddenBy: "menus.header" },
];

const REPLACED_ITEMS: readonly (GroupItem & { overriddenBy: WhiteLabelingField })[] = [
  { group: "footer", kind: "block", type: "footer-menu", overriddenBy: "menus.footer" },
];

export const menuField = (item: GroupItem | undefined, field: string | undefined): WhiteLabelingField | undefined =>
  item && MENU_FIELDS.find((x) => same(x, item) && x.field === field)?.overriddenBy;

export const itemField = (item: GroupItem | undefined): WhiteLabelingField | undefined =>
  item && REPLACED_ITEMS.find((x) => same(x, item))?.overriddenBy;

export const isOverridden = (overridden: readonly string[] | undefined, field: WhiteLabelingField | undefined) =>
  !!field && !!overridden?.includes(field);
