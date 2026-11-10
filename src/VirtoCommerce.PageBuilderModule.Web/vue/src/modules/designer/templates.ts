import type { Item, PageDocument, Schemas, TemplateEntry } from "./types";
import { toDocument } from "./document";
import { clone } from "./json";
import { humanize, newId } from "./schema";

export type PageType = "product" | "category" | "page";
export const PAGE_TYPES: PageType[] = ["product", "category", "page"];

const KEY = /^(product|category|page)(\.[a-z0-9]+(?:-[a-z0-9]+)*)?$/;
export const isTemplateKey = (key: string) => KEY.test(key);
export const pageTypeOf = (key: string): PageType | undefined => (isTemplateKey(key) ? (key.split(".")[0] as PageType) : undefined);

export const templatePath = (theme: string, key: string) => `${theme}/templates/${key}.json`;
export function templateKeyOf(path: string): string | undefined {
  const key = /(?:^|\/)templates\/([^/]+)\.json(?:-draft)?$/.exec(path)?.[1];
  return key && isTemplateKey(key) ? key : undefined;
}

export function alternateKey(type: PageType, name: string): string | undefined {
  const slug = name
    .normalize("NFKD")
    .replace(/\p{M}+/gu, "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
  return slug ? `${type}.${slug}` : undefined;
}

export const alternateName = (key: string) => humanize(key.split(".").slice(1).join("."));

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

export const PAGE_CONTENT = "page-content";

export const MAIN_SECTIONS: Record<PageType, string> = { product: "product-info", category: "category-listing", page: PAGE_CONTENT };

const mainOf = (content: Item[], type: PageType) => content.filter((x) => !x.__group && x.type === MAIN_SECTIONS[type]);

/** A storefront adds a missing main section itself, and shows none for a hidden one. */
export function mainSectionState(content: Item[], type: PageType | undefined): "shown" | "hidden" | "missing" | undefined {
  if (!type) return undefined;
  const main = mainOf(content, type);
  if (!main.length) return "missing";
  return main.some((x) => !x.hidden) ? "shown" : "hidden";
}

export function keptMainSection(content: Item[], type: PageType | undefined, ids: string[]): string | undefined {
  if (!type) return undefined;
  const main = mainOf(content, type);
  const left = main.filter((x) => !ids.includes(x.id));
  const shown = main.filter((x) => !x.hidden);
  if (left.some((x) => !x.hidden) || (!shown.length && left.length)) return undefined;
  return main.length ? (shown.find((x) => ids.includes(x.id)) ?? main[0]).id : undefined;
}

export function hidingLosesMain(content: Item[], type: PageType | undefined, id: string): boolean {
  if (!type) return false;
  const shown = mainOf(content, type).filter((x) => !x.hidden);
  return shown.length === 1 && shown[0].id === id;
}

export function startingTemplate(type: PageType, base?: PageDocument, entry?: TemplateEntry, mainItem?: (type: string) => Item): PageDocument {
  const builtIn = entry?.default && typeof entry.default === "object" && Array.isArray((entry.default as PageDocument).content) ? toDocument(entry.default) : undefined;
  const doc = base ?? builtIn ?? { settings: {} as Item, content: [] };
  // The storefront reads a template's `content`, so the legacy array form cannot be used here.
  const { version: _version, ...copy } = clone(doc);
  const main = MAIN_SECTIONS[type];
  const content = mainOf(copy.content, type).length ? copy.content : [...copy.content, mainItem?.(main) ?? { id: newId(main), type: main }];
  return type === "page" ? { ...copy, settings: { layout: "full", ...copy.settings }, content } : { ...copy, content };
}

export const entityUrl = (type: PageType, id: string) => (type === "page" ? `/${id.replace(/^\/+/, "")}` : `/${type}/${encodeURIComponent(id)}`);
