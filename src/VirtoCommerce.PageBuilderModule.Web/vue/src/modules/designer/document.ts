import type { Item, PageDocument } from "./types";
import { clone } from "./json";
import { newId } from "./schema";

/** Any stored shape as a document: JSON text, the legacy array [settings, ...sections], a PageModel; missing ids added. */
export function toDocument(raw: unknown): PageDocument {
  if (typeof raw === "string") return toDocument(raw.trim() ? JSON.parse(raw) : {});
  if (Array.isArray(raw)) {
    const [settings, ...content] = raw as Item[];
    return toDocument({ version: 1, settings: settings ?? { type: "settings" }, content });
  }
  if (raw && typeof raw === "object" && "pageContent" in raw) return toDocument((raw as { pageContent: unknown }).pageContent);
  const doc = (raw ?? {}) as Partial<PageDocument>;
  const withId = (item: Item): Item => ({ ...item, id: item.id || newId(item.type ?? "item"), ...(item.blocks ? { blocks: item.blocks.map(withId) } : {}) });
  return { ...doc, settings: doc.settings ?? ({} as Item), content: Array.isArray(doc.content) ? doc.content.map(withId) : [] };
}

/** The document as stored: a legacy array page stays an array. */
export function fromDocument(doc: PageDocument): unknown {
  if (doc.version === 1) return [doc.settings, ...doc.content];
  return doc;
}

/** A shared component placed on the page: exactly { id, type: "componentRef", componentRef }, non-blank. */
export function isComponentRef(item: Item): item is Item & { componentRef: string } {
  const keys = Object.keys(item);
  return (
    item.type === "componentRef" &&
    keys.length === 3 &&
    keys.every((k) => k === "id" || k === "type" || k === "componentRef") &&
    typeof item.componentRef === "string" &&
    !!item.componentRef.trim() &&
    !!item.id.trim()
  );
}

export interface SharedBoundary {
  placementId: string;
  componentRef: string;
  startIndex: number;
  count: number;
  label?: string;
}

const hex = (text: string) => [...text].map((c) => c.charCodeAt(0).toString(16)).join("");

/**
 * The document the preview renders: each shared-component reference replaced by the component's sections (ids made
 * unique per placement, letters and digits only), with the placements as boundaries the preview outlines.
 */
export function resolveRefs(
  doc: PageDocument,
  components: Map<string, { content: Item[]; label?: string } | null>,
): { template: PageDocument; sharedComponentBoundaries: SharedBoundary[]; missingSharedComponentIds: string[] } {
  const content: Item[] = [];
  const boundaries: SharedBoundary[] = [];
  const missing = new Set<string>();
  for (const item of doc.content) {
    if (!isComponentRef(item)) {
      content.push(item);
      continue;
    }
    const component = components.get(item.componentRef);
    if (!component) {
      missing.add(item.componentRef);
      continue;
    }
    const prefix = `lc${hex(item.id)}`;
    const sections = component.content.map((section, i) => ({
      ...clone(section),
      id: `${prefix}s${i}`,
      ...(section.blocks ? { blocks: section.blocks.map((block, j) => ({ ...clone(block), id: `${prefix}s${i}b${j}` })) } : {}),
    }));
    if (sections.length) {
      boundaries.push({ placementId: item.id, componentRef: item.componentRef, startIndex: content.length, count: sections.length, label: component.label });
    }
    content.push(...sections);
  }
  return { template: { ...doc, content }, sharedComponentBoundaries: boundaries, missingSharedComponentIds: [...missing] };
}
