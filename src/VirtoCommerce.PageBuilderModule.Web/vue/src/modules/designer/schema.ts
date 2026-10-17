import type { Field, Item, ItemSchema, Schemas } from "./types";

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

/** A new section or block of a type: the schema's default model, then each field's default. */
export function newItem(type: string, kind: "section" | "block", schemas: Schemas): Item {
  const schema = (kind === "section" ? schemas.sections : schemas.blocks)[type];
  const item: Item = { ...valuesOf(fieldsOf(schema, kind, schemas)), ...clone(schema?.default ?? {}), type, id: newId(type) };
  if (schema?.blocks) item.blocks = [];
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

/** The value at a dotted path ("product.name"). */
export function pick(source: unknown, path: string): unknown {
  return path.split(".").reduce<unknown>((value, key) => (value == null ? undefined : (value as Record<string, unknown>)[key]), source);
}

/** A type as a name until the site's schemas say better: "call-to-action" reads "Call to action" */
export const humanize = (type: string) => type.replace(/[-_]+/g, " ").trim().replace(/^\w/, (c) => c.toUpperCase());

/** The item's label in the lists: its displayField value (a "default" display field = the schema name), else the schema name. */
export function itemTitle(item: Item, schema: ItemSchema | undefined): string {
  const field = schema?.displayField;
  const value = field && field !== "default" ? pick(item, field) : undefined;
  return (typeof value === "string" && value.trim()) || schema?.name || humanize(item.type);
}

/** Whether a field shows: its `visibility` expression run with `this` = the context ({ model, item, element, … }). */
export function isVisible(field: Field, context: Record<string, unknown>): boolean {
  if (field.hidden) return false;
  if (!field.visibility) return true;
  try {
    // the schemas come from the store's own theme, as in the Angular designer
    return !!new Function(`return (${field.visibility});`).call(context);
  } catch {
    return true;
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
