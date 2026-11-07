import assert from "node:assert/strict";
import test from "node:test";
import { designerDiscardBody, designerDiscardUrl, designerSaveBody, designerSaveUrl } from "../src/modules/designer/designerSave";

const query = (url: string) => Object.fromEntries(new URL(url, "https://admin.example").searchParams);

test("a Save is one request to the designer's save, naming the catalog previewed when it is known", () => {
  assert.equal(new URL(designerSaveUrl("B2B-store"), "https://admin.example").pathname, "/api/pagebuilder/designer/save");
  assert.deepEqual(query(designerSaveUrl("B2B-store", "abc12345")), { storeId: "B2B-store", catalogHash: "abc12345" });
  assert.deepEqual(query(designerSaveUrl("B2B-store")), { storeId: "B2B-store" });
  assert.deepEqual(query(designerSaveUrl("B2B-store", "")), { storeId: "B2B-store" });
});

test("the body carries the database page with its version and every theme file, each content as JSON text", () => {
  const header = { type: "header", name: "Header", content: [{ id: "h", type: "site-header", since: "2026-10-16T12:00:00Z" }] };
  const body = designerSaveBody({
    page: { groupId: "g1", eTag: '"v1"', content: { settings: {}, content: [] } },
    themeFiles: [
      { path: "default/sections/header-group.json", content: header },
      { path: "default/config/template_rules.json", content: { version: 2, conditions: [] } },
    ],
  });

  assert.deepEqual(body.page, { groupId: "g1", eTag: '"v1"', content: JSON.stringify({ settings: {}, content: [] }, null, 2) });
  assert.deepEqual(
    body.themeFiles.map((x) => x.path),
    ["default/sections/header-group.json", "default/config/template_rules.json"],
  );
  // the text is what the server saves and what the history tells the published version by
  assert.deepEqual(JSON.parse(body.themeFiles[0].content), header);
  assert.equal(body.themeFiles[0].content, JSON.stringify(header, null, 2));
});

test("a file page is named by its path and type; without a page only the theme files go", () => {
  const filePage = designerSaveBody({ page: { path: "about.page", type: "pages", content: { settings: { type: "settings" }, content: [] } }, themeFiles: [] });
  assert.deepEqual(Object.keys(filePage.page!), ["path", "type", "content"]);

  const themeOnly = designerSaveBody({ themeFiles: [{ path: "default/templates/product.json", content: { content: [] } }] });
  assert.equal(themeOnly.page, undefined);
  assert.equal(JSON.stringify(themeOnly).includes('"page"'), false);
});

test("a Discard is one request naming the page and the theme files whose drafts go, each file once", () => {
  assert.equal(designerDiscardUrl("B2B store"), "/api/pagebuilder/designer/discard?storeId=B2B+store");
  assert.deepEqual(
    designerDiscardBody({
      page: { groupId: "g1" },
      themeFiles: ["default/sections/header-group.json", "default/config/brand.json", "default/sections/header-group.json"],
    }),
    { page: { groupId: "g1" }, themeFiles: ["default/sections/header-group.json", "default/config/brand.json"] },
  );
  assert.deepEqual(designerDiscardBody({ page: { path: "about.page", type: "pages" }, themeFiles: [] }), {
    page: { path: "about.page", type: "pages" },
    themeFiles: [],
  });
  assert.equal(JSON.stringify(designerDiscardBody({ themeFiles: ["default/templates/product.json"] })).includes('"page"'), false);
});
