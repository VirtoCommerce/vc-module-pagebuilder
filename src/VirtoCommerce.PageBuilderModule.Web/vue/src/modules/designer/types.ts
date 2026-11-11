import type { InjectionKey, Ref } from "vue";
import type { PageAnchor } from "./anchors";

export interface PageDocument {
  version?: number;
  settings: Item;
  content: Item[];
}

export interface Item {
  id: string;
  type: string;
  hidden?: boolean;
  blocks?: Item[];
  __group?: SectionGroup;
  [field: string]: unknown;
}

export type SectionGroup = "header" | "footer";

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
  format?: string;
  multiple?: boolean;
  min?: number;
  max?: number;
  step?: number;
  options?: { value: unknown; label: string }[];
  element?: Field[];
  displayField?: string;
  visibility?: string;
  request?: RemoteRequest;
  equalKey?: string;
  searchable?: boolean;
  translatable?: boolean;
  [key: string]: unknown;
}

export interface RemoteRequest {
  url: string;
  method?: string;
  body?: unknown;
  response?: { result?: string };
  label?: string;
  value?: string;
}

export interface ItemSchema {
  name?: string;
  icon?: string;
  displayField?: string;
  settings?: Field[];
  includeShared?: string[];
  excludeShared?: boolean | string[];
  blocks?: string[];
  targetTemplates?: string[];
  /** A page-level section: its fields are the page settings, and it is not in the list of sections. */
  static?: boolean | "top" | "bottom";
  groups?: SectionGroup[];
  /** At most this many sections of the type on a page, its header and footer counted in. */
  limit?: number;
  default?: Record<string, unknown>;
  preview?: Record<string, unknown>;
  group?: string;
  hidden?: boolean;
  type?: string;
}

export interface Schemas {
  sections: Record<string, ItemSchema>;
  blocks: Record<string, ItemSchema>;
  shared: Record<string, ItemSchema>;
  objects: Record<string, ItemSchema>;
  templates?: Record<string, TemplateEntry>;
}

export type Selection = { kind: "page"; key: string } | { kind: "section"; id: string } | { kind: "block"; sectionId: string; id: string };

export interface TemplateEntry {
  name?: string;
  type?: string;
  sections?: string[];
  settings?: Field[];
  path?: string;
  previewUrl?: string;
  previewMessage?: { templateKey?: string };
  default?: unknown;
}

export const PickAssetKey: InjectionKey<(acceptedTypes?: string[]) => Promise<string | undefined>> = Symbol("PickAsset");
export const PageAnchorsKey: InjectionKey<Ref<PageAnchor[]>> = Symbol("PageAnchors");
export const StoreMenusKey: InjectionKey<() => Promise<unknown[]>> = Symbol("StoreMenus");
export const PaletteTokensKey: InjectionKey<Ref<{ tokens: string[]; colors: Record<string, string> }>> = Symbol("PaletteTokens");
export const WhiteLabelingKey: InjectionKey<Ref<string[]>> = Symbol("WhiteLabeling");
