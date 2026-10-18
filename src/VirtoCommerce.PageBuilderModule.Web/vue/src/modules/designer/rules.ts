import { PAGE_TYPES, pageTypeOf, type PageType } from "./templates";

// The store's template rules, `{theme}/config/template_rules.json`: per page type an ordered list, the first enabled
// rule whose conditions all hold picks the template, else the type's default. Only the storefront evaluates them;
// the designer edits the file. Whatever it doesn't know (top-level keys, rule keys, conditions) is kept as it is.

export interface TemplateRule {
  template: string;
  when?: Record<string, unknown>;
  disabled?: boolean;
  [key: string]: unknown;
}

export interface TemplateRules {
  version?: number;
  product?: TemplateRule[];
  category?: TemplateRule[];
  page?: TemplateRule[];
  [key: string]: unknown;
}

/** The conditions the designer edits per page type; any other is a custom condition, shown as its JSON */
export const CONDITIONS: Record<PageType, string[]> = {
  product: ["inCategory", "productType", "property"],
  category: ["inCategory", "property"],
  page: ["path", "language", "source", "setting"],
};
export const SOURCES = ["designer", "file", "blog"];

/** Without a rules file the storefront picks the B2C product template by its product property: so does a new file */
export const DEFAULT_RULES: TemplateRules = {
  version: 1,
  product: [{ template: "product.b2c", when: { property: { name: "VirtoFrontend_UI_Layout", equals: "B2C" } } }],
};

/** A problem at a JSON path ("product[2].when.inCategory"); `code` names the message, `params` fill it */
export interface RuleError {
  path: string;
  code: string;
  params?: Record<string, string>;
}

const isObject = (value: unknown): value is Record<string, unknown> => !!value && typeof value === "object" && !Array.isArray(value);
const isText = (value: unknown): value is string => typeof value === "string" && !!value.trim();

/** A `{ name, equals }` condition (property, setting) */
const namedValue = (value: unknown) => isObject(value) && isText(value.name) && "equals" in value;

const CHECKS: Record<string, (value: unknown) => string | undefined> = {
  inCategory: (value) => (Array.isArray(value) && value.length && value.every(isText) ? undefined : "CATEGORY_IDS"),
  productType: (value) => (isText(value) ? undefined : "TEXT"),
  property: (value) => (namedValue(value) ? undefined : "NAME_EQUALS"),
  path: (value) => (isText(value) ? undefined : "TEXT"),
  language: (value) => (isText(value) ? undefined : "TEXT"),
  source: (value) => (typeof value === "string" && SOURCES.includes(value) ? undefined : "SOURCE"),
  setting: (value) => (namedValue(value) ? undefined : "NAME_EQUALS"),
};

/** What is wrong with a rules file, each at its path; a condition the designer doesn't know is never wrong */
export function validateRules(value: unknown): RuleError[] {
  if (!isObject(value)) return [{ path: "", code: "NOT_OBJECT" }];
  const errors: RuleError[] = [];
  if (value.version !== undefined && typeof value.version !== "number") errors.push({ path: "version", code: "NUMBER" });
  for (const type of PAGE_TYPES) {
    const list = value[type];
    if (list === undefined) continue;
    if (!Array.isArray(list)) {
      errors.push({ path: type, code: "NOT_LIST" });
      continue;
    }
    list.forEach((rule: unknown, i) => {
      const at = `${type}[${i}]`;
      if (!isObject(rule)) return errors.push({ path: at, code: "NOT_OBJECT" });
      if (typeof rule.template !== "string" || pageTypeOf(rule.template.trim()) !== type) errors.push({ path: `${at}.template`, code: "TEMPLATE", params: { type } });
      if (rule.disabled !== undefined && typeof rule.disabled !== "boolean") errors.push({ path: `${at}.disabled`, code: "BOOLEAN" });
      if (rule.when === undefined) return;
      if (!isObject(rule.when)) return errors.push({ path: `${at}.when`, code: "NOT_OBJECT" });
      for (const [name, condition] of Object.entries(rule.when)) {
        const code = CONDITIONS[type].includes(name) ? CHECKS[name](condition) : undefined;
        if (code) errors.push({ path: `${at}.when.${name}`, code });
      }
    });
  }
  return errors;
}

/** The rules in a file's text: none when it doesn't parse or validate (the errors say why) */
export function parseRules(text: string): { rules?: TemplateRules; errors: RuleError[] } {
  let value: unknown;
  try {
    value = JSON.parse(text);
  } catch (e) {
    return { errors: [{ path: "", code: "SYNTAX", params: { detail: e instanceof Error ? e.message : String(e) } }] };
  }
  const errors = validateRules(value);
  return errors.length ? { errors } : { rules: value as TemplateRules, errors };
}

/** The rules a theme's file holds, or the storefront's default when it has none */
export const rulesFromFile = (value: unknown): TemplateRules => JSON.parse(JSON.stringify(value === undefined ? DEFAULT_RULES : value)) as TemplateRules;

export const formatRules = (rules: TemplateRules) => JSON.stringify(rules, null, 2);

/** A page type's rules, in order */
export const rulesOf = (rules: TemplateRules, type: PageType): TemplateRule[] => (Array.isArray(rules[type]) ? rules[type]! : []);

/** The rules with a page type's list replaced (the rest of the file as it was) */
export const withRules = (rules: TemplateRules, type: PageType, list: TemplateRule[]): TemplateRules => ({ version: 1, ...rules, [type]: list });

/** A list with one item moved */
export function moveItem<T>(list: T[], from: number, to: number): T[] {
  const next = [...list];
  next.splice(to, 0, ...next.splice(from, 1));
  return next;
}

/** A rule's conditions: the ones the designer edits for its page type, and the custom ones (anything else) */
export function conditionsOf(rule: TemplateRule, type: PageType) {
  const entries = Object.entries(rule.when ?? {});
  return {
    known: entries.filter(([name]) => CONDITIONS[type].includes(name)),
    custom: entries.filter(([name]) => !CONDITIONS[type].includes(name)),
  };
}

/**
 * The conditions as the Add rule form leaves them: known ones left empty dropped (no categories, a blank text, a
 * property without a name), custom ones kept as they were.
 */
export function cleanWhen(when: Record<string, unknown>, type: PageType): Record<string, unknown> {
  const empty = (name: string, value: unknown) => {
    if (!CONDITIONS[type].includes(name)) return false;
    if (Array.isArray(value)) return !value.length;
    if (isObject(value)) return !isText(value.name);
    return value === undefined || value === null || (typeof value === "string" && !value.trim());
  };
  return Object.fromEntries(Object.entries(when).filter(([name, value]) => !empty(name, value)));
}

/** The indexes of the rules that pick a template ("Applies to") */
export const rulesFor = (rules: TemplateRules, key: string): number[] => {
  const type = pageTypeOf(key);
  return type ? rulesOf(rules, type).flatMap((rule, i) => (rule.template?.trim() === key ? [i] : [])) : [];
};
