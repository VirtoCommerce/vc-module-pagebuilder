import type { Item, ItemSchema } from "./types";
import { itemTitle } from "./schema";

export interface PageAnchor {
  value: string;
  label: string;
}

/** Must match the storefront's slugifyAnchor or links break: `\w` is ASCII-only, so a non-Latin value slugifies to "". */
export function slugifyAnchor(value: string): string {
  return value
    .toLowerCase()
    .replace(/[^\w\s-]+/g, "")
    .replace(/\s+/g, "-")
    .replace(/^-+/, "")
    .replace(/-+$/, "");
}

export function anchorOf(item: Item): string {
  const typed = typeof item.anchor === "string" ? slugifyAnchor(item.anchor) : "";
  return typed || item.id;
}

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

/** Hidden items are not rendered, so they have no anchors, and shared-component content is not in this document. */
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
