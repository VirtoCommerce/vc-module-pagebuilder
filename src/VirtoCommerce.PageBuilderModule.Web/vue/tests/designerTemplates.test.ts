import assert from "node:assert/strict";
import test from "node:test";
import {
  PAGE_CONTENT,
  alternateKey,
  alternateName,
  entityUrl,
  isTemplateKey,
  mainSectionState,
  pageTypeOf,
  startingTemplate,
  templateEntry,
  templateKeyOf,
  templatePath,
} from "../src/modules/designer/templates";
import type { Item, PageDocument, Schemas } from "../src/modules/designer/types";

const schemas: Schemas = {
  sections: {},
  blocks: {},
  shared: {},
  objects: {},
  templates: {
    page: { name: "Pages", type: "pages" },
    blogs: { name: "Blog", type: "blogs" },
    "product-template": { name: "Product template", type: "themes", previewMessage: { templateKey: "product" }, sections: ["product-info"] },
  },
};

test("template keys: a page type, or an alternate of it", () => {
  assert.ok(isTemplateKey("product"));
  assert.ok(isTemplateKey("page.landing-2"));
  for (const key of ["blog", "product.", "product.Printer", "product.a.b", "product.-x", "../product"]) assert.ok(!isTemplateKey(key), key);
  assert.equal(pageTypeOf("category.brands"), "category");
  assert.equal(pageTypeOf("pages"), undefined);
});

test("a template is a theme file under templates/", () => {
  assert.equal(templatePath("default", "product.printer"), "default/templates/product.printer.json");
  assert.equal(templateKeyOf("default/templates/product.printer.json"), "product.printer");
  assert.equal(templateKeyOf("templates/page.landing.json-draft"), "page.landing");
  assert.equal(templateKeyOf("default/sections/header-group.json"), undefined);
  assert.equal(templateKeyOf("default/templates/notes.json"), undefined);
});

test("an alternate's key comes from its name", () => {
  assert.equal(alternateKey("product", "  Big Printers! "), "product.big-printers");
  assert.equal(alternateKey("page", "Café landing"), "page.cafe-landing");
  assert.equal(alternateKey("product", "Crème brûlée"), "product.creme-brulee");
  assert.equal(alternateKey("category", "!!"), undefined);
  assert.equal(alternateName("product.big-printers"), "Big printers");
});

test("the descriptor is picked by the opened template's key", () => {
  assert.deepEqual(templateEntry(schemas), { key: "page", entry: schemas.templates!.page });
  assert.deepEqual(templateEntry(schemas, "page").entry.name, "Pages");
  assert.deepEqual(templateEntry(schemas, "product"), { key: "product-template", entry: schemas.templates!["product-template"] });
  assert.deepEqual(templateEntry(schemas, "category"), { key: "category", entry: {} });
  assert.deepEqual(templateEntry({ ...schemas, templates: undefined }), { key: "page", entry: {} });

  const own: Schemas = {
    ...schemas,
    templates: {
      page: { name: "Pages", type: "pages" },
      product: { name: "Product", type: "themes", previewMessage: { templateKey: "product" } },
      category: { name: "Category", type: "themes", previewMessage: { templateKey: "category" } },
      "page-template": { name: "Page template", type: "themes", previewMessage: { templateKey: "page" }, settings: [{ id: "layout", type: "select" }] },
    },
  };
  assert.equal(templateEntry(own, "category").key, "category");
  assert.equal(templateEntry(own, "page").key, "page-template");
  assert.equal(templateEntry(own).key, "page");
});

test("a new template starts from the default, else the storefront's built-in one, never as a legacy array", () => {
  const base: PageDocument = { version: 1, settings: { type: "settings" } as Item, content: [{ id: "s1", type: "product-info" }] };
  const copy = startingTemplate("product", base);
  assert.deepEqual(copy, { settings: { type: "settings" }, content: [{ id: "s1", type: "product-info" }] });
  copy.content.push({ id: "s2", type: "text" });
  assert.equal(base.content.length, 1);

  const builtIn = startingTemplate("product", undefined, { default: { settings: {}, content: [{ type: "related-products" }] } });
  assert.deepEqual(builtIn.content.map((x) => x.type), ["related-products", "product-info"]);
  assert.ok(builtIn.content.every((x) => x.id));
  // The storefront's product descriptor has `default: true`, which is not a document.
  const category = startingTemplate("category", undefined, { default: true });
  assert.deepEqual(category.settings, {});
  assert.deepEqual(category.content.map((x) => x.type), ["category-listing"]);
});

test("a new product or category template starts with its main section, after its own sections", () => {
  const made: string[] = [];
  const mainItem = (type: string): Item => {
    made.push(type);
    return { id: `${type}-1`, type, blocks: [{ id: "b1", type: "description" }] };
  };
  const copy = startingTemplate("product", { settings: {} as Item, content: [{ id: "h", type: "hero" }] }, undefined, mainItem);
  assert.deepEqual(copy.content.map((x) => x.id), ["h", "product-info-1"]);
  assert.equal(copy.content[1].blocks?.length, 1);
  assert.equal(mainSectionState(copy.content, "product"), "shown");
  const empty = startingTemplate("category", undefined, undefined, mainItem);
  assert.equal(mainSectionState(empty.content, "category"), "shown");
  const kept = startingTemplate("product", { settings: {} as Item, content: [{ id: "p", type: "product-info", hidden: true }] }, undefined, mainItem);
  assert.deepEqual(kept.content.map((x) => x.id), ["p"]);
  const grouped = startingTemplate("category", { settings: {} as Item, content: [{ id: "g", type: "category-listing", __group: "header" }] }, undefined, mainItem);
  assert.deepEqual(grouped.content.map((x) => x.id), ["g", "category-listing-1"]);
  assert.deepEqual(made, ["product-info", "category-listing", "category-listing"]);
});

test("a page template has the full layout and the page's content", () => {
  const doc = startingTemplate("page");
  assert.equal(doc.settings.layout, "full");
  assert.deepEqual(doc.content.map((x) => x.type), [PAGE_CONTENT]);
  const copy = startingTemplate("page", { settings: { layout: "blank" } as Item, content: [{ id: "h", type: "hero" }, { id: "pc", type: PAGE_CONTENT }] });
  assert.equal(copy.settings.layout, "blank");
  assert.deepEqual(copy.content.map((x) => x.id), ["h", "pc"]);
});

test("the preview opens a real page of the entity", () => {
  assert.equal(entityUrl("product", "78d65d4f"), "/product/78d65d4f");
  assert.equal(entityUrl("category", "a b"), "/category/a%20b");
  assert.equal(entityUrl("page", "/landing/spring"), "/landing/spring");
  assert.equal(entityUrl("page", "about"), "/about");
});
