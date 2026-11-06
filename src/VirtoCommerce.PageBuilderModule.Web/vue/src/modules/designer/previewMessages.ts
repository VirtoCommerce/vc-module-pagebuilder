import type { Field, Item, PageDocument, Schemas, SectionGroup, Selection } from "./types";
import type { SharedBoundary } from "./document";
import { rulesOf, type TemplateRules } from "./rules";
import { PAGE_TYPES, type PageType } from "./templates";

// Preview protocol 2 (docs/contracts/preview-protocol.md) as the storefront speaks it (vc-frontend
// plugins/builder-preview): what the designer sends its preview and what it takes from it, built and checked here.

export const PREVIEW_PROTOCOL = 2;

/** What a storefront can do, as it announces in `loaded` */
export type Capability = "templates" | "layouts" | "groups" | "inline-edit" | "color-scheme" | "inspector";

/** A template rule condition the storefront knows; the designer builds its form from `value` */
export interface ConditionDescriptor {
  name: string;
  pageTypes: string[];
  label?: string;
  value: Field;
}

export interface PaletteTokens {
  light: string[];
  dark: string[];
}

/** The storefront's schemas, named by their hash: the `loaded` data without what only the handshake needs */
export type Catalog = Schemas & { hash: string; paletteTokens: PaletteTokens; [key: string]: unknown };

/** What the designer keeps of a storefront's handshake besides its schemas */
export interface Handshake {
  hash: string;
  capabilities: string[];
  conditions: ConditionDescriptor[];
  paletteTokens: PaletteTokens;
}

export interface Loaded extends Handshake {
  catalog: Catalog;
}

export type ColorScheme = "light" | "dark";

/** The theme as the designer has it, saved or not; a part left out comes from the published snapshot */
export interface ThemeDraft {
  rules?: TemplateRules;
  templates?: Record<string, PageDocument>;
  header?: { content: Item[] };
  footer?: { content: Item[] };
}

export interface DraftMessage {
  type: "draft";
  theme: ThemeDraft;
}

/** What a content page's rules see when it is not at a route of its own */
export interface PageSubject {
  permalink: string;
  objectType: "Pages" | "ContentFile";
}

export interface RenderMessage {
  type: "render";
  document: PageDocument;
  templateKey?: string;
  url?: string;
  page?: PageSubject;
  cultureName: string;
  selection?: { sectionId: string; blockId?: string; scroll: boolean };
  model?: Item;
  sectionLabels?: Record<string, string>;
  sharedComponentBoundaries?: SharedBoundary[];
}

export type DesignerMessage =
  | { type: "connect"; protocol: typeof PREVIEW_PROTOCOL }
  | { type: "auth"; token: { access_token: string } | null; userId: string | null }
  | { type: "inspector"; enabled: boolean }
  | { type: "colorScheme"; scheme: ColorScheme }
  | DraftMessage
  | RenderMessage
  | { type: "hover"; sectionId: string | null }
  // the theme settings until the draft carries brand and palette: the storefront applies it in both protocols
  | { type: "settings"; settings: Record<string, unknown> };

/** The template the storefront rendered for the page it shows, and the rule that chose it */
export interface Resolved {
  url: string;
  pageType: string;
  template: string;
  ruleId: string | null;
  /** the theme fields white-labeling set for the visitor, which the theme does not change */
  overriddenByWhiteLabeling: string[];
}

/** `resolved` with the rule as the designer lists it: its index; null: no rule chose; undefined: one it doesn't have */
export type ResolvedRule = Resolved & { rule: number | null | undefined };

export interface TextEdit {
  itemId: string;
  path: (string | number)[];
  value: string;
  /** a translatable setting's language */
  cultureName?: string;
}

export type PreviewEvent =
  | { type: "loaded"; loaded: Loaded | "unsupported" }
  | { type: "select"; sectionId: string; blockId?: string }
  | { type: "hover"; sectionId: string | null }
  | ({ type: "edit" } & TextEdit)
  | { type: "resolved"; resolved: Resolved };

const isRecord = (value: unknown): value is Record<string, unknown> => !!value && typeof value === "object" && !Array.isArray(value);
const isText = (value: unknown): value is string => typeof value === "string" && !!value.trim();
const isTextList = (value: unknown): value is string[] => Array.isArray(value) && value.every((x) => typeof x === "string");

const SCHEMA_KINDS = ["sections", "blocks", "shared", "objects"] as const;

const isCondition = (value: unknown): value is ConditionDescriptor =>
  isRecord(value) && isText(value.name) && isTextList(value.pageTypes) && (value.label === undefined || typeof value.label === "string") && isRecord(value.value);

/**
 * A storefront's `loaded`: its catalog and handshake; "unsupported" when it doesn't speak protocol 2 (an older or
 * unchanged storefront); undefined when it isn't the shape the protocol gives it.
 */
export function parseLoaded(data: unknown): Loaded | "unsupported" | undefined {
  if (!isRecord(data)) return undefined;
  if (data.protocol !== PREVIEW_PROTOCOL) return "unsupported";
  const { protocol: _protocol, capabilities, conditions, ...catalog } = data;
  const tokens = catalog.paletteTokens;
  if (
    !isText(catalog.hash) ||
    !SCHEMA_KINDS.every((kind) => isRecord(catalog[kind])) ||
    (catalog.templates !== undefined && !isRecord(catalog.templates)) ||
    !isRecord(tokens) ||
    !isTextList(tokens.light) ||
    !isTextList(tokens.dark) ||
    !isTextList(capabilities) ||
    !Array.isArray(conditions)
  )
    return undefined;
  // a condition the designer can't read is left out, not the storefront
  const known = conditions.filter(isCondition);
  if (known.length !== conditions.length) console.warn("[page-builder] Left out template rule conditions the preview announced in an unknown shape");
  return { catalog: catalog as Catalog, hash: catalog.hash, capabilities, conditions: known, paletteTokens: { light: tokens.light, dark: tokens.dark } };
}

function parseEvent(type: unknown, data: unknown): PreviewEvent | undefined {
  switch (type) {
    case "loaded": {
      const loaded = parseLoaded(data);
      return loaded && { type, loaded };
    }
    case "select":
      return isRecord(data) && isText(data.sectionId) && (data.blockId === undefined || isText(data.blockId))
        ? { type, sectionId: data.sectionId, ...(data.blockId ? { blockId: data.blockId } : {}) }
        : undefined;
    case "hover":
      return isRecord(data) && (data.sectionId === null || isText(data.sectionId)) ? { type, sectionId: data.sectionId } : undefined;
    case "edit": {
      if (!isRecord(data) || !isText(data.itemId) || typeof data.value !== "string") return undefined;
      const path = data.path;
      if (!Array.isArray(path) || !path.length || !path.every((x) => typeof x === "string" || typeof x === "number")) return undefined;
      if (data.cultureName !== undefined && typeof data.cultureName !== "string") return undefined;
      return { type, itemId: data.itemId, path, value: data.value, ...(data.cultureName !== undefined ? { cultureName: data.cultureName } : {}) };
    }
    case "resolved":
      return isRecord(data) &&
        typeof data.url === "string" &&
        typeof data.pageType === "string" &&
        isText(data.template) &&
        (data.ruleId === null || isText(data.ruleId)) &&
        isTextList(data.overriddenByWhiteLabeling)
        ? {
            type,
            resolved: { url: data.url, pageType: data.pageType, template: data.template, ruleId: data.ruleId, overriddenByWhiteLabeling: data.overriddenByWhiteLabeling },
          }
        : undefined;
    default:
      return undefined;
  }
}

/** A message from the preview (`{ source: "preview", type, data }`); one of another shape is dropped with a warning */
export function parsePreviewMessage(message: unknown): PreviewEvent | undefined {
  if (!isRecord(message) || message.source !== "preview") return undefined;
  const event = parseEvent(message.type, message.data);
  if (!event) console.warn("[page-builder] Dropped a preview message that does not match the preview protocol", message.type);
  return event;
}

/** A storefront route as the preview takes it (from its root): only the path, query and hash of `path` */
export function storefrontRoute(path: string): string | undefined {
  try {
    const url = new URL(`/${path.replace(/^[/\\]+/, "")}`, "http://storefront.invalid");
    return url.pathname + url.search + url.hash;
  } catch {
    return undefined;
  }
}

export interface RenderInput {
  /** the page's own sections with shared components filled in, and where each placement's sections are */
  preview: { template: PageDocument; sharedComponentBoundaries: SharedBoundary[] };
  cultureName: string;
  /** a theme template: its key, and the storefront page it shows on */
  templateKey?: string;
  url?: string;
  /** a content page: what its rules see */
  page?: PageSubject;
  selection?: Selection | null;
  /** the selection is brought into view */
  scroll?: boolean;
  /** the section the add-section list shows on hover */
  model?: Item;
  sectionLabels?: Record<string, string>;
}

function selectionOf(input: RenderInput): RenderMessage["selection"] {
  const sel = input.selection;
  if (!sel || sel.kind === "page") return undefined;
  const id = sel.kind === "block" ? sel.sectionId : sel.id;
  // a shared component's placement isn't in the document: its first section stands for it
  const placement = input.preview.sharedComponentBoundaries.find((x) => x.placementId === id);
  const sectionId = placement ? input.preview.template.content[placement.startIndex]?.id : id;
  return sectionId ? { sectionId, ...(sel.kind === "block" ? { blockId: sel.id } : {}), scroll: !!input.scroll } : undefined;
}

/** One render: what the preview shows, on which page, in which language, with the selection outlined */
export function renderMessage(input: RenderInput): RenderMessage {
  const { template, sharedComponentBoundaries } = input.preview;
  const url = input.url === undefined ? undefined : storefrontRoute(input.url);
  const selection = selectionOf(input);
  return {
    type: "render",
    document: { settings: template.settings ?? ({} as Item), content: template.content },
    ...(input.templateKey ? { templateKey: input.templateKey } : {}),
    ...(url ? { url } : {}),
    ...(input.page ? { page: input.page } : {}),
    cultureName: input.cultureName,
    ...(selection ? { selection } : {}),
    ...(input.model ? { model: input.model } : {}),
    ...(input.sectionLabels ? { sectionLabels: input.sectionLabels } : {}),
    ...(sharedComponentBoundaries.length ? { sharedComponentBoundaries } : {}),
  };
}

export interface DraftInput {
  /** the rules as the designer has them; none: not loaded */
  rules?: TemplateRules;
  /** the header's and footer's sections; none: the page has no Header and Footer */
  groups?: Partial<Record<SectionGroup, Item[]>>;
  templates?: Record<string, PageDocument>;
  capabilities: string[];
}

/** The theme the preview shows in place of the published one; the groups only to a storefront that renders them */
export function draftMessage(input: DraftInput): DraftMessage {
  const groups = input.capabilities.includes("groups") ? input.groups : undefined;
  return {
    type: "draft",
    theme: {
      ...(input.rules ? { rules: input.rules } : {}),
      ...(input.templates ? { templates: input.templates } : {}),
      ...(groups?.header ? { header: { content: groups.header } } : {}),
      ...(groups?.footer ? { footer: { content: groups.footer } } : {}),
    },
  };
}

/** The preview renders as an anonymous visitor */
export const ANONYMOUS: DesignerMessage = { type: "auth", token: null, userId: null };

/** What goes out after every `loaded`, in this order: a reloaded frame has lost all of it */
export function handshakeMessages(state: { inspector: boolean; colorScheme: ColorScheme; draft: DraftMessage; render: RenderMessage }): DesignerMessage[] {
  return [ANONYMOUS, { type: "inspector", enabled: state.inspector }, { type: "colorScheme", scheme: state.colorScheme }, state.draft, state.render];
}

/** What a click in the preview selects: the block when it names one the page has, else the section */
export function selectionFrom(doc: PageDocument, event: { sectionId: string; blockId?: string }): Selection {
  const blockId = event.blockId;
  const section = blockId ? doc.content.find((x) => x.blocks?.some((b) => b.id === blockId)) : undefined;
  return section && blockId ? { kind: "block", sectionId: section.id, id: blockId } : { kind: "section", id: event.sectionId };
}

/**
 * The index of the designer's rule the storefront names in `resolved`, by its id, among the page type's rules (its
 * defaults when the file has no list for it). null: no rule chose; undefined: a rule the designer doesn't have.
 */
export function ruleIndex(rules: TemplateRules | undefined, pageType: string, ruleId: string | null): number | null | undefined {
  if (ruleId === null) return null;
  if (!rules || !(PAGE_TYPES as string[]).includes(pageType)) return undefined;
  const index = rulesOf(rules, pageType as PageType).findIndex((rule) => rule.id === ruleId);
  return index < 0 ? undefined : index;
}

export interface CatalogApi {
  /** whether the server keeps the store's catalog with that hash */
  has(storeId: string, hash: string): Promise<boolean>;
  upload(storeId: string, catalog: Catalog): Promise<void>;
}

/** per store, the catalog hashes the server has (or this editor may not upload), for the session */
const knownCatalogs = new Map<string, Set<string>>();

/**
 * Makes sure the server has the catalog of the storefront previewed: saves, publications and agents check the theme
 * against it. Never throws: a failed upload is tried again on the next `loaded`.
 */
export async function ensureCatalog(storeId: string, catalog: Catalog, api: CatalogApi, known = knownCatalogs): Promise<void> {
  const hashes = known.get(storeId) ?? new Set<string>();
  known.set(storeId, hashes);
  if (hashes.has(catalog.hash)) return;
  try {
    if (!(await api.has(storeId, catalog.hash))) await api.upload(storeId, catalog);
    hashes.add(catalog.hash);
  } catch (e) {
    // an editor without builder:theme leaves the upload to one who has it
    if ((e as { status?: unknown } | undefined)?.status === 403) hashes.add(catalog.hash);
    else console.warn("[page-builder] The storefront's schema catalog was not uploaded; it is tried again when the preview reloads", e);
  }
}
