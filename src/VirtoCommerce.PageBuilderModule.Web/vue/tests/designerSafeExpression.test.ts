import assert from "node:assert/strict";
import test from "node:test";
import { evaluate, isSupportedExpression, UnsupportedExpressionError } from "../src/modules/designer/safeExpression";

// the visibility expressions of vc-frontend's section schemas, as they ship
const VC_FRONTEND_VISIBILITY = [
  "!!this.item && this.item.action === 'url'",
  "!this.model.background",
  "this.model.countLimitation !== false",
  "this.model.countLimitation === true",
  "this.model.hideViewModeSelector !== false",
];

const unsupported = (expression: string, context: unknown = {}) => assert.throws(() => evaluate(expression, context), UnsupportedExpressionError);

test("every visibility expression the storefront's schemas ship is supported", () => {
  assert.deepEqual(VC_FRONTEND_VISIBILITY.filter((x) => !isSupportedExpression(x)), []);
});

test("expressions read the context through this", () => {
  assert.equal(evaluate("this.model.countLimitation !== false", { model: { countLimitation: true } }), true);
  assert.equal(evaluate("!this.model.background", { model: {} }), true);
  assert.equal(evaluate("!!this.item && this.item.action === 'url'", { item: undefined }), false);
});

test("arithmetic, logic and conditionals are evaluated", () => {
  assert.equal(evaluate("1 + 2 * 3", {}), 7);
  assert.equal(evaluate("this.a ? 'yes' : 'no'", { a: 0 }), "no");
  assert.deepEqual(evaluate("this.items || []", { items: null }), []);
  assert.equal(evaluate("this.value ?? 5", { value: 0 }), 0);
});

test("the helpers given and plain string and array methods can be called", () => {
  assert.equal(evaluate("join(this.url, '/designer-preview')", { url: "https://store" }, { join: (a: string, b: string) => a + b }), "https://store/designer-preview");
  assert.equal(evaluate("(this.permissions || []).indexOf('builder:theme') === -1", { permissions: ["builder:theme"] }), false);
  assert.equal(evaluate("encodeURIComponent(this.folder)", { folder: "a b" }), "a%20b");
  assert.equal(evaluate("JSON.stringify(this.content, null, 2)", { content: { a: 1 } }), '{\n  "a": 1\n}');
});

for (const expression of [
  "(function() { return document.cookie })()",
  'this.constructor.constructor("alert(1)")()',
  "this.__proto__",
  "this['constructor']",
  "alert(1)",
  "window.location",
  'fetch("https://elsewhere.example")',
  "this.items.map(x => x)",
  'new Function("return 1")()',
  "this.a = 1",
  "'abc'.constructor",
  "this.toString()",
  "constructor",
  "toString",
  "__proto__",
  "hasOwnProperty",
]) {
  test(`${expression} is not run`, () => unsupported(expression, { items: [], a: 0 }));
}

test("an optional link that meets nothing ends the whole chain", () => {
  assert.equal(evaluate("this.a?.b.c || 'x'", {}), "x");
  assert.equal(evaluate("this.a?.b.trim()", {}), undefined);
  assert.equal(evaluate("this.a?.b", { a: { b: 1 } }), 1);
});

test("a call on nothing is a runtime error, not an unsupported form", () => {
  assert.throws(() => evaluate("this.permissions.indexOf('builder:theme')", {}), TypeError);
});

test("JSON.stringify takes no replacer", () => {
  unsupported("JSON.stringify(this.x, ['id'])", { x: { id: 1, name: "a" } });
});

test("a helper is not handed what the context has no name for", () => {
  let called = false;
  assert.throws(() => evaluate("walk([constructor])", {}, { walk: () => (called = true) }), UnsupportedExpressionError);
  assert.equal(called, false);
});
