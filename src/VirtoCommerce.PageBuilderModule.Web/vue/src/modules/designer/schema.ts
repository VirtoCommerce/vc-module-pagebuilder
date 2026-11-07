import { evaluate, UnsupportedExpressionError } from "./safeExpression";
import type { Field, Item, ItemSchema, Schemas } from "./types";
import { displayText, isTranslations, withTranslation, type Translating } from "./translatable";

/**
 * A section's or block's fields, as the Angular designer assembled them: its own settings, plus the shared fields
 * every section (shared._sections) or block (shared._blocks) gets, plus the shared groups it includes; minus what it
 * excludes; ordered by `sort` (stable).
 */
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

/** A deep copy of JSON data (also of reactive proxies, which structuredClone refuses) */
export const clone = <T>(value: T): T => (value === undefined ? value : (JSON.parse(JSON.stringify(value)) as T));

/** A field's starting value: its default, else an empty list, else an object of its elements' defaults. */
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

/** A new id as the Angular designer made them: the type's letters and digits plus 4 random ones. */
export function newId(type: string): string {
  const chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
  const suffix = [...crypto.getRandomValues(new Uint32Array(4))].map((x) => chars[x % chars.length]).join("");
  return type.replace(/[^a-zA-Z0-9]/g, "") + suffix;
}

/** A new section or block of a type: the schema's default model (its blocks with new ids), then each field's default. */
export function newItem(type: string, kind: "section" | "block", schemas: Schemas): Item {
  const schema = (kind === "section" ? schemas.sections : schemas.blocks)[type];
  const item: Item = { ...valuesOf(fieldsOf(schema, kind, schemas)), ...clone(schema?.default ?? {}), type, id: newId(type) };
  if (schema?.blocks) item.blocks = Array.isArray(item.blocks) ? item.blocks.map(copyItem) : [];
  return item;
}

/** A deep copy with new ids (its own and its blocks'), for Duplicate. */
export function copyItem(item: Item): Item {
  const copy = clone(item);
  copy.id = newId(item.type);
  copy.blocks = copy.blocks?.map(copyItem);
  if (!item.blocks) delete copy.blocks;
  return copy;
}

/**
 * A copy of the value with the string at the path replaced, or undefined when there is no string there: the preview
 * edits texts it shows, never adds or retypes a setting.
 */
export function withTextAt<T>(value: T, path: (string | number)[], text: string): T | undefined {
  const [key, ...rest] = path;
  if (key === undefined) return typeof value === "string" ? (text as T) : undefined;
  if (!value || typeof value !== "object" || !Object.prototype.hasOwnProperty.call(value, key)) return undefined;
  const child = withTextAt((value as Record<string | number, unknown>)[key], rest, text);
  if (child === undefined) return undefined;
  if (Array.isArray(value)) return value.map((x, i) => (i === key ? child : x)) as T;
  return { ...value, [key]: child };
}

/** A copy of the value with what is at the path replaced, or undefined when the path isn't there. */
export function withValueAt<T>(value: T, path: (string | number)[], next: unknown): T | undefined {
  const [key, ...rest] = path;
  if (key === undefined) return next as T;
  if (!value || typeof value !== "object" || !Object.prototype.hasOwnProperty.call(value, key)) return undefined;
  const child = withValueAt((value as Record<string | number, unknown>)[key], rest, next);
  if (child === undefined) return undefined;
  if (Array.isArray(value)) return value.map((x, i) => (i === key ? child : x)) as T;
  return { ...value, [key]: child };
}

/** What is at the path ("links", 0, "label") */
export const valueAt = (value: unknown, path: (string | number)[]): unknown =>
  path.reduce<unknown>((current, key) => (current && typeof current === "object" ? (current as Record<string | number, unknown>)[key] : undefined), value);

/** The field of the setting at the path among these fields: a list's row (an index) and an object hold `element` fields. */
export function fieldAt(fields: Field[], [key, ...rest]: (string | number)[]): Field | undefined {
  const field = typeof key === "string" ? fields.find((x) => x.id === key) : undefined;
  if (!field || !rest.length) return field;
  if (field.type === "list") return typeof rest[0] === "number" && rest.length > 1 ? fieldAt(field.element ?? [], rest.slice(1)) : undefined;
  return field.type === "object" ? fieldAt(field.element ?? [], rest) : undefined;
}

/** Whether the preview's inline edit may set the value at the path: a string, for a plain-text ("string") setting. */
export function isTextEdit(fields: Field[], path: (string | number)[], value: unknown): boolean {
  return typeof value === "string" && fieldAt(fields, path)?.type === "string";
}

/**
 * The item with a text the preview edited in place, or undefined when nothing changes: the schema declares no plain
 * text setting at the path, or the item holds no text there. With `at` (the item is edited per language and the
 * preview named the language), a translatable setting takes the text as that language's.
 */
export function withTextEdit<T>(item: T, fields: Field[], path: (string | number)[], text: string, at?: Translating): T | undefined {
  const field = fieldAt(fields, path);
  if (field?.type !== "string") return undefined;
  const current = valueAt(item, path);
  if (at && (field.translatable || isTranslations(current)) && (typeof current === "string" || isTranslations(current))) {
    return withValueAt(item, path, withTranslation(current, at, text));
  }
  return withTextAt(item, path, text);
}

/** The value at a dotted path ("product.name"). */
export function pick(source: unknown, path: string): unknown {
  return path.split(".").reduce<unknown>((value, key) => (value == null ? undefined : (value as Record<string, unknown>)[key]), source);
}

/** A type as a name until the site's schemas say better: "call-to-action" reads "Call to action" */
export const humanize = (type: string) => type.replace(/[-_]+/g, " ").trim().replace(/^\w/, (c) => c.toUpperCase());

/** The item's label in the lists: its displayField value (a "default" display field = the schema name), else the schema name. */
export function itemTitle(item: Item, schema: ItemSchema | undefined): string {
  const field = schema?.displayField;
  const value = field && field !== "default" ? displayText(pick(item, field)) : undefined;
  return value?.trim() || schema?.name || humanize(item.type);
}

const reportedExpressions = new Set<string>();

/**
 * Whether a field shows: its `visibility` expression with `this` = the context ({ model, item, element, … }). As in the
 * Angular designer, an expression that isn't a supported form, or fails, hides the field.
 */
export function isVisible(field: Field, context: Record<string, unknown>): boolean {
  if (field.hidden) return false;
  if (!field.visibility) return true;
  try {
    return !!evaluate(field.visibility, context);
  } catch (error) {
    // once per expression: the fields are filtered again on every change
    if (!reportedExpressions.has(field.visibility)) {
      reportedExpressions.add(field.visibility);
      const reason = error instanceof UnsupportedExpressionError ? "it is not one of the supported forms" : "it failed";
      console.warn(`Expression not executed: ${reason}`, field.visibility, error instanceof Error ? error.message : error);
    }
    return false;
  }
}

/** Replaces {{path}} in strings (deeply, for a request body) with values from the context. */
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

/**
 * Where a schema's request goes, resolved as fetch would from the designer's page, or undefined when not http(s).
 * `platform`: it goes to the platform (the designer's own origin), the only place the user's token may go.
 */
export function schemaRequest(url: string, location: Pick<Location, "href" | "origin">): { url: string; platform: boolean } | undefined {
  try {
    const target = new URL(url, location.href);
    if (target.protocol !== "http:" && target.protocol !== "https:") return undefined;
    return { url: target.href, platform: target.origin === location.origin };
  } catch {
    return undefined;
  }
}

/** Material icon names of the schemas → the shell's lucide icons */
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
