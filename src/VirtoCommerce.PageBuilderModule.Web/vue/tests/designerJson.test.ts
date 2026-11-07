import assert from "node:assert/strict";
import test from "node:test";
import { clone, isRecord, isText, isTextList } from "../src/modules/designer/json";

test("an object is a record; an array or null is not", () => {
  assert.equal(isRecord({}), true);
  assert.equal(isRecord([]), false);
  assert.equal(isRecord(null), false);
  assert.equal(isRecord("x"), false);
});

test("a text has more than white space", () => {
  assert.equal(isText("a"), true);
  assert.equal(isText(" "), false);
  assert.equal(isText(4), false);
});

test("a list of strings is checked for its shape only: empty lists and empty strings pass", () => {
  assert.equal(isTextList([]), true);
  assert.equal(isTextList(["a", ""]), true);
  assert.equal(isTextList(["a", 1]), false);
  assert.equal(isTextList("a"), false);
});

test("a copy shares nothing with the original, and undefined stays undefined", () => {
  const value = { a: [{ b: 1 }] };
  const copy = clone(value);
  copy.a[0].b = 2;
  assert.equal(value.a[0].b, 1);
  assert.equal(clone(undefined), undefined);
});
