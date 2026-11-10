import { clone } from "./json";
import { evaluate, UnsupportedExpressionError } from "./safeExpression";
import type { Field, Item, ItemSchema, RemoteRequest, Schemas } from "./types";
import { displayText, isTranslations, withTranslation, type Translating } from "./translatable";

export function fieldsOf(schema: ItemSchema | undefined, kind: "section" | "block", schemas: Schemas): Field[] {
  if (!schema) return [];
  const own = schema.settings ?? [];
  const has = (id?: string) => !!id && own.some((x) => x.id === id);
  const base = schemas.shared[kind === "section" ? "_sections" : "_blocks"]?.settings ?? [];
  const included = (schema.includeShared ?? []).flatMap((key) => schemas.shared[key]?.settings ?? []);
  const exclude = schema.excludeShared;
  const shared = exclude === true ? included : [...base, ...included].filter((x) => !(Array.isArray(exclude) && x.id && exclude.includes(x.id)));
  return [...own, ...shared.filter((x) => !has(x.id))]
    .map((field, i) => ({ field, i }))
    .sort((a, b) => (a.field.sort ?? 0) - (b.field.sort ?? 0) || a.i - b.i)
    .map((x) => x.field);
}

export function defaultOf(field: Field): unknown {
  if (field.default !== undefined) return clone(field.default);
  if (field.type === "list") return [];
  if (field.type === "object") return valuesOf(field.element ?? []);
  return undefined;
}

export function valuesOf(fields: Field[]): Record<string, unknown> {
  const values: Record<string, unknown> = {};
  for (const field of fields) if (field.id) values[field.id] = defaultOf(field);
  return values;
}

export function newId(type: string): string {
  const chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
  const suffix = [...crypto.getRandomValues(new Uint32Array(4))].map((x) => chars[x % chars.length]).join("");
  return type.replace(/[^a-zA-Z0-9]/g, "") + suffix;
}

export function newItem(type: string, kind: "section" | "block", schemas: Schemas): Item {
  const schema = (kind === "section" ? schemas.sections : schemas.blocks)[type];
  const item: Item = { ...valuesOf(fieldsOf(schema, kind, schemas)), ...clone(schema?.default ?? {}), type, id: newId(type) };
  if (schema?.blocks) item.blocks = Array.isArray(item.blocks) ? item.blocks.map(copyItem) : [];
  return item;
}

export function copyItem(item: Item): Item {
  const copy = clone(item);
  copy.id = newId(item.type);
  copy.blocks = copy.blocks?.map(copyItem);
  if (!item.blocks) delete copy.blocks;
  return copy;
}

export function withValueAt<T>(value: T, path: (string | number)[], next: unknown): T | undefined {
  const [key, ...rest] = path;
  if (key === undefined) return next as T;
  if (!value || typeof value !== "object" || !Object.prototype.hasOwnProperty.call(value, key)) return undefined;
  const child = withValueAt((value as Record<string | number, unknown>)[key], rest, next);
  if (child === undefined) return undefined;
  if (Array.isArray(value)) return value.map((x, i) => (i === key ? child : x)) as T;
  return { ...value, [key]: child };
}

export const valueAt = (value: unknown, path: (string | number)[]): unknown =>
  path.reduce<unknown>((current, key) => (current && typeof current === "object" ? (current as Record<string | number, unknown>)[key] : undefined), value);

/** The preview edits texts it shows; it never adds or retypes a setting. */
export const withTextAt = <T>(value: T, path: (string | number)[], text: string): T | undefined =>
  typeof valueAt(value, path) === "string" ? withValueAt(value, path, text) : undefined;

export function fieldAt(fields: Field[], [key, ...rest]: (string | number)[]): Field | undefined {
  const field = typeof key === "string" ? fields.find((x) => x.id === key) : undefined;
  if (!field || !rest.length) return field;
  if (field.type === "list") return typeof rest[0] === "number" && rest.length > 1 ? fieldAt(field.element ?? [], rest.slice(1)) : undefined;
  return field.type === "object" ? fieldAt(field.element ?? [], rest) : undefined;
}

/** A link (`"format": "url"`) is set in the settings panel only, never by an edit from the preview. */
export function isTextEdit(fields: Field[], path: (string | number)[], value: unknown): boolean {
  const field = fieldAt(fields, path);
  return typeof value === "string" && field?.type === "string" && field.format !== "url";
}

export function withTextEdit<T>(item: T, fields: Field[], path: (string | number)[], text: string, at?: Translating): T | undefined {
  if (!isTextEdit(fields, path, text)) return undefined;
  const current = valueAt(item, path);
  if (at && (fieldAt(fields, path)?.translatable || isTranslations(current)) && (typeof current === "string" || isTranslations(current))) {
    return withValueAt(item, path, withTranslation(current, at, text));
  }
  return withTextAt(item, path, text);
}

export function pick(source: unknown, path: string): unknown {
  return path.split(".").reduce<unknown>((value, key) => (value == null ? undefined : (value as Record<string, unknown>)[key]), source);
}

export const humanize = (type: string) => type.replace(/[-_]+/g, " ").trim().replace(/^\w/, (c) => c.toUpperCase());

export function itemTitle(item: Item, schema: ItemSchema | undefined): string {
  const field = schema?.displayField;
  const value = field && field !== "default" ? displayText(pick(item, field)) : undefined;
  return value?.trim() || schema?.name || humanize(item.type);
}

const reportedExpressions = new Set<string>();

/** An expression that is not a supported form, or fails, hides the field. */
export function isVisible(field: Field, context: Record<string, unknown>): boolean {
  if (field.hidden) return false;
  if (!field.visibility) return true;
  try {
    return !!evaluate(field.visibility, context);
  } catch (error) {
    // Cached per expression because the fields are filtered again on every change.
    if (!reportedExpressions.has(field.visibility)) {
      reportedExpressions.add(field.visibility);
      const reason = error instanceof UnsupportedExpressionError ? "it is not one of the supported forms" : "it failed";
      console.warn(`Expression not executed: ${reason}`, field.visibility, error instanceof Error ? error.message : error);
    }
    return false;
  }
}

export function fill<T>(template: T, context: Record<string, unknown>): T {
  if (typeof template === "string") {
    return template.replace(/\{\{\s*([\w.]+)\s*\}\}/g, (_, path: string) => String(pick(context, path) ?? "")) as T;
  }
  if (Array.isArray(template)) return template.map((x) => fill(x, context)) as T;
  if (template && typeof template === "object") {
    return Object.fromEntries(Object.entries(template).map(([key, value]) => [key, fill(value, context)])) as T;
  }
  return template;
}

type SchemaRequest = Pick<RemoteRequest, "url" | "method" | "body">;

// A comment may hold a lone quote, so comments are consumed together with the strings.
const GRAPHQL_STRINGS_AND_COMMENTS = /"""(?:\\"""|[\s\S])*?"""|"(?:\\.|[^"\\\r\n])*"|#[^\r\n]*/g;

function isGraphqlRead(body: unknown): boolean {
  const query = (body as { query?: unknown } | null | undefined)?.query;
  return typeof query === "string" && !/\b(mutation|subscription)\b/.test(query.replace(GRAPHQL_STRINGS_AND_COMMENTS, " "));
}

function isAllowedOnPlatform(target: URL, request: SchemaRequest): boolean {
  const method = (request.method ?? "GET").toUpperCase();
  if (method === "GET") return target.pathname === "/api/pagebuilder/search";
  // GraphQL.NET's server can read the query from the URL, where it overrides the body.
  return method === "POST" && target.pathname === "/graphql" && !target.search && isGraphqlRead(request.body);
}

/** `platform` means the designer's own origin, the only place the user's token may go. */
export function schemaRequest(request: SchemaRequest, location: Pick<Location, "href" | "origin">): { url: string; platform: boolean } | undefined {
  try {
    const target = new URL(request.url, location.href);
    if (target.protocol !== "http:" && target.protocol !== "https:") return undefined;
    const platform = target.origin === location.origin;
    if (platform && !isAllowedOnPlatform(target, request)) {
      console.warn("[page-builder] Refused a schema's request to the platform", request.method ?? "GET", target.pathname);
      return undefined;
    }
    return { url: target.href, platform };
  } catch {
    return undefined;
  }
}

const ICONS: Record<string, string> = {
  article: "lucide-file-text",
  category: "lucide-layout-grid",
  colors: "lucide-palette",
  construction: "lucide-settings-2",
  email: "lucide-mail",
  favorite: "lucide-heart",
  general: "lucide-sliders-horizontal",
  image: "lucide-image",
  inventory_2: "lucide-package",
  local_mall: "lucide-shopping-bag",
  smartphone: "lucide-smartphone",
  text_fields: "lucide-type",
  text_snippet: "lucide-text",
};
export const iconOf = (schema?: ItemSchema) =>
  (schema?.icon && (ICONS[schema.icon] ?? (schema.icon.startsWith("lucide-") ? schema.icon : undefined))) || "lucide-square-dashed";
