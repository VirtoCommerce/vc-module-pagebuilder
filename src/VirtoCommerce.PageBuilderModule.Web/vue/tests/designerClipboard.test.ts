import assert from "node:assert/strict";
import test from "node:test";
import { fromClipboard, toClipboard } from "../src/modules/designer/clipboard";

test("a copied section or block reads back from the clipboard", () => {
  const section = { id: "text1234", type: "text", text: "Hi" };
  assert.deepEqual(fromClipboard(toClipboard("section", section)), { type: "section", content: section });
  assert.deepEqual(fromClipboard(toClipboard("block", section))?.type, "block");
});

test("anything else on the clipboard is refused", () => {
  assert.equal(fromClipboard("hello"), undefined);
  assert.equal(fromClipboard(""), undefined);
  assert.equal(fromClipboard(undefined), undefined);
  assert.equal(fromClipboard('{"type":"page","content":{"type":"text"}}'), undefined);
  assert.equal(fromClipboard('{"type":"section","content":{"id":"x"}}'), undefined);
  assert.equal(fromClipboard('{"type":"section","content":"text"}'), undefined);
});
