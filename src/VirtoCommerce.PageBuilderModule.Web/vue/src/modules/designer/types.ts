import type { InjectionKey, Ref } from "vue";
import type { PageAnchor } from "./anchors";

/** A page as the designer edits it and the storefront preview renders it: page settings plus a list of sections. */
export interface PageDocument {
  version?: number;
  settings: Item;
  content: Item[];
}

/** A section (or a block inside a section): its schema type, a unique id, and the values of its fields. */
export interface Item {
  id: string;
  type: string;
  hidden?: boolean;
  blocks?: Item[];
  /** while a page is designed: the section group (header, footer) the section belongs to, none for the page's own */
  __group?: SectionGroup;
  [field: string]: unknown;
}

/** Shopify's section groups: the theme's header and footer, shown on every page */
export type SectionGroup = "header" | "footer";

/** One field of a schema; `element` holds the fields of an object or of a list row. */
export interface Field {
  id?: string;
  type: string;
  label?: string;
  title?: string;
  tab?: string;
  sort?: number;
  default?: unknown;
  placeholder?: string;
  info?: string;
  required?: boolean;
  hidden?: boolean;
  multiline?: boolean;
  multiple?: boolean;
  min?: number;
  max?: number;
  step?: number;
  options?: { value: unknown; label: string }[];
  element?: Field[];
  displayField?: string;
  /** a JS expression, true when the field shows (this = the field's context) */
  visibility?: string;
  /** remote options of a select: a templated request */
  request?: RemoteRequest;
  equalKey?: string;
  searchable?: boolean;
  [key: string]: unknown;
}

export interface RemoteRequest {
  url: string;
  method?: string;
  body?: unknown;
  response?: { result?: string };
  /** the option's label and value keys in the result rows */
  label?: string;
  value?: string;
}

/** A section's or block's schema (the storefront's schemas/sections/*.json, blocks/*.json). */
export interface ItemSchema {
  name?: string;
  icon?: string;
  displayField?: string;
  settings?: Field[];
  includeShared?: string[];
  excludeShared?: boolean | string[];
  /** block types a section can hold */
  blocks?: string[];
  targetTemplates?: string[];
  /** a page-level section: its fields are the page settings, it isn't in the list of sections */
  static?: boolean | "top" | "bottom";
  /** the section groups the section is for (Shopify's `enabled_on.groups`); such a section isn't offered for pages */
  groups?: SectionGroup[];
  default?: Record<string, unknown>;
  preview?: Record<string, unknown>;
  group?: string;
  hidden?: boolean;
  type?: string;
}

/** What the storefront preview announces in its 'loaded' message. */
export interface Schemas {
  sections: Record<string, ItemSchema>;
  blocks: Record<string, ItemSchema>;
  shared: Record<string, ItemSchema>;
  objects: Record<string, ItemSchema>;
  templates?: Record<string, TemplateEntry>;
  settingsSchema?: { settings_schema?: { name: string; icon?: string; settings: Field[] }[] };
}

/**
 * What the settings panel shows: a page-level row (key "settings": the page template's fields; else a static
 * section's type, e.g. "page-header"), a section, or a block of a section.
 */
export type Selection = { kind: "page"; key: string } | { kind: "section"; id: string } | { kind: "block"; sectionId: string; id: string };

/** A page template entry (the storefront's schemas/templates/*.json) */
export interface TemplateEntry {
  name?: string;
  type?: string;
  /** the section types allowed on such pages */
  sections?: string[];
  /** the page-level fields (header, hideBreadcrumbs, …) */
  settings?: Field[];
}

/** Opens the store's assets library as a picker; resolves to the chosen file's URL, or undefined when cancelled */
export const PickAssetKey: InjectionKey<() => Promise<string | undefined>> = Symbol("PickAsset");
/** the page's anchors, for links in rich text */
export const PageAnchorsKey: InjectionKey<Ref<PageAnchor[]>> = Symbol("PageAnchors");
