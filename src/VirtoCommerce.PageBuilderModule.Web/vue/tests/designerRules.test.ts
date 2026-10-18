import assert from "node:assert/strict";
import test from "node:test";
import {
  DEFAULT_RULES,
  cleanWhen,
  conditionsOf,
  formatRules,
  moveItem,
  parseRules,
  rulesFor,
  rulesFromFile,
  rulesOf,
  validateRules,
  withRules,
  type TemplateRules,
} from "../src/modules/designer/rules";

const file = {
  version: 1,
  product: [
    { template: "product.printer", when: { inCategory: ["printers-id"] } },
    { template: "product.bundle", when: { property: { name: "Bundle", equals: true } }, disabled: true },
  ],
  category: [{ template: "category.brands", when: { inCategory: ["brands-id"] } }],
  page: [{ template: "page.landing", when: { path: "/landing/*", source: "designer" } }],
};

test("a rules file parses as it is", () => {
  const { rules, errors } = parseRules(JSON.stringify(file));
  assert.deepEqual(errors, []);
  assert.deepEqual(rules, file);
  assert.deepEqual(rulesOf(rules!, "product").map((x) => x.template), ["product.printer", "product.bundle"]);
  assert.deepEqual(rulesOf(rules!, "page").length, 1);
});

test("no rules file: the storefront's default (the B2C product property)", () => {
  const rules = rulesFromFile(undefined);
  assert.deepEqual(rules, DEFAULT_RULES);
  // a copy: editing it never changes the default
  rulesOf(rules, "product")[0].template = "product.other";
  assert.equal(DEFAULT_RULES.product![0].template, "product.b2c");
});

test("text that isn't JSON says so", () => {
  const { rules, errors } = parseRules("{ product: [ }");
  assert.equal(rules, undefined);
  assert.equal(errors.length, 1);
  assert.equal(errors[0].code, "SYNTAX");
  assert.ok(errors[0].params?.detail);
});

test("errors point at their JSON path", () => {
  const errors = validateRules({
    version: "1",
    product: [
      { template: "product.printer", when: { inCategory: [] } },
      { template: "category.brands" },
      { template: "product.x", when: { property: { name: "Brand" } }, disabled: "yes" },
      "rule",
    ],
    category: { template: "category.x" },
    page: [{ template: "page.landing", when: { source: "cms", path: "" } }],
  });
  assert.deepEqual(
    errors.map((x) => `${x.path}: ${x.code}`),
    [
      "version: NUMBER",
      "product[0].when.inCategory: CATEGORY_IDS",
      "product[1].template: TEMPLATE",
      "product[2].disabled: BOOLEAN",
      "product[2].when.property: NAME_EQUALS",
      "product[3]: NOT_OBJECT",
      "category: NOT_LIST",
      "page[0].when.source: SOURCE",
      "page[0].when.path: TEXT",
    ],
  );
  assert.deepEqual(validateRules([]), [{ path: "", code: "NOT_OBJECT" }]);
});

test("conditions the designer doesn't know are custom, never errors", () => {
  const rule = { template: "product.printer", when: { inCategory: ["printers-id"], vendor: { id: "acme" }, path: "/x" } };
  assert.deepEqual(validateRules({ product: [rule] }), []);
  const { known, custom } = conditionsOf(rule, "product");
  assert.deepEqual(known.map(([name]) => name), ["inCategory"]);
  // a page condition on a product rule is custom there
  assert.deepEqual(custom.map(([name]) => name), ["vendor", "path"]);
});

test("edits keep what the designer doesn't know: top-level keys, rule keys, custom conditions", () => {
  const raw = {
    version: 1,
    owner: "marketing",
    product: [
      { template: "product.printer", when: { inCategory: ["printers-id"], vendor: { id: "acme" } }, note: "Q4" },
      { template: "product.bundle", when: { productType: "BillOfMaterials" } },
    ],
    page: [{ template: "page.landing", when: { path: "/landing/*" } }],
  };
  let rules: TemplateRules = parseRules(JSON.stringify(raw)).rules!;

  // reorder, switch off, and edit a rule's known condition through the form's cleanup
  const list = moveItem(rulesOf(rules, "product"), 1, 0).map((rule) =>
    rule.template === "product.printer"
      ? { ...rule, disabled: true, when: cleanWhen({ ...rule.when, inCategory: ["printers-id", "scanners-id"], productType: "", property: { name: "", equals: "" } }, "product") }
      : rule,
  );
  rules = withRules(rules, "product", list);

  const saved = JSON.parse(formatRules(rules)) as typeof raw;
  assert.equal(saved.owner, "marketing");
  assert.deepEqual(saved.page, raw.page);
  assert.deepEqual(saved.product.map((x) => x.template), ["product.bundle", "product.printer"]);
  assert.deepEqual(saved.product[1], {
    template: "product.printer",
    when: { inCategory: ["printers-id", "scanners-id"], vendor: { id: "acme" } },
    note: "Q4",
    disabled: true,
  });
});

test("a list replaced in a file without a version gets one", () => {
  assert.deepEqual(withRules({}, "category", []), { version: 1, category: [] });
  assert.deepEqual(withRules({ version: 2 }, "category", []).version, 2);
});

test("Applies to: the rules that pick a template", () => {
  const rules: TemplateRules = {
    product: [
      { template: "product.printer", when: { inCategory: ["a"] } },
      { template: "product.bundle" },
      { template: " product.printer ", when: { productType: "Physical" } },
    ],
  };
  assert.deepEqual(rulesFor(rules, "product.printer"), [0, 2]);
  assert.deepEqual(rulesFor(rules, "product"), []);
  assert.deepEqual(rulesFor(rules, "category.x"), []);
});
