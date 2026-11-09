import type { Field, Item, PageDocument, Schemas, SectionGroup, Selection } from "./types";
import type { SharedBoundary } from "./document";
import { isRecord, isText, isTextList } from "./json";
import { rulesOf, type TemplateRules } from "./rules";
import { PAGE_TYPES, type PageType } from "./templates";
import type { Brand, Palette } from "./themeLook";

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

/** The light and dark switch, as the canvas bar and the Theme tab's palettes show it */
export const COLOR_SCHEMES: readonly { id: ColorScheme; icon: string; label: string }[] = [
  { id: "light", icon: "lucide-sun", label: "PAGE_BUILDER.DESIGNER.LIGHT" },
  { id: "dark", icon: "lucide-moon", label: "PAGE_BUILDER.DESIGNER.DARK" },
];

/** The theme as the designer has it, saved or not; a part left out comes from the published snapshot */
export interface ThemeDraft {
  rules?: TemplateRules;
  templates?: Record<string, PageDocument>;
  header?: { content: Item[] };
  footer?: { content: Item[] };
  brand?: Brand;
  /** null: the theme has no palette, the storefront's preset shows */
  palette?: Palette | null;
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
  | { type: "hover"; sectionId: string | null };

/** A template and the rule that chose it; null: no rule did */
export interface TemplateChoice {
  template: string;
  ruleId: string | null;
}

/**
 * The template the storefront rendered for the page it shows and the rule that chose it (an open template: that
 * template, no rule), and in `rules` the template the store's rules pick for the page, the open template aside.
 */
export interface Resolved extends TemplateChoice {
  url: string;
  pageType: string;
  rules: TemplateChoice;
  /** the theme fields white-labeling set for the visitor, which the theme does not change */
  overriddenByWhiteLabeling: string[];
}

/**
 * What the store's rules pick for the page the preview shows, with the rule as the designer lists it: its index; null:
 * no rule chose (the page type's default); undefined: a rule the designer doesn't have
 */
export interface RulesPick extends TemplateChoice {
  url: string;
  pageType: string;
  rule: number | null | undefined;
}

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

const templateChoice = (data: Record<string, unknown>): TemplateChoice | undefined =>
  isText(data.template) && (data.ruleId === null || isText(data.ruleId)) ? { template: data.template, ruleId: data.ruleId } : undefined;

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
    case "resolved": {
      if (!isRecord(data) || typeof data.url !== "string" || typeof data.pageType !== "string" || !isTextList(data.overriddenByWhiteLabeling)) return undefined;
      const rendered = templateChoice(data);
      const rules = isRecord(data.rules) ? templateChoice(data.rules) : undefined;
      return rendered && rules
        ? { type, resolved: { url: data.url, pageType: data.pageType, ...rendered, rules, overriddenByWhiteLabeling: data.overriddenByWhiteLabeling } }
        : undefined;
    }
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

/**
 * A path on the storefront, on a placeholder host: only the path, query and hash of `path` are taken, so
 * "//evil.example" or "https://evil.example" can't change the host. Throws when the path can't be a URL's.
 */
export const storefrontPath = (path: string): URL => new URL(`/${path.replace(/^[/\\]+/, "")}`, "http://storefront.invalid");

/** A storefront route as the preview takes it (from its root): only the path, query and hash of `path` */
export function storefrontRoute(path: string): string | undefined {
  try {
    const url = storefrontPath(path);
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
  /** every template of the theme as the designer has it; none: the published ones */
  templates?: Record<string, PageDocument>;
  /** the brand and palette as the Theme tab has them; none: not loaded */
  look?: { brand?: Brand; palette?: Palette | null };
  capabilities: string[];
}

/** The theme the preview shows in place of the published one; the templates and the groups only to a storefront that renders them */
export function draftMessage(input: DraftInput): DraftMessage {
  const groups = input.capabilities.includes("groups") ? input.groups : undefined;
  const templates = input.capabilities.includes("templates") ? input.templates : undefined;
  return {
    type: "draft",
    theme: {
      ...(input.rules ? { rules: input.rules } : {}),
      ...(templates ? { templates } : {}),
      ...(groups?.header ? { header: { content: groups.header } } : {}),
      ...(groups?.footer ? { footer: { content: groups.footer } } : {}),
      ...(input.look?.brand ? { brand: input.look.brand } : {}),
      ...(input.look?.palette !== undefined ? { palette: input.look.palette } : {}),
    },
  };
}

export type AuthMessage = Extract<DesignerMessage, { type: "auth" }>;

/** The preview renders as an anonymous visitor */
export const ANONYMOUS: AuthMessage = { type: "auth", token: null, userId: null };

/** The preview renders as the user: the access token only, never a refresh token */
export const authMessage = (userId: string, accessToken: string): AuthMessage => ({ type: "auth", token: { access_token: accessToken }, userId });

/** What goes out after every `loaded`, in this order: a reloaded frame has lost all of it */
export function handshakeMessages(state: {
  auth?: AuthMessage;
  inspector: boolean;
  colorScheme: ColorScheme;
  draft: DraftMessage;
  render: RenderMessage;
}): DesignerMessage[] {
  return [state.auth ?? ANONYMOUS, { type: "inspector", enabled: state.inspector }, { type: "colorScheme", scheme: state.colorScheme }, state.draft, state.render];
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

/** The rules' pick `resolved` reports, with the designer's rule that made it; the rendered template is not it while a template is open */
export function rulesPick(resolved: Resolved, rules: TemplateRules | undefined): RulesPick {
  const { url, pageType } = resolved;
  const { template, ruleId } = resolved.rules;
  return { url, pageType, template, ruleId, rule: ruleIndex(rules, pageType, ruleId) };
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
    // an editor who may not upload it (without builder:update) leaves it to one who may
    if ((e as { status?: unknown } | undefined)?.status === 403) hashes.add(catalog.hash);
    else console.warn("[page-builder] The storefront's schema catalog was not uploaded; it is tried again when the preview reloads", e);
  }
}
