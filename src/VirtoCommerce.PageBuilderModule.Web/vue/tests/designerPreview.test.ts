import assert from "node:assert/strict";
import test from "node:test";
import { previewUrl, storefrontPage } from "../src/modules/designer/preview";

test("a storefront page is the storefront's URL joined with the path, its own path kept", () => {
  assert.equal(storefrontPage("https://shop.test/b2b/", "/custom-preview")?.toString(), "https://shop.test/b2b/custom-preview");
  assert.equal(storefrontPage("https://shop.test/b2b", "/designer-preview")?.toString(), "https://shop.test/b2b/designer-preview");
  assert.equal(storefrontPage("https://shop.test", "designer-preview")?.toString(), "https://shop.test/designer-preview");
  assert.equal(storefrontPage("https://shop.test//", "//designer-preview")?.toString(), "https://shop.test/designer-preview");
  assert.equal(storefrontPage("https://shop.test/b2b/", "/product/p%201?view=list")?.toString(), "https://shop.test/b2b/product/p%201?view=list");
});

test("a path never moves a storefront page to another origin", () => {
  for (const path of ["//evil.example", "https://evil.example", "\\\\evil.example", "/\t/evil.example/x", "@evil.example", ".evil.example"])
    assert.equal(storefrontPage("https://shop.test/b2b", path)?.origin, "https://shop.test", path);
});

test("there is no storefront page without an http(s) storefront", () => {
  assert.equal(storefrontPage(undefined, "/designer-preview"), undefined);
  assert.equal(storefrontPage("javascript:alert(1)//", "/designer-preview"), undefined);
  assert.equal(storefrontPage("not a url", "/designer-preview"), undefined);
});

test("the preview page carries the designer's origin as ep", () => {
  globalThis.window = { location: { origin: "https://platform.test" } } as Window & typeof globalThis;
  assert.equal(previewUrl("https://shop.test/b2b/", "/designer-preview?ep=https://evil.example"), "https://shop.test/b2b/designer-preview?ep=https%3A%2F%2Fplatform.test&protocol=2");
  assert.equal(previewUrl("https://shop.test"), "https://shop.test/designer-preview?ep=https%3A%2F%2Fplatform.test&protocol=2");
  assert.equal(previewUrl(undefined), undefined);
});
