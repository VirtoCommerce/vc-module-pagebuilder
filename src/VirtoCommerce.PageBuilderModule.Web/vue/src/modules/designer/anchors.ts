import type { Item, ItemSchema } from "./types";
import { itemTitle } from "./schema";

/** A place on the page a rich-text link can jump to: `#value`, shown as its item's name */
export interface PageAnchor {
  value: string;
  label: string;
}

/**
 * The storefront's `slugifyAnchor` (vc-frontend-next `shared/static-content/anchors.ts`): a link resolves only while
 * both sides derive the same string, so its quirks are kept: `\w` is ASCII-only, so a value without Latin letters
 * slugifies to "" and the item's id is used instead.
 */
export function slugifyAnchor(value: string): string {
  return value
    .toLowerCase()
    .replace(/[^\w\s-]+/g, "")
    .replace(/\s+/g, "-")
    .replace(/^-+/, "")
    .replace(/-+$/, "");
}

/** The anchor the storefront renders for a section or block: its typed `anchor`, slugified, else its id */
export function anchorOf(item: Item): string {
  const typed = typeof item.anchor === "string" ? slugifyAnchor(item.anchor) : "";
  return typed || item.id;
}

/** Ids and `<a name>`s inside rich text, anywhere in a setting value (objects and lists too) */
function textAnchors(value: unknown): string[] {
  if (typeof value === "string") {
    if (!value.includes("<") || typeof DOMParser === "undefined") return [];
    const html = new DOMParser().parseFromString(value, "text/html");
    return [...html.querySelectorAll("a[name]")].map((x) => x.getAttribute("name") ?? "")
      .concat([...html.querySelectorAll("[id]")].map((x) => x.id))
      .map((x) => x.trim());
  }
  if (Array.isArray(value)) return value.flatMap(textAnchors);
  if (value && typeof value === "object") return Object.values(value).flatMap(textAnchors);
  return [];
}

/**
 * Every anchor of the page, in page order and once each: the visible sections and blocks, and the anchors in their
 * rich text. Hidden items aren't rendered, so they have none; shared-component placements are left out (their
 * content isn't in this document).
 */
export function pageAnchors(content: Item[], schemaOf: (item: Item, kind: "section" | "block") => ItemSchema | undefined): PageAnchor[] {
  const anchors = new Map<string, string>();
  const add = (item: Item, kind: "section" | "block") => {
    const label = itemTitle(item, schemaOf(item, kind));
    const { blocks: _blocks, ...settings } = item;
    for (const value of [anchorOf(item), ...textAnchors(settings)]) {
      if (value && !anchors.has(value)) anchors.set(value, label);
    }
  };
  for (const section of content) {
    if (section.hidden || section.type === "componentRef") continue;
    add(section, "section");
    for (const block of section.blocks ?? []) if (!block.hidden) add(block, "block");
  }
  return [...anchors].map(([value, label]) => ({ value, label }));
}
