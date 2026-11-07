import type { SectionGroup } from "./types";
import type { BrandField } from "./themeLook";

// White-labeling wins over the theme field by field (docs/contracts/preview-protocol.md, `resolved`): the preview reports
// the fields it set for the visitor, and the designer marks them, so that the editor sees why a change does not show.

export type WhiteLabelingField = `brand.${BrandField}` | "palette" | "menus.header" | "menus.footer";

export const brandField = (field: BrandField): WhiteLabelingField => `brand.${field}`;

/** The field a `menu` setting of a section of the group is: white-labeling's main menu or footer links replace them */
export const menuField = (group: SectionGroup | undefined): WhiteLabelingField | undefined => (group ? `menus.${group}` : undefined);

/** Whether white-labeling set the field for the visitor the preview shows */
export const isOverridden = (overridden: readonly string[] | undefined, field: WhiteLabelingField | undefined) =>
  !!field && !!overridden?.includes(field);
