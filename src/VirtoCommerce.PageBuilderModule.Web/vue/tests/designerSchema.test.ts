import assert from "node:assert/strict";
import test from "node:test";
import { copyItem, fieldsOf, fill, isVisible, itemTitle, newItem, withTextAt } from "../src/modules/designer/schema";
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

test("visibility expressions see the context as this; a broken one shows the field", () => {
  const field = { id: "n", type: "number", visibility: "this.model.countLimitation === true" };
  assert.equal(isVisible(field, { model: { countLimitation: true } }), true);
  assert.equal(isVisible(field, { model: { countLimitation: false } }), false);
  assert.equal(isVisible({ ...field, visibility: "this.nope.deeper" }, {}), true);
  assert.equal(isVisible({ ...field, hidden: true }, { model: { countLimitation: true } }), false);
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
