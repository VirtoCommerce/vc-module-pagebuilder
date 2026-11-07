import assert from "node:assert/strict";
import test from "node:test";
import { copyItem, fieldsOf, fill, isTextEdit, isVisible, itemTitle, newItem, schemaRequest, withTextAt, withTextEdit, withValueAt } from "../src/modules/designer/schema";
import type { Schemas } from "../src/modules/designer/types";

const schemas: Schemas = {
  sections: {
    "call-to-action": { name: "Call to Action", displayField: "title", includeShared: ["buttons"], settings: [{ id: "title", type: "string" }] },
    "page-header": { name: "Page Header", displayField: "default", static: "top", settings: [] },
    slider: { name: "Slider", blocks: [], settings: [{ id: "height", type: "select", default: "auto", sort: -1 }] },
  },
  blocks: { text: { name: "Text", settings: [{ id: "text", type: "markdown", default: "Hi" }] } },
  shared: {
    _sections: { settings: [{ id: "anchor", type: "string", default: null }, { id: "title", type: "string", default: "shared" }] },
    _blocks: { settings: [{ id: "name", type: "string" }] },
    buttons: { settings: [{ id: "buttons", type: "list", element: [{ id: "label", type: "string" }] }] },
  },
  objects: {},
};

test("fields: own, then shared ones it doesn't define, sorted stably", () => {
  assert.deepEqual(fieldsOf(schemas.sections["call-to-action"], "section", schemas).map((x) => x.id), ["title", "anchor", "buttons"]);
  assert.deepEqual(fieldsOf(schemas.sections.slider, "section", schemas).map((x) => x.id), ["height", "anchor", "title"]);
  assert.deepEqual(fieldsOf(schemas.blocks.text, "block", schemas).map((x) => x.id), ["text", "name"]);
  assert.deepEqual(fieldsOf({ settings: [], excludeShared: ["anchor"] }, "section", schemas).map((x) => x.id), ["title"]);
});

test("new items get defaults, an id from the type, and a blocks list when they hold blocks", () => {
  const cta = newItem("call-to-action", "section", schemas);
  assert.equal(cta.type, "call-to-action");
  assert.match(cta.id, /^calltoaction[a-zA-Z0-9]{4}$/);
  assert.deepEqual(cta.buttons, []);
  assert.equal(cta.anchor, null);
  assert.equal(cta.blocks, undefined);
  assert.deepEqual(newItem("slider", "section", schemas).blocks, []);
  // a default model's blocks come with it, with ids of their own
  const withBlocks = newItem("slider", "section", { ...schemas, sections: { slider: { blocks: ["text"], default: { blocks: [{ id: "b1", type: "text" }] } } } });
  assert.equal(withBlocks.blocks?.length, 1);
  assert.equal(withBlocks.blocks?.[0].type, "text");
  assert.notEqual(withBlocks.blocks?.[0].id, "b1");
  assert.equal(newItem("text", "block", schemas).text, "Hi");
});

test("copies get new ids, also for their blocks, and don't share values", () => {
  const item = { id: "slider1234", type: "slider", slides: [{ title: "a" }], blocks: [{ id: "text1234", type: "text" }] };
  const copy = copyItem(item);
  assert.notEqual(copy.id, item.id);
  assert.notEqual(copy.blocks![0].id, "text1234");
  (copy.slides as { title: string }[])[0].title = "b";
  assert.equal(item.slides[0].title, "a");
});

test("titles come from the display field, else the schema name", () => {
  assert.equal(itemTitle({ id: "x", type: "call-to-action", title: "Sale" }, schemas.sections["call-to-action"]), "Sale");
  assert.equal(itemTitle({ id: "x", type: "call-to-action", title: " " }, schemas.sections["call-to-action"]), "Call to Action");
  assert.equal(itemTitle({ id: "x", type: "page-header" }, schemas.sections["page-header"]), "Page Header");
  // no schema yet (the site still loading): the type, readable
  assert.equal(itemTitle({ id: "x", type: "unknown" }, undefined), "Unknown");
  assert.equal(itemTitle({ id: "x", type: "site-header" }, undefined), "Site header");
});

test("visibility expressions see the context as this; a failing one hides the field", (t) => {
  t.mock.method(console, "warn", () => undefined);
  const field = { id: "n", type: "number", visibility: "this.model.countLimitation === true" };
  assert.equal(isVisible(field, { model: { countLimitation: true } }), true);
  assert.equal(isVisible(field, { model: { countLimitation: false } }), false);
  assert.equal(isVisible({ ...field, visibility: "this.nope.deeper" }, {}), false);
  assert.equal(isVisible({ ...field, hidden: true }, { model: { countLimitation: true } }), false);
});

test("a visibility expression that isn't a supported form is not run: the field hides, reported once", (t) => {
  const warn = t.mock.method(console, "warn", () => undefined);
  const field = { id: "n", type: "number", visibility: "(function() { globalThis.__visibilityRan = true; return true })()" };
  assert.equal(isVisible(field, {}), false);
  assert.equal(isVisible(field, {}), false);
  assert.equal((globalThis as Record<string, unknown>).__visibilityRan, undefined);
  assert.equal(warn.mock.callCount(), 1);
});

test("request templates are filled deeply", () => {
  const body = { query: '{categories(storeId:"{{location.params.storeId}}",query:"{{__searchQuery}}")}' };
  assert.deepEqual(fill(body, { location: { params: { storeId: "B2B" } }, __searchQuery: "tea" }), {
    query: '{categories(storeId:"B2B",query:"tea")}',
  });
});

test("a text edited on the page replaces only an existing string, without touching the original", () => {
  const item = { id: "cta1", type: "cta", title: "Sale", buttons: [{ label: "Shop", link: "/x" }], count: 3 };
  assert.deepEqual(withTextAt(item, ["buttons", 0, "label"], "Buy"), { ...item, buttons: [{ label: "Buy", link: "/x" }] });
  assert.equal(withTextAt(item, ["title"], "Big sale")?.title, "Big sale");
  assert.equal(item.title, "Sale");
  assert.equal(withTextAt(item, ["count"], "4"), undefined);
  assert.equal(withTextAt(item, ["missing"], "x"), undefined);
  assert.equal(withTextAt(item, ["buttons", 5, "label"], "x"), undefined);
});

test("the preview edits only plain-text settings the item's schema declares, with a string", () => {
  const fields = [
    ...fieldsOf(schemas.sections["call-to-action"], "section", schemas),
    { id: "body", type: "text" },
    { id: "link", type: "object", element: [{ id: "label", type: "string" }, { id: "image", type: "images" }] },
  ];
  assert.equal(isTextEdit(fields, ["title"], "Sale"), true);
  assert.equal(isTextEdit(fields, ["buttons", 0, "label"], "Buy"), true);
  assert.equal(isTextEdit(fields, ["link", "label"], "Shop"), true);
  assert.equal(isTextEdit(fields, ["title"], 4), false);
  assert.equal(isTextEdit(fields, ["body"], "<b>rich</b>"), false);
  assert.equal(isTextEdit(fields, ["link", "image"], "/x.png"), false);
  assert.equal(isTextEdit(fields, ["missing"], "x"), false);
  assert.equal(isTextEdit(fields, ["buttons", "label"], "x"), false);
  assert.equal(isTextEdit(fields, ["buttons", 0], "x"), false);
  assert.equal(isTextEdit(fields, ["link", 0, "label"], "x"), false);

  // the preview's edit goes through this guard: a text the item holds where the guard refuses is left alone
  const item = { id: "c1", type: "call-to-action", title: "Sale", body: "<b>rich</b>", buttons: [{ label: "Shop" }], link: { label: "Go", image: "/x.png" } };
  for (const path of [["title"], ["buttons", 0, "label"], ["link", "label"], ["body"], ["link", "image"], ["missing"]] as (string | number)[][])
    assert.equal(withTextEdit(item, fields, path, "New") !== undefined, isTextEdit(fields, path, "New"), path.join("."));
});

test("a value at a path is replaced in a copy; a text only where a text is", () => {
  const item = { title: "Sale", count: 3, links: [{ label: "A" }] };
  assert.deepEqual(withValueAt(item, ["count"], 4), { ...item, count: 4 });
  assert.deepEqual(withValueAt(item, ["links", 0], { label: "B" }), { ...item, links: [{ label: "B" }] });
  assert.equal(withValueAt(item, ["links", 2, "label"], "x"), undefined);
  assert.equal(withTextAt(item, ["count"], "4"), undefined);
  assert.equal(withTextAt(item, ["links"], "x"), undefined);
  // what an object inherits is not its own
  assert.equal(withTextAt(item, ["constructor", "name"], "x"), undefined);
  assert.equal(item.count, 3);
});

test("a schema request goes over http(s) only, and to the platform only with the token", () => {
  const location = { href: "https://platform.example/apps/designer/", origin: "https://platform.example" };
  assert.deepEqual(schemaRequest("/api/stores", location), { url: "https://platform.example/api/stores", platform: true });
  assert.deepEqual(schemaRequest("api/stores", location), { url: "https://platform.example/apps/designer/api/stores", platform: true });
  assert.deepEqual(schemaRequest("https://elsewhere.example/schema.json", location), { url: "https://elsewhere.example/schema.json", platform: false });
  assert.equal(schemaRequest("//elsewhere.example/x", location)?.platform, false);
  assert.equal(schemaRequest("http://platform.example/api", location)?.platform, false);
  assert.equal(schemaRequest("https://platform.example.evil.example/api", location)?.platform, false);
  assert.equal(schemaRequest("javascript:alert(1)", location), undefined);
  assert.equal(schemaRequest("data:application/json,[]", location), undefined);
  assert.equal(schemaRequest("http://[", location), undefined);
});
