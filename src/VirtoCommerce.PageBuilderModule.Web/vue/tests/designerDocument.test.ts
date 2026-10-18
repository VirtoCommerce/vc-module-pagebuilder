import assert from "node:assert/strict";
import test from "node:test";
import { chromeToSections, fromDocument, isComponentRef, resolveRefs, toDocument } from "../src/modules/designer/document";

test("documents load from every stored shape", () => {
  assert.deepEqual(toDocument(""), { settings: {}, content: [] });
  assert.deepEqual(toDocument('{"settings":{"name":"a"},"content":[{"id":"t1","type":"text"}]}'), {
    settings: { name: "a" },
    content: [{ id: "t1", type: "text" }],
  });
  const legacy = toDocument([{ type: "settings" }, { id: "t1", type: "text" }]);
  assert.equal(legacy.version, 1);
  assert.deepEqual(fromDocument(legacy), [{ type: "settings" }, { id: "t1", type: "text" }]);
  assert.equal(toDocument({ pageContent: '{"settings":{},"content":[{"id":"x","type":"text"}]}' }).content[0].id, "x");
});

test("items without an id get one, blocks too", () => {
  const doc = toDocument({ settings: {}, content: [{ type: "slider", blocks: [{ type: "text" }] }] });
  assert.match(doc.content[0].id, /^slider/);
  assert.match(doc.content[0].blocks![0].id, /^text/);
});

test("shared references are recognized strictly", () => {
  assert.equal(isComponentRef({ id: "componentRefab12cd34", type: "componentRef", componentRef: "c1" }), true);
  assert.equal(isComponentRef({ id: "componentRefab12cd34", type: "componentRef", componentRef: " " }), false);
  assert.equal(isComponentRef({ id: "x", type: "componentRef", componentRef: "c1", title: "extra" }), false);
});

test("references resolve to the component's sections with placement boundaries", () => {
  const doc = {
    settings: {},
    content: [
      { id: "t1", type: "text" },
      { id: "ref1", type: "componentRef", componentRef: "c1" },
      { id: "ref2", type: "componentRef", componentRef: "gone" },
      { id: "t2", type: "text" },
    ],
  };
  const components = new Map([
    ["c1", { content: [{ id: "a", type: "title" }, { id: "b", type: "slider", blocks: [{ id: "c", type: "text" }] }], label: "Shared" }],
    ["gone", null],
  ]);
  const { template, sharedComponentBoundaries, missingSharedComponentIds } = resolveRefs(doc, components);
  assert.deepEqual(template.content.map((x) => x.type), ["text", "title", "slider", "text"]);
  assert.match(template.content[1].id, /^lc[0-9a-f]+s0$/);
  assert.match(template.content[2].blocks![0].id, /^lc[0-9a-f]+s1b0$/);
  assert.deepEqual(sharedComponentBoundaries, [{ placementId: "ref1", componentRef: "c1", startIndex: 1, count: 2, label: "Shared" }]);
  assert.deepEqual(missingSharedComponentIds, ["gone"]);
  // the page itself is untouched
  assert.equal(doc.content.length, 4);
});

test("page settings' breadcrumbs and H1 become sections at the top, once", () => {
  const text = { id: "text1", type: "text" };
  const page = (settings: Record<string, unknown>) => ({ settings: { type: "settings", seoInfo: { pageTitle: "T" }, ...settings }, content: [text] });
  const types = (doc?: { content: { type: string }[] }) => doc?.content.map((x) => x.type);

  // never set: the storefront showed breadcrumbs
  const shown = chromeToSections(page({}));
  assert.deepEqual(types(shown), ["breadcrumbs", "text"]);
  assert.deepEqual(shown?.settings, { type: "settings", seoInfo: { pageTitle: "T" }, hideBreadcrumbs: true });
  const both = chromeToSections(page({ header: "About us" }));
  assert.deepEqual(types(both), ["breadcrumbs", "page-heading", "text"]);
  assert.equal(both?.content[1].text, "About us");
  assert.equal("header" in both!.settings, false);
  assert.deepEqual(types(chromeToSections(page({ hideBreadcrumbs: true, header: "About us" }))), ["page-heading", "text"]);
  // already moved, or nothing shown: as it is
  assert.equal(chromeToSections(shown!), undefined);
  assert.equal(chromeToSections(page({ hideBreadcrumbs: true })), undefined);
});
