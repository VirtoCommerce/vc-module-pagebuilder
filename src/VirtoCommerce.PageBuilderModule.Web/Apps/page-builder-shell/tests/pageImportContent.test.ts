import assert from "node:assert/strict";
import test from "node:test";
import { serializeImportedPageContent } from "../src/modules/page-builder/composables/usePageBuilderDetails/importContent";

test("atomic import adapts legacy exports without dropping settings, section identities or references", () => {
  const settings = { name: "Legacy page", cultureName: "en-US" };
  const section = { id: "hero", type: "componentRef", componentRef: "shared-hero" };
  const legacy = [settings, section];
  for (const input of [legacy, JSON.stringify(legacy)]) {
    assert.deepEqual(JSON.parse(serializeImportedPageContent(input)), {
      settings,
      content: [section],
      version: 1,
    });
  }
  assert.deepEqual(legacy, [settings, section]);
});

test("atomic import accepts an empty legacy page", () => {
  assert.deepEqual(JSON.parse(serializeImportedPageContent([])), { settings: {}, content: [], version: 1 });
});

test("atomic import preserves current-format strings and extension fields", () => {
  const document = { settings: { name: "Current" }, content: [], extension: { enabled: true } };
  const text = JSON.stringify(document, null, 2);
  assert.equal(serializeImportedPageContent(text), text);
  assert.deepEqual(JSON.parse(serializeImportedPageContent(document)), document);
});

test("atomic import rejects malformed JSON before creating a page", () => {
  assert.throws(() => serializeImportedPageContent("{invalid"), SyntaxError);
});
