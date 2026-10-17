import assert from "node:assert/strict";
import test from "node:test";
import { anchorOf, pageAnchors, slugifyAnchor } from "../src/modules/designer/anchors";

// the storefront's anchors.test.ts cases: a link resolves only while both sides derive the same string
test("anchors slugify exactly like the storefront", () => {
  assert.equal(slugifyAnchor("Technical Specifications"), "technical-specifications");
  assert.equal(slugifyAnchor("Back To Top!"), "back-to-top");
  assert.equal(slugifyAnchor("--hello--"), "hello");
  assert.equal(slugifyAnchor("Спецификация"), "");
  assert.equal(anchorOf({ id: "text2", type: "text" }), "text2");
  assert.equal(anchorOf({ id: "text2", type: "text", anchor: "   " }), "text2");
  assert.equal(anchorOf({ id: "text2", type: "text", anchor: "Technical Specifications" }), "technical-specifications");
  assert.equal(anchorOf({ id: "text2", type: "text", anchor: "Спецификация" }), "text2");
  assert.equal(anchorOf({ id: "text2", type: "text", anchor: 42 }), "text2");
});

test("the page's anchors: visible sections and blocks in order, once each, no shared placements", () => {
  const anchors = pageAnchors(
    [
      { id: "hero1", type: "hero", anchor: "Top", blocks: [{ id: "b1", type: "button" }, { id: "b2", type: "button", hidden: true }] },
      { id: "gone1", type: "text", hidden: true },
      { id: "ref1", type: "componentRef", componentRef: "c1" },
      { id: "text1", type: "text", anchor: "top" },
    ],
    (item) => ({ name: item.type === "hero" ? "Hero" : "Block" }),
  );
  assert.deepEqual(anchors, [
    { value: "top", label: "Hero" },
    { value: "b1", label: "Block" },
  ]);
});
