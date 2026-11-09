import assert from "node:assert/strict";
import test from "node:test";
import type { ConditionDescriptor } from "../src/modules/designer/previewMessages";
import {
  DEFAULT_RULES,
  formatRules,
  hasOwnRules,
  moveItem,
  newRuleId,
  parseRules,
  readRules,
  rulesFor,
  rulesOf,
  unreadableRules,
  validateRules,
  withRules,
  type TemplateRules,
} from "../src/modules/designer/rules";
import {
  BUILT_IN_CONDITIONS,
  categoryIds,
  checkCondition,
  cleanWhen,
  conditionInput,
  conditionsFor,
  isEmptyValue,
  optionLabel,
  splitConditions,
  splitTexts,
} from "../src/modules/designer/ruleConditions";

/** The storefront's built-in conditions as its handshake announces them (vc-frontend templates/conditions.ts) */
const nameEquals = { type: "object", element: [{ id: "name", type: "string", label: "Name" }, { id: "equals", type: "string", label: "Value" }] };
const DESCRIPTORS: ConditionDescriptor[] = [
  { name: "inCategory", pageTypes: ["product", "category"], label: "In category", value: { type: "list", label: "Category ids" } },
  { name: "productType", pageTypes: ["product"], label: "Product type", value: { type: "string" } },
  { name: "property", pageTypes: ["product"], label: "Property", value: nameEquals },
  { name: "path", pageTypes: ["page"], label: "Path", value: { type: "string", label: "Path, * matches any characters" } },
  { name: "language", pageTypes: ["page"], label: "Language", value: { type: "string" } },
  {
    name: "source",
    pageTypes: ["page"],
    label: "Source",
    value: { type: "select", options: [{ label: "Page", value: "Pages" }, { label: "File page", value: "ContentFile" }] },
  },
  { name: "setting", pageTypes: ["page"], label: "Page setting", value: nameEquals },
];

const v1 = {
  version: 1,
  product: [
    { template: "product.printer", when: { inCategory: ["printers-id"] } },
    { template: "product.bundle", when: { property: { name: "Bundle", equals: true } }, disabled: true },
  ],
  category: [{ template: "category.brands", when: { inCategory: ["brands-id"] } }],
  page: [{ template: "page.landing", when: { path: "/landing/*", source: "Pages" } }],
};

// ── read, convert, write ─────────────────────────────────────────────────────

test("a version 1 file reads as version 2: the ids the storefront gives it, and the conditions its rules use", () => {
  const rules = readRules(v1);
  assert.equal(rules.version, 2);
  assert.deepEqual(rules.conditions, ["inCategory", "path", "property", "source"]);
  assert.deepEqual(rulesOf(rules, "product").map((x) => x.id), ["product-0", "product-1"]);
  assert.deepEqual(rulesOf(rules, "category").map((x) => x.id), ["category-0"]);
  assert.deepEqual(rulesOf(rules, "page").map((x) => x.id), ["page-0"]);
  // the rest of each rule as it was
  assert.deepEqual(rulesOf(rules, "product")[1], { ...v1.product[1], id: "product-1" });
});

test("ids count the rules the storefront reads; a rule's own id is kept, a taken one is given another as the server does", () => {
  const rules = readRules({
    product: [
      "rule",
      { template: "product.a" },
      { template: 5 },
      { id: "mine", template: "product.b", when: [] },
      { id: "mine", template: "product.c" },
      { id: "", template: "product.d" },
      { id: "product-0", template: "product.e" },
    ],
  });
  // "rule", the number template and the list `when` are not rules to the storefront: they are not counted, nor kept
  assert.deepEqual(
    rulesOf(rules, "product").map((x) => `${x.template}:${x.id}`),
    ["product.a:product-0", "product.c:mine", "product.d:product-2", "product.e:product-3"],
  );
});

test("a version 2 file keeps its ids; the conditions list is written from the rules, whatever the file said", () => {
  const rules = readRules({
    version: 2,
    conditions: ["stale"],
    owner: "marketing",
    page: [{ id: "landing", template: "page.landing", when: { path: "/landing/*" } }],
  });
  assert.deepEqual(rules, { version: 2, conditions: ["path"], page: [{ id: "landing", template: "page.landing", when: { path: "/landing/*" } }], owner: "marketing" });
  // written as it reads: no change, nothing to save
  assert.equal(formatRules(readRules(JSON.parse(formatRules(rules)))), formatRules(rules));
});

test("no file: no lists, so every page type has its defaults, which are not written", () => {
  for (const missing of [undefined, null, [], "x"]) {
    const rules = readRules(missing);
    assert.deepEqual(rules, { version: 2, conditions: [] });
    assert.equal(hasOwnRules(rules, "product"), false);
    assert.deepEqual(rulesOf(rules, "product"), DEFAULT_RULES.product);
    assert.deepEqual(rulesOf(rules, "category"), []);
  }
  // a page type whose list isn't one has none, as on the storefront
  assert.equal(hasOwnRules(readRules({ product: { template: "product.x" } }), "product"), false);
});

test("the defaults: product picks the B2C template by its layout property, as the contract says; a copy each time", () => {
  assert.deepEqual(DEFAULT_RULES.product, [
    { id: "default-b2c", template: "product.b2c", when: { property: { name: "VirtoFrontend_UI_Layout", equals: "B2C" } } },
  ]);
  const rules = readRules(undefined);
  rulesOf(rules, "product")[0].template = "product.other";
  assert.equal(DEFAULT_RULES.product[0].template, "product.b2c");
});

test("a page type's defaults are written only when its list changes; an empty list means no rules", () => {
  let rules = readRules({ page: [] });
  assert.deepEqual(rulesOf(rules, "page"), []);
  assert.equal(hasOwnRules(rules, "page"), true);

  const product = rulesOf(rules, "product").map((rule) => ({ ...rule, disabled: true }));
  rules = withRules(rules, "product", product);
  assert.deepEqual(rules.product, [{ ...DEFAULT_RULES.product[0], disabled: true }]);
  assert.deepEqual(rules.conditions, ["property"]);
  assert.equal(hasOwnRules(rules, "category"), false);

  rules = withRules(rules, "product", []);
  assert.deepEqual(rulesOf(rules, "product"), []);
  assert.deepEqual(rules.conditions, []);
});

// ── ids stay with their rules ────────────────────────────────────────────────

test("a rule keeps its id when it changes, moves or is switched on, and when others are removed", () => {
  let rules = readRules({ ...v1, owner: "marketing" });
  const ids = rulesOf(rules, "product").map((x) => x.id);

  // edited as the form saves it: the rule's other keys, its id among them, with the new template and conditions
  const { when: _when, ...rest } = rulesOf(rules, "product")[0];
  rules = withRules(rules, "product", [{ ...rest, template: "product.scanner", when: { productType: "Physical" } }, rulesOf(rules, "product")[1]]);
  assert.deepEqual(rulesOf(rules, "product").map((x) => x.id), ids);

  rules = withRules(rules, "product", moveItem(rulesOf(rules, "product"), 1, 0));
  assert.deepEqual(rulesOf(rules, "product").map((x) => x.id), [ids[1], ids[0]]);

  const { disabled: _off, ...on } = rulesOf(rules, "product")[0];
  rules = withRules(rules, "product", [on, rulesOf(rules, "product")[1]]);
  assert.deepEqual(rulesOf(rules, "product").map((x) => x.id), [ids[1], ids[0]]);

  rules = withRules(rules, "product", rulesOf(rules, "product").filter((_, i) => i !== 0));
  assert.deepEqual(rulesOf(rules, "product").map((x) => x.id), [ids[0]]);

  const saved = JSON.parse(formatRules(rules)) as TemplateRules;
  assert.equal(saved.version, 2);
  assert.equal(saved.owner, "marketing");
  assert.deepEqual(saved.conditions, ["inCategory", "path", "productType", "source"]);
  assert.deepEqual(saved.product, [{ id: ids[0], template: "product.scanner", when: { productType: "Physical" } }]);
  // read again, the ids are the ones written
  assert.deepEqual(rulesOf(readRules(saved), "product").map((x) => x.id), [ids[0]]);
});

test("a new rule's id is short, url-safe and taken by no rule of the file, the defaults included", () => {
  const rules = readRules(v1);
  const taken = new Set(["product-0", "product-1", "category-0", "page-0", "default-b2c"]);
  const made = new Set<string>();
  for (let i = 0; i < 200; i++) {
    const id = newRuleId(rules);
    assert.match(id, /^[a-z0-9]{8}$/);
    assert.ok(!taken.has(id));
    made.add(id);
  }
  assert.ok(made.size > 190);
});

test("a new rule's id drawn again when a rule has it", (t) => {
  let draw = 0;
  // the first draw is "aaaaaaaa", the next "bbbbbbbb"
  t.mock.method(globalThis.crypto, "getRandomValues", (array: Uint32Array) => array.fill(draw++));
  assert.equal(newRuleId(readRules({ page: [{ id: "aaaaaaaa", template: "page" }] })), "bbbbbbbb");
});

// ── validation ───────────────────────────────────────────────────────────────

test("a rules file parses into version 2", () => {
  const { rules, errors } = parseRules(JSON.stringify(v1), DESCRIPTORS);
  assert.deepEqual(errors, []);
  assert.deepEqual(rules, readRules(v1));
});

test("text that isn't JSON says so", () => {
  const { rules, errors } = parseRules("{ product: [ }");
  assert.equal(rules, undefined);
  assert.equal(errors.length, 1);
  assert.equal(errors[0].code, "SYNTAX");
  assert.ok(errors[0].params?.detail);
});

test("errors point at their JSON path", () => {
  const errors = validateRules(
    {
      version: 3,
      conditions: "inCategory",
      product: [
        { id: "a", template: "product.printer", when: { inCategory: [] } },
        { id: "a", template: "category.brands" },
        { id: " ", template: "product.x", when: { property: { name: "Brand" } }, disabled: "yes" },
        "rule",
        { template: "product.y", when: { inCategory: "c1,c2", productType: 5 } },
      ],
      category: { template: "category.x" },
      page: [{ template: "page.landing", when: { source: "designer", path: "", setting: { name: "x", equals: { deep: 1 } } } }],
    },
    DESCRIPTORS,
  );
  assert.deepEqual(
    errors.map((x) => `${x.path}: ${x.code}${x.params ? ` ${JSON.stringify(x.params)}` : ""}`),
    [
      "version: VERSION",
      "conditions: TEXT_LIST",
      "product[0].when.inCategory: CATEGORY_IDS",
      'product[1].id: DUPLICATE_ID {"id":"a"}',
      'product[1].template: TEMPLATE {"type":"product"}',
      "product[2].id: ID",
      "product[2].disabled: BOOLEAN",
      'product[2].when.property: FIELDS {"fields":"name, equals"}',
      "product[3]: NOT_OBJECT",
      // "c1,c2" is the list the contract reads it as; only the product type is wrong there
      "product[4].when.productType: TEXT",
      "category: NOT_LIST",
      'page[0].when.source: OPTION {"options":"Pages, ContentFile"}',
      "page[0].when.path: TEXT",
      'page[0].when.setting: FIELDS {"fields":"name, equals"}',
    ],
  );
  assert.deepEqual(validateRules([]), [{ path: "", code: "NOT_OBJECT" }]);
});

test("an inCategory text is read as its comma-separated ids, as the contract and the storefront read it", () => {
  assert.deepEqual(categoryIds("c1, c2,,c3 "), ["c1", "c2", "c3"]);
  assert.deepEqual(categoryIds(["c1", 5, "c2"]), ["c1", "c2"]);
  assert.deepEqual(categoryIds(undefined), []);
  assert.deepEqual(validateRules({ product: [{ template: "product.x", when: { inCategory: "c1,c2" } }] }, DESCRIPTORS), []);
  assert.equal(validateRules({ product: [{ template: "product.x", when: { inCategory: " , " } }] }, DESCRIPTORS)[0]?.code, "CATEGORY_IDS");
});

test("ids are unique within a page type's list; two lists may use the same one", () => {
  assert.deepEqual(validateRules({ product: [{ id: "x", template: "product" }], page: [{ id: "x", template: "page" }] }), []);
  const errors = validateRules({ product: [{ id: "x", template: "product" }, { id: "x", template: "product" }] });
  assert.deepEqual(errors, [{ path: "product[1].id", code: "DUPLICATE_ID", params: { id: "x" } }]);
});

test("a condition the storefront didn't announce for the page type is never an error", () => {
  const rule = { template: "product.printer", when: { inCategory: ["printers-id"], vendor: { id: "acme" }, path: 5 } };
  assert.deepEqual(validateRules({ product: [rule] }, DESCRIPTORS), []);
  // property is a product condition: on a category rule the storefront doesn't know it
  assert.deepEqual(validateRules({ category: [{ template: "category.x", when: { property: 1 } }] }, DESCRIPTORS), []);
  // before the handshake no condition is checked
  assert.deepEqual(validateRules({ product: [{ template: "product.x", when: { inCategory: "x" } }] }), []);
});

// ── conditions from the handshake ────────────────────────────────────────────

test("a page type offers only the conditions the storefront announced for it", () => {
  assert.deepEqual(conditionsFor(DESCRIPTORS, "product").map((x) => x.name), ["inCategory", "productType", "property"]);
  assert.deepEqual(conditionsFor(DESCRIPTORS, "category").map((x) => x.name), ["inCategory"]);
  assert.deepEqual(conditionsFor(DESCRIPTORS, "page").map((x) => x.name), ["path", "language", "source", "setting"]);
  assert.deepEqual(conditionsFor([], "page"), []);
});

test("each condition's form comes from its value schema; inCategory is the category tree", () => {
  const input = (name: string) => conditionInput(DESCRIPTORS.find((x) => x.name === name)!);
  assert.deepEqual(input("inCategory"), { kind: "categories" });
  assert.deepEqual(input("path"), { kind: "field", field: { type: "string", label: "Path, * matches any characters", id: "path" } });
  assert.deepEqual(input("source"), { kind: "field", field: { ...DESCRIPTORS[5].value, id: "source" } });
  // an object's fields are its `element`, as in a section schema; `settings` are not read
  assert.deepEqual(input("property"), { kind: "field", field: { ...nameEquals, id: "property" } });
  const bySettings: ConditionDescriptor = { name: "property", pageTypes: ["product"], value: { type: "object", settings: nameEquals.element } };
  assert.deepEqual(conditionInput(bySettings), { kind: "field", field: { ...bySettings.value, id: "property" } });
  assert.equal(checkCondition(bySettings, { name: "x" }), undefined);
  // a plugin's list of texts
  const tiers: ConditionDescriptor = { name: "loyalty.tier", pageTypes: ["product"], value: { type: "list" } };
  assert.deepEqual(conditionInput(tiers), { kind: "texts" });
  assert.deepEqual(conditionInput({ ...tiers, value: { type: "string", multiple: true } }), { kind: "texts" });
  assert.equal(checkCondition(tiers, ["gold", "silver"]), undefined);
  assert.deepEqual(checkCondition(tiers, "gold"), { code: "TEXT_LIST" });
  assert.deepEqual(splitTexts(" gold, ,silver ,"), ["gold", "silver"]);
});

test("values are checked as their schema says", () => {
  const plugin = (value: ConditionDescriptor["value"]): ConditionDescriptor => ({ name: "x", pageTypes: ["page"], value });
  assert.equal(checkCondition(plugin({ type: "number" }), 3), undefined);
  assert.deepEqual(checkCondition(plugin({ type: "number" }), "3"), { code: "NUMBER" });
  assert.equal(checkCondition(plugin({ type: "checkbox" }), false), undefined);
  assert.deepEqual(checkCondition(plugin({ type: "checkbox" }), "no"), { code: "BOOLEAN" });
  const property = DESCRIPTORS[2];
  // a product property's value may be other than a text
  assert.equal(checkCondition(property, { name: "Bundle", equals: true }), undefined);
  assert.deepEqual(checkCondition(property, "Bundle"), { code: "NOT_OBJECT" });
});

test("the form drops the announced conditions left empty and keeps the others as they were", () => {
  const when = { inCategory: [], productType: " ", property: { name: "", equals: "" }, vendor: { id: "acme" }, path: "" };
  assert.deepEqual(cleanWhen(when, DESCRIPTORS, "product"), { vendor: { id: "acme" }, path: "" });
  assert.equal(isEmptyValue(false), false);
  assert.equal(isEmptyValue({ name: "x", equals: undefined }), false);

  const { known, unknown } = splitConditions({ inCategory: ["a"], vendor: 1, path: "/x" }, DESCRIPTORS, "product");
  assert.deepEqual(known.map((x) => x.name), ["inCategory"]);
  assert.deepEqual(unknown, [{ name: "vendor", value: 1 }, { name: "path", value: "/x" }]);
});

test("Applies to: the rules that pick a template, the defaults among them", () => {
  const rules = readRules({
    product: [
      { template: "product.printer", when: { inCategory: ["a"] } },
      { template: "product.bundle" },
      { template: " product.printer ", when: { productType: "Physical" } },
    ],
  });
  assert.deepEqual(rulesFor(rules, "product.printer"), [0, 2]);
  assert.deepEqual(rulesFor(rules, "product"), []);
  assert.deepEqual(rulesFor(rules, "category.x"), []);
  assert.deepEqual(rulesFor(readRules(undefined), "product.b2c"), [0]);
});

test("a rules file's entries the designer can't read are counted: a save of the rules leaves them out", () => {
  const file = {
    version: 2,
    product: [{ template: "product.a" }, { template: 4 }, "printers", { template: "product.b", when: [] }],
    category: { template: "category.a" },
    page: [{ template: "page.a", when: { path: "/a" } }],
    other: "kept",
  };
  assert.equal(unreadableRules(file), 4);
  // what is left is what the designer reads and writes back; a key it doesn't know stays
  const read = readRules(file);
  assert.deepEqual(rulesOf(read, "product").map((x) => x.template), ["product.a"]);
  assert.equal(hasOwnRules(read, "category"), false);
  assert.equal(read.other, "kept");
  assert.equal(unreadableRules(undefined), 0);
  assert.equal(unreadableRules(v1), 0);
  assert.equal(unreadableRules(["not", "an", "object"]), 1);
});

test("a list condition needs at least one text, none of them blank", () => {
  const productType = { name: "tiers", pageTypes: ["product"], value: { type: "list" } } as ConditionDescriptor;
  assert.equal(checkCondition(productType, ["gold"]), undefined);
  assert.deepEqual(checkCondition(productType, []), { code: "TEXT_LIST" });
  assert.deepEqual(checkCondition(productType, ["gold", " "]), { code: "TEXT_LIST" });
  assert.deepEqual(checkCondition(DESCRIPTORS[0], []), { code: "CATEGORY_IDS" });
});

test("the storefront's built-in conditions have their texts in the editor's language; any other goes by its own label", () => {
  assert.deepEqual(Object.keys(BUILT_IN_CONDITIONS), DESCRIPTORS.map((x) => x.name));
  const messages: Record<string, string> = { "PAGE_BUILDER.DESIGNER.RULES.SOURCES.PAGES": "Designer pages" };
  const i18n = { t: (key: string) => messages[key] ?? key, te: (key: string) => key in messages };
  assert.equal(optionLabel({ value: "Pages", label: "Page" }, i18n), "Designer pages");
  assert.equal(optionLabel({ value: "Blog", label: "Blog post" }, i18n), "Blog post");
});
