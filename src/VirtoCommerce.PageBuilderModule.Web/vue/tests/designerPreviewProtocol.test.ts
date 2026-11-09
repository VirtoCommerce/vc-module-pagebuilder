import assert from "node:assert/strict";
import test from "node:test";
import { createPreviewChannel } from "../src/modules/designer/preview";
import {
  ANONYMOUS,
  COLOR_SCHEMES,
  authMessage,
  draftMessage,
  ensureCatalog,
  handshakeMessages,
  parseLoaded,
  parsePreviewMessage,
  renderMessage,
  ruleIndex,
  rulesPick,
  selectionFrom,
  storefrontPath,
  storefrontRoute,
  type Catalog,
  type DesignerMessage,
  type Loaded,
} from "../src/modules/designer/previewMessages";
import { readRules } from "../src/modules/designer/rules";
import type { Item, PageDocument } from "../src/modules/designer/types";

/** Mirrors the `loaded` message that vc-frontend's buildPreviewCatalog in preview-v2.ts sends. */
const loadedData = (extra: Record<string, unknown> = {}) => ({
  blocks: { link: { name: "Link" } },
  sections: { "site-header": { name: "Header", groups: ["header"] }, text: { name: "Text" } },
  shared: { title: { settings: [] } },
  objects: {},
  templates: { product: { name: "Product" } },
  settingsSchema: { settings_schema: [] },
  paletteTokens: { light: ["color-primary-500"], dark: ["color-primary-500"] },
  hash: "0badc0de",
  protocol: 2,
  capabilities: ["templates", "layouts", "groups", "inline-edit", "color-scheme", "inspector"],
  conditions: [{ name: "inCategory", pageTypes: ["product", "category"], label: "In category", value: { type: "string" } }],
  ...extra,
});

const fromPreview = (data: unknown, type = "loaded") => ({ source: "preview", type, data });

async function warnings(run: () => unknown): Promise<unknown[][]> {
  const calls: unknown[][] = [];
  const original = console.warn;
  console.warn = (...args: unknown[]) => calls.push(args);
  try {
    await run();
  } finally {
    console.warn = original;
  }
  return calls;
}

test("a protocol 2 loaded gives the catalog without the handshake's own fields, and the handshake", () => {
  const loaded = parseLoaded(loadedData()) as Loaded;
  assert.equal(loaded.hash, "0badc0de");
  assert.deepEqual(loaded.capabilities, ["templates", "layouts", "groups", "inline-edit", "color-scheme", "inspector"]);
  assert.deepEqual(loaded.conditions.map((x) => x.name), ["inCategory"]);
  assert.deepEqual(loaded.paletteTokens, { light: ["color-primary-500"], dark: ["color-primary-500"] });
  assert.deepEqual(Object.keys(loaded.catalog).sort(), ["blocks", "hash", "objects", "paletteTokens", "sections", "settingsSchema", "shared", "templates"]);
  assert.equal(loaded.catalog.hash, "0badc0de");
  assert.deepEqual(loaded.catalog.sections, loadedData().sections);
});

test("a loaded without protocol 2 is a storefront the designer can't preview", () => {
  const { protocol: _protocol, hash: _hash, capabilities: _capabilities, conditions: _conditions, paletteTokens: _tokens, ...v1 } = loadedData();
  assert.equal(parseLoaded(v1), "unsupported");
  assert.equal(parseLoaded(loadedData({ protocol: 1 })), "unsupported");
  assert.equal(parseLoaded(loadedData({ protocol: "2" })), "unsupported");
});

test("a protocol 2 loaded of another shape is dropped; a condition it can't read is left out", async () => {
  for (const bad of [{ hash: "" }, { hash: 12 }, { sections: [] }, { shared: undefined }, { paletteTokens: { light: [] } }, { capabilities: "groups" }, { conditions: {} }, { templates: [] }])
    assert.equal(parseLoaded(loadedData(bad)), undefined, JSON.stringify(bad));
  assert.equal(parseLoaded("loaded"), undefined);
  let loaded: Loaded | undefined;
  const calls = await warnings(() => {
    loaded = parseLoaded(loadedData({ conditions: [{ name: "path", pageTypes: ["page"], value: { type: "string" } }, { name: "", pageTypes: [], value: {} }, "language"] })) as Loaded;
  });
  assert.deepEqual(loaded?.conditions.map((x) => x.name), ["path"]);
  assert.equal(calls.length, 1);
});

test("select names a section, or a block and its section", () => {
  assert.deepEqual(parsePreviewMessage(fromPreview({ sectionId: "s1" }, "select")), { type: "select", sectionId: "s1" });
  assert.deepEqual(parsePreviewMessage(fromPreview({ sectionId: "s1", blockId: "b1" }, "select")), { type: "select", sectionId: "s1", blockId: "b1" });
});

test("resolved carries the url, the template rendered, the rules' pick and what white-labeling overrides", () => {
  const data = {
    url: "/product/p1",
    pageType: "product",
    template: "product.b2c",
    ruleId: "product-0",
    rules: { template: "product.b2c", ruleId: "product-0" },
    overriddenByWhiteLabeling: ["palette", "brand.logoUrl"],
  };
  assert.deepEqual(parsePreviewMessage(fromPreview(data, "resolved")), { type: "resolved", resolved: data });
  const open = { ...data, template: "product.printer", ruleId: null, rules: { template: "product", ruleId: null } };
  assert.deepEqual(parsePreviewMessage(fromPreview(open, "resolved")), { type: "resolved", resolved: open });
});

test("a resolved without the rules' pick, or with one of another shape, is dropped", async () => {
  const data = { url: "/product/p1", pageType: "product", template: "product", ruleId: null, overriddenByWhiteLabeling: [] };
  const calls = await warnings(() => {
    assert.equal(parsePreviewMessage(fromPreview(data, "resolved")), undefined);
    assert.equal(parsePreviewMessage(fromPreview({ ...data, rules: { template: "", ruleId: null } }, "resolved")), undefined);
    assert.equal(parsePreviewMessage(fromPreview({ ...data, rules: { template: "product", ruleId: 3 } }, "resolved")), undefined);
  });
  assert.equal(calls.length, 3);
});

test("the rules' pick names the designer's rule by its index; the template rendered does not count", () => {
  const rules = readRules({ product: [{ id: "r-a", template: "product.a" }, { id: "r-b", template: "product.b" }] });
  const resolved = {
    url: "/product/p1",
    pageType: "product",
    template: "product.printer",
    ruleId: null,
    rules: { template: "product.b", ruleId: "r-b" },
    overriddenByWhiteLabeling: [],
  };
  assert.deepEqual(rulesPick(resolved, rules), { url: "/product/p1", pageType: "product", template: "product.b", ruleId: "r-b", rule: 1 });
  assert.equal(rulesPick({ ...resolved, rules: { template: "product", ruleId: null } }, rules).rule, null);
  assert.equal(rulesPick({ ...resolved, rules: { template: "product.c", ruleId: "r-gone" } }, rules).rule, undefined);
  assert.equal(rulesPick(resolved, undefined).rule, undefined);
});

test("edit keeps the language of a translatable text", () => {
  assert.deepEqual(parsePreviewMessage(fromPreview({ itemId: "s1", path: ["links", 0, "label"], value: "Hi" }, "edit")), {
    type: "edit",
    itemId: "s1",
    path: ["links", 0, "label"],
    value: "Hi",
  });
  assert.deepEqual(parsePreviewMessage(fromPreview({ itemId: "s1", path: ["title"], value: "Hallo", cultureName: "de-DE" }, "edit")), {
    type: "edit",
    itemId: "s1",
    path: ["title"],
    value: "Hallo",
    cultureName: "de-DE",
  });
});

test("a preview message of another shape is dropped with a warning; others' messages are not looked at", async () => {
  const bad = [
    fromPreview({ sectionId: "" }, "select"),
    fromPreview({ sectionId: "s1", blockId: 3 }, "select"),
    fromPreview({}, "hover"),
    fromPreview({ itemId: "s1", path: [], value: "x" }, "edit"),
    fromPreview({ itemId: "s1", path: [{}], value: "x" }, "edit"),
    fromPreview({ itemId: "s1", path: ["title"], value: 1 }, "edit"),
    // A storefront on preview protocol 1 names a rule by its index.
    fromPreview({ pageType: "product", template: "product", rule: 0 }, "resolved"),
    fromPreview({}, "unknown"),
  ];
  const calls = await warnings(() => bad.forEach((message) => assert.equal(parsePreviewMessage(message), undefined)));
  assert.equal(calls.length, bad.length);
  const quiet = await warnings(() => {
    assert.equal(parsePreviewMessage({ source: "builder", type: "select", data: { sectionId: "s1" } }), undefined);
    assert.equal(parsePreviewMessage("loaded"), undefined);
  });
  assert.equal(quiet.length, 0);
});

const doc = (content: Item[]): PageDocument => ({ version: 2, settings: { id: "settings", type: "settings" }, content });

test("a page's render: its own sections, its language, what its rules see, the selected block brought into view", () => {
  const render = renderMessage({
    preview: { template: doc([{ id: "s1", type: "text", blocks: [{ id: "b1", type: "link" }] }]), sharedComponentBoundaries: [] },
    cultureName: "de-DE",
    page: { permalink: "/about", objectType: "Pages" },
    selection: { kind: "block", sectionId: "s1", id: "b1" },
    scroll: true,
    sectionLabels: { s1: "Text" },
  });
  assert.deepEqual(render, {
    type: "render",
    document: { settings: { id: "settings", type: "settings" }, content: [{ id: "s1", type: "text", blocks: [{ id: "b1", type: "link" }] }] },
    page: { permalink: "/about", objectType: "Pages" },
    cultureName: "de-DE",
    selection: { sectionId: "s1", blockId: "b1", scroll: true },
    sectionLabels: { s1: "Text" },
  });
});

test("a template's render names it and the storefront route it shows on", () => {
  const render = renderMessage({
    preview: { template: doc([]), sharedComponentBoundaries: [] },
    cultureName: "en-US",
    templateKey: "product.b2c",
    url: "product/p%201?view=list",
    selection: { kind: "section", id: "s1" },
  });
  assert.equal(render.templateKey, "product.b2c");
  assert.equal(render.url, "/product/p%201?view=list");
  assert.deepEqual(render.selection, { sectionId: "s1", scroll: false });
  assert.equal("page" in render, false);
});

test("a selected shared component is outlined by its first section: the placement isn't in the document", () => {
  const template = doc([
    { id: "s1", type: "text" },
    { id: "lc70s0", type: "text" },
    { id: "lc70s1", type: "text" },
  ]);
  const boundaries = [{ placementId: "p", componentRef: "c1", startIndex: 1, count: 2 }];
  const render = renderMessage({ preview: { template, sharedComponentBoundaries: boundaries }, cultureName: "en-US", selection: { kind: "section", id: "p" }, scroll: true });
  assert.deepEqual(render.selection, { sectionId: "lc70s0", scroll: true });
});

test("a storefront path keeps only the path, query and hash: the host never changes", () => {
  for (const path of ["//evil.example/x?a=1#h", "https://evil.example/x?a=1#h", "\\\\evil.example/x?a=1#h"]) {
    const url = storefrontPath(path);
    assert.equal(url.host, "storefront.invalid", path);
    assert.equal(url.search + url.hash, "?a=1#h", path);
  }
  assert.equal(storefrontPath("product/1").pathname, "/product/1");
});

test("the light and dark switch is one definition for the canvas bar and the Theme tab", () => {
  assert.deepEqual(
    COLOR_SCHEMES.map((x) => x.id),
    ["light", "dark"],
  );
});

test("a loaded with no capabilities and no dark tokens is a storefront's, not a malformed one", () => {
  const loaded = parseLoaded(loadedData({ capabilities: [], paletteTokens: { light: ["color-primary-500"], dark: [] } }));
  assert.ok(loaded && loaded !== "unsupported");
  assert.deepEqual(loaded.capabilities, []);
  assert.deepEqual(loaded.paletteTokens.dark, []);
});

test("a render's route never leaves the storefront", () => {
  for (const path of ["//evil.example/x", "https://evil.example/x", "\\\\evil.example"]) assert.ok(storefrontRoute(path)?.startsWith("/"), path);
  assert.equal(storefrontRoute("//evil.example/x"), "/evil.example/x");
});

test("a render carries the add-section model, and shared components' boundaries when there are any", () => {
  const boundaries = [{ placementId: "p1", componentRef: "c1", startIndex: 0, count: 2, label: "Promo" }];
  const model = { id: "text-1", type: "text" };
  const render = renderMessage({ preview: { template: doc([]), sharedComponentBoundaries: boundaries }, cultureName: "en-US", model, selection: { kind: "page", key: "settings" } });
  assert.deepEqual(render.sharedComponentBoundaries, boundaries);
  assert.deepEqual(render.model, model);
  assert.equal("selection" in render, false);
  assert.equal("sharedComponentBoundaries" in renderMessage({ preview: { template: doc([]), sharedComponentBoundaries: [] }, cultureName: "en-US" }), false);
});

test("the draft: the rules and the header and footer, to a storefront that renders groups", () => {
  const rules = readRules({ product: [{ template: "product.b2c" }] });
  const groups = { header: [{ id: "h1", type: "site-header" }], footer: [{ id: "f1", type: "site-footer" }] };
  assert.deepEqual(draftMessage({ rules, groups, capabilities: ["groups"] }), {
    type: "draft",
    theme: { rules, header: { content: groups.header }, footer: { content: groups.footer } },
  });
  assert.deepEqual(draftMessage({ rules, groups, capabilities: ["templates"] }), { type: "draft", theme: { rules } });
  assert.deepEqual(draftMessage({ capabilities: ["groups"] }), { type: "draft", theme: {} });
});

test("the draft carries the templates to a storefront that renders them from the draft", () => {
  const templates = { product: { settings: {} as Item, content: [{ id: "p1", type: "product-info" }] } };
  assert.deepEqual(draftMessage({ templates, capabilities: ["templates"] }), { type: "draft", theme: { templates } });
  assert.deepEqual(draftMessage({ templates, capabilities: ["groups"] }), { type: "draft", theme: {} });
  assert.deepEqual(draftMessage({ capabilities: ["templates"] }), { type: "draft", theme: {} });
});

test("the draft carries the brand and the palette the Theme tab has, whatever the storefront renders of the groups", () => {
  const brand = { logoUrl: "/logo.svg" };
  const palette = { light: { color_primary_500: "#f99e24" } };
  assert.deepEqual(draftMessage({ look: { brand, palette }, capabilities: [] }), { type: "draft", theme: { brand, palette } });
  // A null palette means the theme has none, so the storefront's preset shows.
  assert.deepEqual(draftMessage({ look: { brand, palette: null }, capabilities: [] }), { type: "draft", theme: { brand, palette: null } });
  // A palette left out (the Theme tab not loaded) or no brand means the published one stays, so its logo still shows.
  assert.deepEqual(draftMessage({ look: { brand }, capabilities: [] }), { type: "draft", theme: { brand } });
  assert.deepEqual(draftMessage({ look: {}, capabilities: [] }), { type: "draft", theme: {} });
});

test("after loaded: who the preview renders as, the inspector, light or dark, the draft, the render", () => {
  const draft = draftMessage({ capabilities: [] });
  const render = renderMessage({ preview: { template: doc([]), sharedComponentBoundaries: [] }, cultureName: "en-US" });
  assert.deepEqual(handshakeMessages({ inspector: true, colorScheme: "dark", draft, render }), [
    { type: "auth", token: null, userId: null },
    { type: "inspector", enabled: true },
    { type: "colorScheme", scheme: "dark" },
    draft,
    render,
  ]);
  assert.deepEqual(handshakeMessages({ auth: authMessage("u1", "jwt"), inspector: false, colorScheme: "light", draft, render })[0], {
    type: "auth",
    token: { access_token: "jwt" },
    userId: "u1",
  });
});

test("a click selects the block it names, in the section that has it; else the section", () => {
  const page = doc([
    { id: "s1", type: "text", blocks: [{ id: "b1", type: "link" }] },
    { id: "s2", type: "text" },
  ]);
  assert.deepEqual(selectionFrom(page, { sectionId: "s1", blockId: "b1" }), { kind: "block", sectionId: "s1", id: "b1" });
  assert.deepEqual(selectionFrom(page, { sectionId: "s2" }), { kind: "section", id: "s2" });
  assert.deepEqual(selectionFrom(page, { sectionId: "lc1s0", blockId: "lc1s0b0" }), { kind: "section", id: "lc1s0" });
});

test("the storefront's rule id names the designer's rule by its id, the ids a version 1 file reads with among them", () => {
  const rules = readRules({
    version: 1,
    product: [{ template: "product.a" }, { template: "product.b", id: "big" }, { template: "product.c" }],
    page: [{ template: "page.x" }, { nope: true }, { template: "page.y" }],
  });
  assert.equal(ruleIndex(rules, "product", "product-0"), 0);
  assert.equal(ruleIndex(rules, "product", "big"), 1);
  assert.equal(ruleIndex(rules, "product", "product-2"), 2);
  assert.equal(ruleIndex(rules, "product", "product-1"), undefined);
  assert.equal(ruleIndex(rules, "page", "page-1"), 1);
  assert.equal(ruleIndex(rules, "product", null), null);
  assert.equal(ruleIndex(rules, "constructor", "constructor-0"), undefined);
});

test("a rule moved keeps the storefront's id pointing at it", () => {
  const rules = readRules({ version: 2, category: [{ id: "a", template: "category.a" }, { id: "b", template: "category.b" }] });
  assert.equal(ruleIndex(rules, "category", "b"), 1);
  assert.equal(ruleIndex({ ...rules, category: [rules.category![1], rules.category![0]] }, "category", "b"), 0);
});

test("a page type without rules of its own: the storefront's id names one of its default rules", () => {
  assert.equal(ruleIndex(readRules({ page: [] }), "product", "default-b2c"), 0);
  assert.equal(ruleIndex(readRules({ product: [] }), "product", "default-b2c"), undefined);
  assert.equal(ruleIndex(undefined, "product", "default-b2c"), undefined);
  assert.equal(ruleIndex(undefined, "category", "category-3"), undefined);
  assert.equal(ruleIndex(undefined, "category", null), null);
});

const catalog = (parseLoaded(loadedData()) as Loaded).catalog;

function fakeServer(has: (hash: string) => boolean | Error, upload: () => void | Error = () => undefined) {
  const calls: string[] = [];
  return {
    calls,
    api: {
      has: async (storeId: string, hash: string) => {
        calls.push(`GET ${storeId} ${hash}`);
        const result = has(hash);
        if (result instanceof Error) throw result;
        return result;
      },
      upload: async (storeId: string, sent: Catalog) => {
        calls.push(`PUT ${storeId} ${sent.hash}`);
        const result = upload();
        if (result instanceof Error) throw result;
      },
    },
  };
}
const status = (code: number) => Object.assign(new Error(String(code)), { status: code });

test("a catalog the server has is not uploaded, and not asked about again", async () => {
  const known = new Map<string, Set<string>>();
  const server = fakeServer(() => true);
  await ensureCatalog("b2b", catalog, server.api, known);
  await ensureCatalog("b2b", catalog, server.api, known);
  assert.deepEqual(server.calls, ["GET b2b 0badc0de"]);
});

test("a catalog the server lacks is uploaded with its hash, once per store", async () => {
  const known = new Map<string, Set<string>>();
  let sent: Catalog | undefined;
  const server = fakeServer(() => false);
  const api = { ...server.api, upload: async (storeId: string, value: Catalog) => ((sent = value), server.api.upload(storeId, value)) };
  await ensureCatalog("b2b", catalog, api, known);
  await ensureCatalog("b2b", catalog, api, known);
  await ensureCatalog("b2c", catalog, api, known);
  assert.deepEqual(server.calls, ["GET b2b 0badc0de", "PUT b2b 0badc0de", "GET b2c 0badc0de", "PUT b2c 0badc0de"]);
  assert.equal(sent?.hash, "0badc0de");
  assert.equal("protocol" in (sent ?? {}), false);
});

test("a failed upload is logged and tried again; an editor who may not upload it skips it quietly", async () => {
  const known = new Map<string, Set<string>>();
  const failing = fakeServer(() => false, () => status(500));
  const logged = await warnings(async () => {
    await ensureCatalog("b2b", catalog, failing.api, known);
    await ensureCatalog("b2b", catalog, failing.api, known);
  });
  assert.equal(logged.length, 2);
  assert.deepEqual(failing.calls, ["GET b2b 0badc0de", "PUT b2b 0badc0de", "GET b2b 0badc0de", "PUT b2b 0badc0de"]);

  const forbidden = fakeServer(() => false, () => status(403));
  const quiet = await warnings(async () => {
    await ensureCatalog("b2c", catalog, forbidden.api, known);
    await ensureCatalog("b2c", catalog, forbidden.api, known);
  });
  assert.equal(quiet.length, 0);
  assert.deepEqual(forbidden.calls, ["GET b2c 0badc0de", "PUT b2c 0badc0de"]);
});

function channel(options: { prepare?: () => Promise<void> } = {}) {
  const posted: DesignerMessage[] = [];
  const log: string[] = [];
  const events: unknown[] = [];
  const draft = draftMessage({ capabilities: [] });
  const render = renderMessage({ preview: { template: doc([]), sharedComponentBoundaries: [] }, cultureName: "en-US" });
  const created = createPreviewChannel({
    post: (message) => {
      posted.push(message);
      log.push(`post ${message.type}`);
    },
    onLoaded: (loaded) => log.push(`loaded ${loaded.hash}`),
    prepare: async () => {
      log.push("catalog");
      await options.prepare?.();
    },
    handshake: () => handshakeMessages({ inspector: false, colorScheme: "light", draft, render }),
    onSelect: (event) => events.push(["select", event]),
    onHover: (id) => events.push(["hover", id]),
    onEdit: (edit) => events.push(["edit", edit]),
    onResolved: (resolved) => events.push(["resolved", resolved]),
  });
  return { ...created, posted, log, events };
}

test("a frame load connects with protocol 2", () => {
  const preview = channel();
  preview.frameLoaded();
  assert.deepEqual(preview.posted, [{ type: "connect", protocol: 2 }]);
});

test("on loaded: the schemas, the catalog, then auth, inspector, colorScheme, draft and render", async () => {
  const preview = channel();
  await preview.receive(fromPreview(loadedData()));
  assert.deepEqual(preview.log, ["loaded 0badc0de", "catalog", "post auth", "post inspector", "post colorScheme", "post draft", "post render"]);
  assert.deepEqual(preview.posted[0], ANONYMOUS);
  assert.equal(preview.connected.value, true);
  preview.send({ type: "hover", sectionId: "s1" });
  assert.deepEqual(preview.posted.at(-1), { type: "hover", sectionId: "s1" });
});

test("nothing but the handshake goes out before it: it carries the state as it is when it goes", async () => {
  let release!: () => void;
  const preview = channel({ prepare: () => new Promise<void>((resolve) => (release = resolve)) });
  const handshake = preview.receive(fromPreview(loadedData()));
  preview.send({ type: "hover", sectionId: "s1" });
  assert.deepEqual(preview.posted, []);
  release();
  await handshake;
  assert.deepEqual(preview.posted.map((x) => x.type), ["auth", "inspector", "colorScheme", "draft", "render"]);
});

test("a frame reloaded during the catalog step drops that handshake", async () => {
  let release!: () => void;
  const preview = channel({ prepare: () => new Promise<void>((resolve) => (release = resolve)) });
  const handshake = preview.receive(fromPreview(loadedData()));
  preview.frameLoaded();
  release();
  await handshake;
  assert.deepEqual(preview.posted, [{ type: "connect", protocol: 2 }]);
  assert.equal(preview.connected.value, false);
});

test("a storefront without protocol 2 is told nothing more, and is not listened to", async () => {
  const preview = channel();
  preview.frameLoaded();
  await preview.receive(fromPreview({ sections: {}, blocks: {}, shared: {}, objects: {} }));
  assert.equal(preview.unsupported.value, true);
  assert.equal(preview.connected.value, false);
  preview.send({ type: "inspector", enabled: false });
  await preview.receive(fromPreview({ sectionId: "s1" }, "select"));
  assert.deepEqual(preview.posted, [{ type: "connect", protocol: 2 }]);
  assert.deepEqual(preview.log, ["post connect"]);
  assert.deepEqual(preview.events, []);
});

test("select, hover and resolved reach the designer once the storefront announced itself", async () => {
  const preview = channel();
  await preview.receive(fromPreview({ sectionId: "s1" }, "select"));
  assert.deepEqual(preview.events, []);
  await preview.receive(fromPreview(loadedData()));
  const resolved = { url: "/product/p1", pageType: "product", template: "product", ruleId: null, rules: { template: "product", ruleId: null }, overriddenByWhiteLabeling: [] };
  await preview.receive(fromPreview({ sectionId: "s1", blockId: "b1" }, "select"));
  await preview.receive(fromPreview({ sectionId: null }, "hover"));
  await preview.receive(fromPreview(resolved, "resolved"));
  assert.deepEqual(preview.events, [
    ["select", { sectionId: "s1", blockId: "b1" }],
    ["hover", null],
    ["resolved", resolved],
  ]);
});

test("an edit counts only from a storefront with inline-edit; a translatable text's keeps its language", async () => {
  const withoutInline = channel();
  await withoutInline.receive(fromPreview(loadedData({ capabilities: ["groups"] })));
  await withoutInline.receive(fromPreview({ itemId: "s1", path: ["title"], value: "Hi" }, "edit"));
  assert.deepEqual(withoutInline.events, []);

  const preview = channel();
  await preview.receive(fromPreview(loadedData()));
  await preview.receive(fromPreview({ itemId: "s1", path: ["title"], value: "Hallo", cultureName: "de-DE" }, "edit"));
  await preview.receive(fromPreview({ itemId: "s1", path: ["title"], value: "Hi" }, "edit"));
  assert.deepEqual(preview.events, [
    ["edit", { itemId: "s1", path: ["title"], value: "Hallo", cultureName: "de-DE" }],
    ["edit", { itemId: "s1", path: ["title"], value: "Hi" }],
  ]);
});
