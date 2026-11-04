import assert from "node:assert/strict";
import test from "node:test";
import { storefrontPage } from "../src/modules/designer/preview";

test("a storefront page is the storefront's URL joined with the path, its own path kept", () => {
  assert.equal(storefrontPage("https://shop.test/b2b/", "/custom-preview")?.toString(), "https://shop.test/b2b/custom-preview");
  assert.equal(storefrontPage("https://shop.test", "designer-preview")?.toString(), "https://shop.test/designer-preview");
});

test("there is no storefront page without an http(s) storefront", () => {
  assert.equal(storefrontPage(undefined, "/designer-preview"), undefined);
  assert.equal(storefrontPage("javascript:alert(1)//", "/designer-preview"), undefined);
  assert.equal(storefrontPage("not a url", "/designer-preview"), undefined);
});
