import { clone, isRecord, isText } from "./json";
import type { ConditionDescriptor } from "./previewMessages";
import { checkCondition, conditionsFor } from "./ruleConditions";
import { PAGE_TYPES, pageTypeOf, type PageType } from "./templates";

// The store's template rules, `{theme}/config/template_rules.json` (docs/contracts/theme-snapshot.md, Template rules):
// per page type an ordered list, the first enabled rule whose conditions all hold picks the template. Only the
// storefront evaluates them; the designer edits the file. Whatever it doesn't know (top-level keys, rule keys,
// conditions) is kept as it is.

export const RULES_VERSION = 2;

export interface TemplateRule {
  /** stable: kept when the rule changes or moves */
  id: string;
  template: string;
  when?: Record<string, unknown>;
  disabled?: boolean;
  [key: string]: unknown;
}

export interface TemplateRules {
  version: typeof RULES_VERSION;
  /** the condition names the rules use */
  conditions: string[];
  product?: TemplateRule[];
  category?: TemplateRule[];
  page?: TemplateRule[];
  [key: string]: unknown;
}

/** The rules of a page type the file has no list for: the B2C product template by its product property */
export const DEFAULT_RULES: Readonly<Record<PageType, readonly TemplateRule[]>> = {
  product: [{ id: "default-b2c", template: "product.b2c", when: { property: { name: "VirtoFrontend_UI_Layout", equals: "B2C" } } }],
  category: [],
  page: [],
};

/** A problem at a JSON path ("product[2].when.inCategory"); `code` names the message, `params` fill it */
export interface RuleError {
  path: string;
  code: string;
  params?: Record<string, string>;
}

/** A rule the storefront reads; it skips any other entry of a list */
const isRuleLike = (value: unknown): value is Record<string, unknown> & { template: string } =>
  isRecord(value) && typeof value.template === "string" && (value.when === undefined || isRecord(value.when));

/**
 * A page type's rules with their ids: a rule's own, else `{pageType}-{index}` counting the rules the storefront reads,
 * as the storefront and the server read a version 1 file; a taken id gets a suffix, as the server gives it.
 */
function withIds(list: unknown[], type: PageType): TemplateRule[] {
  const ids = new Set<string>();
  return list.filter(isRuleLike).map((rule, index) => {
    let id = typeof rule.id === "string" ? rule.id : "";
    if (!id || ids.has(id)) {
      id = `${type}-${index}`;
      for (let n = 2; ids.has(id); n++) id = `${type}-${index}-${n}`;
    }
    ids.add(id);
    return { ...rule, id };
  });
}

/** The condition names a file's rules use, sorted */
const conditionNames = (lists: Partial<Record<PageType, unknown>>) =>
  [...new Set(PAGE_TYPES.flatMap((type) => (Array.isArray(lists[type]) ? (lists[type] as TemplateRule[]) : []).flatMap((rule) => Object.keys(rule.when ?? {}))))].sort();

/** The file as the designer writes it: version 2, the conditions its rules use, the page types' lists, the rest */
function toFile(rules: Record<string, unknown>): TemplateRules {
  const { version: _version, conditions: _conditions, product, category, page, ...rest } = rules;
  const lists: Partial<Record<PageType, unknown>> = { product, category, page };
  return {
    version: RULES_VERSION,
    conditions: conditionNames(lists),
    ...Object.fromEntries(PAGE_TYPES.filter((type) => Array.isArray(lists[type])).map((type) => [type, lists[type]])),
    ...rest,
  };
}

/** The rules a file holds, version 1 or 2; no file has no lists, so every page type has its defaults */
export function readRules(value: unknown): TemplateRules {
  const file: Record<string, unknown> = isRecord(value) ? clone(value) : {};
  for (const type of PAGE_TYPES) {
    if (Array.isArray(file[type])) file[type] = withIds(file[type] as unknown[], type);
    // the storefront reads a list that isn't one as none
    else delete file[type];
  }
  return toFile(file);
}

/**
 * How many entries of a rules file the designer can't read, which a save of the rules leaves out: a page type's value
 * that isn't a list, an entry of a list that isn't a rule; a file that isn't an object counts as one
 */
export function unreadableRules(value: unknown): number {
  if (value === undefined) return 0;
  if (!isRecord(value)) return 1;
  return PAGE_TYPES.reduce((count, type) => {
    const list = value[type];
    if (list === undefined) return count;
    return count + (Array.isArray(list) ? list.filter((rule) => !isRuleLike(rule)).length : 1);
  }, 0);
}

/** What is wrong with a rules file, each at its path; a condition the storefront didn't announce is never wrong */
export function validateRules(value: unknown, descriptors: ConditionDescriptor[] = []): RuleError[] {
  if (!isRecord(value)) return [{ path: "", code: "NOT_OBJECT" }];
  const errors: RuleError[] = [];
  if (value.version !== undefined && value.version !== 1 && value.version !== RULES_VERSION) errors.push({ path: "version", code: "VERSION" });
  if (value.conditions !== undefined && !(Array.isArray(value.conditions) && value.conditions.every((x) => typeof x === "string")))
    errors.push({ path: "conditions", code: "TEXT_LIST" });
  for (const type of PAGE_TYPES) {
    const list = value[type];
    // an id is unique within its page type's list
    const ids = new Set<string>();
    if (list === undefined) continue;
    if (!Array.isArray(list)) {
      errors.push({ path: type, code: "NOT_LIST" });
      continue;
    }
    const offered = new Map(conditionsFor(descriptors, type).map((x) => [x.name, x]));
    list.forEach((rule: unknown, i) => {
      const at = `${type}[${i}]`;
      if (!isRecord(rule)) return errors.push({ path: at, code: "NOT_OBJECT" });
      if (rule.id !== undefined) {
        if (!isText(rule.id)) errors.push({ path: `${at}.id`, code: "ID" });
        else if (ids.has(rule.id)) errors.push({ path: `${at}.id`, code: "DUPLICATE_ID", params: { id: rule.id } });
        else ids.add(rule.id);
      }
      if (typeof rule.template !== "string" || pageTypeOf(rule.template.trim()) !== type) errors.push({ path: `${at}.template`, code: "TEMPLATE", params: { type } });
      if (rule.disabled !== undefined && typeof rule.disabled !== "boolean") errors.push({ path: `${at}.disabled`, code: "BOOLEAN" });
      if (rule.when === undefined) return;
      if (!isRecord(rule.when)) return errors.push({ path: `${at}.when`, code: "NOT_OBJECT" });
      for (const [name, condition] of Object.entries(rule.when)) {
        const descriptor = offered.get(name);
        const problem = descriptor && checkCondition(descriptor, condition);
        if (problem) errors.push({ path: `${at}.when.${name}`, ...problem });
      }
    });
  }
  return errors;
}

/** The rules in a file's text: none when it doesn't parse or validate (the errors say why) */
export function parseRules(text: string, descriptors: ConditionDescriptor[] = []): { rules?: TemplateRules; errors: RuleError[] } {
  let value: unknown;
  try {
    value = JSON.parse(text);
  } catch (e) {
    return { errors: [{ path: "", code: "SYNTAX", params: { detail: e instanceof Error ? e.message : String(e) } }] };
  }
  const errors = validateRules(value, descriptors);
  return errors.length ? { errors } : { rules: readRules(value), errors };
}

export const formatRules = (rules: TemplateRules) => JSON.stringify(rules, null, 2);

/** Whether the file has a list of its own for the page type; without one, the page type has its defaults */
export const hasOwnRules = (rules: TemplateRules, type: PageType) => Array.isArray(rules[type]);

/** A page type's rules, in order: its own list, else (a copy of) its defaults */
export const rulesOf = (rules: TemplateRules, type: PageType): TemplateRule[] => (hasOwnRules(rules, type) ? rules[type]! : clone([...DEFAULT_RULES[type]]));

/** The rules with a page type's list replaced (the rest of the file as it was) */
export const withRules = (rules: TemplateRules, type: PageType, list: TemplateRule[]): TemplateRules => toFile({ ...rules, [type]: list });

const ID_CHARS = "abcdefghijklmnopqrstuvwxyz0123456789";

/** A new rule's id: short, url-safe, unique within the file (the defaults' ids included) */
export function newRuleId(rules: TemplateRules): string {
  const taken = new Set(PAGE_TYPES.flatMap((type) => rulesOf(rules, type).map((rule) => rule.id)));
  for (;;) {
    const id = [...crypto.getRandomValues(new Uint32Array(8))].map((x) => ID_CHARS[x % ID_CHARS.length]).join("");
    if (!taken.has(id)) return id;
  }
}

/** A list with one item moved */
export function moveItem<T>(list: T[], from: number, to: number): T[] {
  const next = [...list];
  next.splice(to, 0, ...next.splice(from, 1));
  return next;
}

/** The indexes of the rules that pick a template ("Applies to") */
export const rulesFor = (rules: TemplateRules, key: string): number[] => {
  const type = pageTypeOf(key);
  return type ? rulesOf(rules, type).flatMap((rule, i) => (rule.template?.trim() === key ? [i] : [])) : [];
};
