import type { Field } from "./types";
import { isRecord, isText, isTextList } from "./json";
import type { ConditionDescriptor } from "./previewMessages";
import type { PageType } from "./templates";

export type ConditionInput = { kind: "categories" } | { kind: "texts" } | { kind: "field"; field: Field };

export interface ConditionProblem {
  code: string;
  params?: Record<string, string>;
}

export const CATEGORY_CONDITION = "inCategory";

export const BUILT_IN_CONDITIONS: Readonly<Record<string, { label: string; hint?: string; is: string }>> = {
  [CATEGORY_CONDITION]: { label: "IN_CATEGORIES", is: "IS.IN_CATEGORY" },
  productType: { label: "PRODUCT_TYPE", is: "IS.PRODUCT_TYPE" },
  property: { label: "PROPERTY", is: "IS.PROPERTY" },
  path: { label: "PATH", hint: "PATH_HINT", is: "IS.PATH" },
  language: { label: "LANGUAGE", hint: "LANGUAGE_HINT", is: "IS.LANGUAGE" },
  source: { label: "SOURCE", is: "IS.SOURCE" },
  setting: { label: "SETTING", is: "IS.SETTING" },
};

export function optionLabel(option: { value: unknown; label: string }, i18n: { t: (key: string) => string; te: (key: string) => boolean }): string {
  const key = `PAGE_BUILDER.DESIGNER.RULES.SOURCES.${String(option.value).toUpperCase()}`;
  return i18n.te(key) ? i18n.t(key) : option.label;
}

const isFilledTextList = (value: unknown): value is string[] => isTextList(value) && value.length > 0 && value.every(isText);

export function categoryIds(value: unknown): string[] {
  if (Array.isArray(value)) return value.filter((x): x is string => typeof x === "string");
  return typeof value === "string" ? value.split(",").map((x) => x.trim()).filter(Boolean) : [];
}

export const conditionsFor = (descriptors: ConditionDescriptor[], type: PageType) => descriptors.filter((x) => x.pageTypes.includes(type));

const subFields = (field: Field): Field[] => (Array.isArray(field.element) ? field.element : []);

const isTextListField = (field: Field) => (field.type === "list" && !subFields(field).length) || (field.type === "string" && !!field.multiple);

export function conditionInput(descriptor: ConditionDescriptor): ConditionInput {
  // The contract stores category ids as a list, whatever field the storefront describes them with.
  if (descriptor.name === CATEGORY_CONDITION) return { kind: "categories" };
  if (isTextListField(descriptor.value)) return { kind: "texts" };
  const element = subFields(descriptor.value);
  return { kind: "field", field: { ...descriptor.value, id: descriptor.name, ...(element.length ? { element } : {}) } };
}

export function isEmptyValue(value: unknown): boolean {
  if (value === undefined || value === null) return true;
  if (typeof value === "string") return !value.trim();
  if (Array.isArray(value)) return !value.length;
  if (isRecord(value)) return Object.values(value).every(isEmptyValue);
  return false;
}

export function checkCondition(descriptor: ConditionDescriptor, value: unknown): ConditionProblem | undefined {
  const input = conditionInput(descriptor);
  if (input.kind === "categories") return isFilledTextList(typeof value === "string" ? categoryIds(value) : value) ? undefined : { code: "CATEGORY_IDS" };
  if (input.kind === "texts") return isFilledTextList(value) ? undefined : { code: "TEXT_LIST" };
  return checkField(input.field, value);
}

function checkField(field: Field, value: unknown): ConditionProblem | undefined {
  switch (field.type) {
    case "select": {
      const options = (field.options ?? []).map((x) => x.value);
      return options.includes(value) ? undefined : { code: "OPTION", params: { options: options.map(String).join(", ") } };
    }
    case "number":
    case "slider":
      return typeof value === "number" && Number.isFinite(value) ? undefined : { code: "NUMBER" };
    case "checkbox":
      return typeof value === "boolean" ? undefined : { code: "BOOLEAN" };
    case "object": {
      if (!isRecord(value)) return { code: "NOT_OBJECT" };
      const ids = subFields(field).flatMap((x) => (x.id ? [x.id] : []));
      const missing = ids.some((id) => isEmptyValue(value[id]) || typeof value[id] === "object");
      return missing ? { code: "FIELDS", params: { fields: ids.join(", ") } } : undefined;
    }
    default:
      return isText(value) ? undefined : { code: "TEXT" };
  }
}

export function splitConditions(when: Record<string, unknown> | undefined, descriptors: ConditionDescriptor[], type: PageType) {
  const offered = new Map(conditionsFor(descriptors, type).map((x) => [x.name, x]));
  const known: { name: string; value: unknown; descriptor: ConditionDescriptor }[] = [];
  const unknown: { name: string; value: unknown }[] = [];
  for (const [name, value] of Object.entries(when ?? {})) {
    const descriptor = offered.get(name);
    if (descriptor) known.push({ name, value, descriptor });
    else unknown.push({ name, value });
  }
  return { known, unknown };
}

export function cleanWhen(when: Record<string, unknown>, descriptors: ConditionDescriptor[], type: PageType): Record<string, unknown> {
  const offered = new Set(conditionsFor(descriptors, type).map((x) => x.name));
  return Object.fromEntries(Object.entries(when).filter(([name, value]) => !offered.has(name) || !isEmptyValue(value)));
}

export const splitTexts = (text: string): string[] =>
  text
    .split(",")
    .map((x) => x.trim())
    .filter(Boolean);
