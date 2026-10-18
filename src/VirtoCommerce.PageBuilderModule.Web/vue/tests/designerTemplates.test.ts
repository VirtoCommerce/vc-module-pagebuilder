import assert from "node:assert/strict";
import test from "node:test";
import {
  PAGE_CONTENT,
  alternateKey,
  alternateName,
  entityUrl,
  isTemplateKey,
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
  // a draft only, as the content module lists it
  assert.equal(templateKeyOf("templates/page.landing.json-draft"), "page.landing");
  assert.equal(templateKeyOf("default/sections/header-group.json"), undefined);
  assert.equal(templateKeyOf("default/templates/notes.json"), undefined);
});

test("an alternate's key comes from its name", () => {
  assert.equal(alternateKey("product", "  Big Printers! "), "product.big-printers");
  assert.equal(alternateKey("page", "Café landing"), "page.cafe-landing");
  assert.equal(alternateKey("category", "!!"), undefined);
  assert.equal(alternateName("product.big-printers"), "Big printers");
});

test("the descriptor is picked by the opened template's key", () => {
  // pages: the pages entry, under its own key
  assert.deepEqual(templateEntry(schemas), { key: "page", entry: schemas.templates!.page });
  // page templates of a storefront without their own descriptor: the pages one
  assert.deepEqual(templateEntry(schemas, "page").entry.name, "Pages");
  // by the template key its preview message names, under the descriptor's key (what targetTemplates name)
  assert.deepEqual(templateEntry(schemas, "product"), { key: "product-template", entry: schemas.templates!["product-template"] });
  // none yet: an empty entry for the type, never the pages one
  assert.deepEqual(templateEntry(schemas, "category"), { key: "category", entry: {} });
  assert.deepEqual(templateEntry({ ...schemas, templates: undefined }), { key: "page", entry: {} });

  // the storefront's own descriptors: product, category, page-template
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
  assert.equal(builtIn.content[0].type, "related-products");
  assert.ok(builtIn.content[0].id);
  // `default: true` (the product descriptor today) isn't a document
  assert.deepEqual(startingTemplate("category", undefined, { default: true }), { settings: {}, content: [] });
});

test("a page template has the full layout and the page's content", () => {
  const doc = startingTemplate("page");
  assert.equal(doc.settings.layout, "full");
  assert.deepEqual(doc.content.map((x) => x.type), [PAGE_CONTENT]);
  // a copy keeps its layout and its own page-content section
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
