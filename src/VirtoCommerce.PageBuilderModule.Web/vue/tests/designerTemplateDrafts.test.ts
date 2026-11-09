import assert from "node:assert/strict";
import test from "node:test";
import { draftTemplates, draftedTemplateKeys, savedTemplatesReader, type TemplateSource } from "../src/modules/designer/templateDrafts";
import type { ThemeStatus } from "../src/modules/designer/themePublication";
import type { Item, PageDocument } from "../src/modules/designer/types";

const doc = (...types: string[]): PageDocument => ({ settings: { type: "settings" } as Item, content: types.map((type, i) => ({ id: `${type}-${i}`, type })) });
const status = (drafts: string[], publication?: number): ThemeStatus => ({ publication, drafts });

function source(files: Record<string, PageDocument | undefined>) {
  const requests: string[] = [];
  const src: TemplateSource = {
    keys: async () => {
      requests.push("keys");
      return Object.keys(files);
    },
    read: async (key) => {
      requests.push(key);
      return files[key];
    },
  };
  return { src, requests, files };
}

test("draftedTemplateKeys: the templates among the theme files with a draft", () => {
  assert.deepEqual(
    draftedTemplateKeys(status(["templates/product.json", "sections/header-group.json", "templates/page.landing.json", "config/template_rules.json"])),
    ["page.landing", "product"],
  );
  assert.deepEqual(draftedTemplateKeys(status(["config/brand.json"])), []);
  assert.deepEqual(draftedTemplateKeys(undefined), []);
});

test("no template with a draft: no templates, nothing read (the published snapshot has them all)", async () => {
  const { src, requests } = source({ product: doc("product-info") });
  assert.equal(await savedTemplatesReader()("s/default", status(["sections/header-group.json"], 3), src), undefined);
  assert.deepEqual(requests, []);
});

test("a template with a draft: every template goes, each as last saved", async () => {
  const { src } = source({ product: doc("product-info"), "page.landing": doc("hero", "page-content"), category: undefined });
  const templates = await savedTemplatesReader()("s/default", status(["templates/page.landing.json"], 3), src);
  assert.deepEqual(templates, { product: doc("product-info"), "page.landing": doc("hero", "page-content") });
});

test("a draft never published (a new template) is in the draft, though not listed yet", async () => {
  const { src } = source({ product: doc("product-info") });
  src.read = async (key) => (key === "product.printer" ? doc("product-info", "banner") : doc("product-info"));
  const templates = await savedTemplatesReader()("s/default", status(["templates/product.printer.json"], 1), src);
  assert.deepEqual(Object.keys(templates ?? {}).sort(), ["product", "product.printer"]);
});

test("live templates are read once per publication, drafts on every read", async () => {
  const { src, requests, files } = source({ product: doc("product-info"), category: doc("category-listing") });
  const read = savedTemplatesReader();
  await read("s/default", status(["templates/product.json"], 3), src);
  assert.deepEqual(requests.sort(), ["category", "keys", "product"]);

  requests.length = 0;
  files.product = doc("product-info", "banner");
  assert.deepEqual((await read("s/default", status(["templates/product.json"], 3), src))?.product, doc("product-info", "banner"));
  assert.deepEqual(requests, ["product"]);

  requests.length = 0;
  files.product = doc("product-info");
  await read("s/default", status(["templates/category.json"], 3), src);
  await read("s/default", status(["templates/category.json"], 3), src);
  assert.deepEqual(requests.sort(), ["category", "category", "product"]);

  requests.length = 0;
  await read("s/default", status(["templates/category.json"], 4), src);
  assert.deepEqual(requests.sort(), ["category", "keys", "product"]);

  requests.length = 0;
  await read("s/b2c", status(["templates/category.json"], 4), src);
  assert.deepEqual(requests.sort(), ["category", "keys", "product"]);
});

test("a failed read fails the whole read: an incomplete set would hide the templates it lacks", async () => {
  const { src } = source({ product: doc("product-info"), category: doc("category-listing") });
  src.read = async (key) => {
    if (key === "category") throw new Error("503");
    return doc("product-info");
  };
  await assert.rejects(savedTemplatesReader()("s/default", status(["templates/product.json"], 3), src));
});

test("draftTemplates: the open template as edited wins over its saved one; none while nothing has a draft", () => {
  const saved = { product: doc("product-info"), category: doc("category-listing") };
  const edited = doc("banner", "product-info");
  assert.deepEqual(draftTemplates(saved, { key: "product", document: edited }), { product: edited, category: saved.category });
  assert.deepEqual(draftTemplates(saved, { key: "product.printer", document: edited }), { ...saved, "product.printer": edited });
  assert.equal(draftTemplates(saved), saved);
  assert.equal(draftTemplates(undefined, { key: "product", document: edited }), undefined);
  assert.deepEqual(draftTemplates({}, { key: "page", document: { ...edited, version: 2 } as PageDocument }), { page: edited });
});
