import assert from "node:assert/strict";
import test from "node:test";
import {
  hasThemeDraft,
  parseThemeStatus,
  publishActions,
  publishThemeUrl,
  templateStatus,
  themeErrors,
  themePublishButton,
  type ThemeStatus,
} from "../src/modules/designer/themePublication";

const query = (url: string) => Object.fromEntries(new URL(url, "https://admin.example").searchParams);

const status = (drafts: string[], publication?: number): ThemeStatus => ({ publication, drafts });

test("parseThemeStatus reads the publication and the drafts, and nothing else", () => {
  assert.deepEqual(parseThemeStatus({ publication: 3, drafts: ["templates/product.json", "sections/header-group.json"] }), {
    publication: 3,
    drafts: ["templates/product.json", "sections/header-group.json"],
  });
  // the platform leaves out a null publication: before the first one
  assert.deepEqual(parseThemeStatus({ drafts: [] }), { publication: undefined, drafts: [] });
  assert.deepEqual(parseThemeStatus({ publication: "3", drafts: ["config/brand.json", 7, null] }), { publication: undefined, drafts: ["config/brand.json"] });
  assert.deepEqual(parseThemeStatus(null), { publication: undefined, drafts: [] });
});

test("hasThemeDraft matches a file as saves name it to the status's path in the theme", () => {
  const drafts = status(["sections/header-group.json", "config/template_rules.json"], 2);

  assert.equal(hasThemeDraft(drafts, "default", "default/sections/header-group.json"), true);
  assert.equal(hasThemeDraft(drafts, "default", "default/config/template_rules.json"), true);
  assert.equal(hasThemeDraft(drafts, "default", "default/sections/footer-group.json"), false);
  // another theme's file of the same path is not this theme's draft
  assert.equal(hasThemeDraft(drafts, "b2c", "default/sections/header-group.json"), false);
  assert.equal(hasThemeDraft(undefined, "default", "default/sections/header-group.json"), false);
});

test("templateStatus: changed while the template has a draft, unpublished before the theme's first publication", () => {
  const path = "default/templates/product.json";

  assert.deepEqual(templateStatus(status(["templates/product.json"], 4), "default", path), { published: true, hasChanges: true });
  assert.deepEqual(templateStatus(status(["sections/header-group.json"], 4), "default", path), { published: true, hasChanges: false });
  assert.deepEqual(templateStatus(status(["templates/product.json"]), "default", path), { published: false, hasChanges: true });
  // the status could not be read: nothing to publish or discard is claimed
  assert.deepEqual(templateStatus(undefined, "default", path), { published: true, hasChanges: false });
});

test("Publish theme counts the theme files waiting and is enabled only with some, nothing unsaved and nothing running", () => {
  const waiting = status(["templates/product.json", "sections/header-group.json", "config/template_rules.json"], 1);

  assert.deepEqual(themePublishButton({ status: waiting, unsaved: false, busy: false }), {
    label: { key: "PAGE_BUILDER.DESIGNER.PUBLISH_THEME_COUNT", count: 3 },
    disabled: false,
  });
  assert.deepEqual(themePublishButton({ status: status([], 1), unsaved: false, busy: false }), {
    label: { key: "PAGE_BUILDER.DESIGNER.PUBLISH_THEME" },
    disabled: true,
  });
  assert.equal(themePublishButton({ status: waiting, unsaved: true, busy: false }).disabled, true);
  assert.equal(themePublishButton({ status: waiting, unsaved: false, busy: true }).disabled, true);
  assert.equal(themePublishButton({ status: undefined, unsaved: false, busy: false }).disabled, true);
});

test("publishActions: a page publishes itself, a template only with the theme, a shared component neither; nothing without builder:publish", () => {
  assert.deepEqual(publishActions({ template: false, shared: false, mayPublish: true }), { page: true, theme: true });
  assert.deepEqual(publishActions({ template: false, shared: false, mayPublish: false }), { page: false, theme: false });
  // a template's Publish is the theme's; it has no Unpublish
  assert.deepEqual(publishActions({ template: true, shared: false, mayPublish: true }), { page: false, theme: true });
  assert.deepEqual(publishActions({ template: true, shared: false, mayPublish: false }), { page: false, theme: false });
  assert.deepEqual(publishActions({ template: false, shared: true, mayPublish: true }), { page: false, theme: false });
});

test("the theme's publication names the catalog previewed", () => {
  const url = new URL(publishThemeUrl("B2B-store", "abc12345"), "https://admin.example");

  assert.equal(url.pathname, "/api/pagebuilder/theme/publish");
  assert.deepEqual(Object.fromEntries(url.searchParams), { storeId: "B2B-store", catalogHash: "abc12345" });
  // without a handshake there is none: the server answers why
  assert.deepEqual(query(publishThemeUrl("B2B-store")), { storeId: "B2B-store" });
});

test("themeErrors lists what a refused theme save or publication names", () => {
  const body = JSON.stringify({ error: "The theme is not valid: …", errors: ["templates/product.json: 's1' has a type the storefront does not have, 'carousel'"] });

  assert.deepEqual(themeErrors(400, body), ["templates/product.json: 's1' has a type the storefront does not have, 'carousel'"]);
  // a refusal with one error only: a live theme write on a New Page Builder store
  assert.deepEqual(themeErrors(400, JSON.stringify({ error: "theme files are published with theme/publish" })), ["theme files are published with theme/publish"]);
  assert.deepEqual(themeErrors(400, JSON.stringify({ errors: [], error: "" })), []);
  assert.deepEqual(themeErrors(400, "Bad Request"), []);
  assert.deepEqual(themeErrors(409, body), []);
  assert.deepEqual(themeErrors(500, body), []);
});
