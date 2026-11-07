import type { Item, PageDocument, Schemas, TemplateEntry } from "./types";
import { toDocument } from "./document";
import { humanize, newId } from "./schema";

// Shopify-style theme templates: the default template of a page type (product, category, page) and its named
// alternates, kept as theme files `{theme}/templates/{key}.json` and edited like pages. Which one a page gets is
// decided by the store's rules (rules.ts), which only the storefront evaluates.

/** The kinds of pages a theme template is for */
export type PageType = "product" | "category" | "page";
export const PAGE_TYPES: PageType[] = ["product", "category", "page"];

const KEY = /^(product|category|page)(\.[a-z0-9]+(?:-[a-z0-9]+)*)?$/;
/** A template key: a page type ("product"), or an alternate of it ("product.printer") */
export const isTemplateKey = (key: string) => KEY.test(key);
/** "product.printer" → "product"; undefined for what isn't a template key */
export const pageTypeOf = (key: string): PageType | undefined => (isTemplateKey(key) ? (key.split(".")[0] as PageType) : undefined);

export const templatePath = (theme: string, key: string) => `${theme}/templates/${key}.json`;
/** The template key of a theme file ("default/templates/product.printer.json"), or undefined when it isn't one */
export function templateKeyOf(path: string): string | undefined {
  const key = /(?:^|\/)templates\/([^/]+)\.json(?:-draft)?$/.exec(path)?.[1];
  return key && isTemplateKey(key) ? key : undefined;
}

/** A new alternate's key from the name typed for it: "Big printers" → "product.big-printers" */
export function alternateKey(type: PageType, name: string): string | undefined {
  const slug = name
    .normalize("NFKD")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
  return slug ? `${type}.${slug}` : undefined;
}

/** An alternate's name ("product.big-printers" → "Big printers"); a default has none (the UI says Default) */
export const alternateName = (key: string) => humanize(key.split(".").slice(1).join("."));

/**
 * The storefront's descriptor (schemas/templates/*.json) of what is open: a theme template's is the one whose preview
 * message names its page type ("page-template" for page templates), else the one keyed by it; pages use the "pages"
 * entry, and so do page templates of a storefront without their own. `key` is the descriptor's, which sections'
 * targetTemplates name; none: an empty entry.
 */
export function templateEntry(schemas: Schemas, type?: PageType): { key: string; entry: TemplateEntry } {
  const entries = Object.entries(schemas.templates ?? {});
  if (type) {
    const found = entries.find(([k, x]) => x.previewMessage?.templateKey === type || (k === type && x.type !== "pages"));
    if (found) return { key: found[0], entry: found[1] };
    if (type !== "page") return { key: type, entry: {} };
  }
  const pages = entries.find(([, x]) => x.type === "pages" && !x.previewMessage?.templateKey);
  return { key: pages?.[0] ?? "page", entry: pages?.[1] ?? {} };
}

/** What a new page template holds: the full layout, and where the page's own sections go */
export const PAGE_CONTENT = "page-content";

/** The section every template of a page type holds (the storefront's built-in.ts): it is never removed or hidden */
export const MAIN_SECTIONS: Record<PageType, string> = { product: "product-info", category: "category-listing", page: PAGE_CONTENT };

const mainOf = (content: Item[], type: PageType) => content.filter((x) => !x.__group && x.type === MAIN_SECTIONS[type]);

/** Whether the template's main section is there and shown; a storefront adds a missing one itself, and shows none for a hidden one */
export function mainSectionState(content: Item[], type: PageType | undefined): "shown" | "hidden" | "missing" | undefined {
  if (!type) return undefined;
  const main = mainOf(content, type);
  if (!main.length) return "missing";
  return main.some((x) => !x.hidden) ? "shown" : "hidden";
}

/** The main section a removal of these sections must leave in the template, if it would take the last one */
export function keptMainSection(content: Item[], type: PageType | undefined, ids: string[]): string | undefined {
  if (!type) return undefined;
  const main = mainOf(content, type);
  return main.length && main.every((x) => ids.includes(x.id)) ? main[0].id : undefined;
}

/** Whether hiding the section hides the template's last shown main section */
export function hidingLosesMain(content: Item[], type: PageType | undefined, id: string): boolean {
  if (!type) return false;
  const shown = mainOf(content, type).filter((x) => !x.hidden);
  return shown.length === 1 && shown[0].id === id;
}

/**
 * The document a template starts from: a copy of `base` (the default template, for "Create template"), else the
 * built-in one the storefront's descriptor carries (`default` as a document), else an empty one. It always has its
 * page type's main section, after its own sections (`mainItem` makes it, else a bare one); a page template also has
 * the full layout.
 */
export function startingTemplate(type: PageType, base?: PageDocument, entry?: TemplateEntry, mainItem?: (type: string) => Item): PageDocument {
  const builtIn = entry?.default && typeof entry.default === "object" && Array.isArray((entry.default as PageDocument).content) ? toDocument(entry.default) : undefined;
  const doc = base ?? builtIn ?? { settings: {} as Item, content: [] };
  // never the legacy array form (version 1): the storefront reads a template's `content`
  const { version: _version, ...copy } = JSON.parse(JSON.stringify(doc)) as PageDocument;
  const main = MAIN_SECTIONS[type];
  const content = mainOf(copy.content, type).length ? copy.content : [...copy.content, mainItem?.(main) ?? { id: newId(main), type: main }];
  return type === "page" ? { ...copy, settings: { layout: "full", ...copy.settings }, content } : { ...copy, content };
}

/** Where the preview shows a template: a product's or category's page by id, or a page's permalink */
export const entityUrl = (type: PageType, id: string) => (type === "page" ? `/${id.replace(/^\/+/, "")}` : `/${type}/${encodeURIComponent(id)}`);
