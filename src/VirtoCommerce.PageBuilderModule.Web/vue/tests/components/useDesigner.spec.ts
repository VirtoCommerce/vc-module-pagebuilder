import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { effectScope, nextTick } from "vue";
import { useDesigner, type Designer } from "../../src/modules/designer/useDesigner";
import * as api from "../../src/modules/designer/api";
import { ApiError, type FilePage } from "../../src/modules/designer/api";
import { useThemeLook } from "../../src/modules/designer/useThemeLook";
import { themePublishButton } from "../../src/modules/designer/themePublication";
import type { Handshake } from "../../src/modules/designer/previewMessages";
import type { Item, Schemas } from "../../src/modules/designer/types";
import { fakePlatform, status, type FakePlatform, type Request } from "./support/fakePlatform";
import { TEST_TOKEN } from "./support/vcShell";

const STORE = "B2B-store";
const LOCKED = "PAGE_BUILDER.DESIGNER.MAIN_SECTION.LOCKED";

const HANDSHAKE: Handshake = { hash: "catalog-hash-1", capabilities: ["groups"], conditions: [], paletteTokens: { light: [], dark: [] } };

const title = { id: "title", type: "string", label: "Title", translatable: true };
const SCHEMAS: Schemas = {
  sections: {
    hero: { name: "Hero", settings: [title] },
    "product-info": {
      name: "Product info",
      settings: [{ id: "variationsLayout", type: "select", default: "b2b", options: [{ value: "b2b", label: "B2B" }, { value: "b2c", label: "B2C" }] }],
    },
    "site-header": { name: "Header", groups: ["header"], settings: [{ id: "announcement", type: "string", translatable: true }] },
    "site-footer": { name: "Footer", groups: ["footer"], settings: [] },
  },
  blocks: {},
  shared: {},
  objects: {},
};

const PAGE = { settings: { type: "settings" }, content: [{ id: "hero-1", type: "hero", title: "Welcome" }] };
const HEADER = { type: "header", name: "Header", content: [{ id: "header-1", type: "site-header", announcement: "Free shipping" }] };
const PRODUCT_TEMPLATE = {
  settings: {},
  content: [
    { id: "hero-t", type: "hero", title: "Hello" },
    { id: "info-1", type: "product-info" },
  ],
};

/** the theme files the fake platform has, by their path with the theme folder */
let themeFiles: Record<string, unknown>;
let themeStatus: { publication?: number; drafts: string[] };
let pageStatus: { published?: boolean; hasChanges?: boolean };
let platform: FakePlatform;
let designer: Designer;
let scope: ReturnType<typeof effectScope>;

beforeEach(() => {
  themeFiles = { "default/sections/header-group.json": HEADER, "default/templates/product.json": PRODUCT_TEMPLATE, "about.page": PAGE };
  themeStatus = { publication: 3, drafts: [] };
  pageStatus = { published: true, hasChanges: false };
  platform = fakePlatform()
    .on("GET", `/api/stores/${STORE}`, { id: STORE, dynamicProperties: [{ name: "DefaultThemeName", values: [{ value: "default" }] }] })
    .on("GET", "/api/page-builder-pages/grouped/g1/content", { content: JSON.stringify(PAGE), eTag: '"v1"' })
    .on("GET", "/api/page-builder-pages/grouped/publish-status/g1", () => pageStatus)
    .on("GET", "/api/pagebuilder/git/publish-status", () => pageStatus)
    .on("GET", "/api/pagebuilder/settings", {})
    .on("GET", "/api/pagebuilder/template", (request: Request) =>
      request.query.path in themeFiles ? JSON.stringify(themeFiles[request.query.path]) : status(404),
    )
    .on("GET", "/api/pagebuilder/theme/status", () => themeStatus)
    .on("POST", "/api/pagebuilder/designer/save", { eTag: '"v2"' })
    .on("POST", "/api/pagebuilder/designer/discard", {})
    .on("POST", "/api/page-builder-pages/grouped/publishing/g1", "")
    .on("POST", "/api/pagebuilder/theme/publish", () => {
      themeStatus = { publication: 4, drafts: [] };
      return { publication: 4 };
    })
    .install();
  scope = effectScope();
  designer = scope.run(() => useDesigner())!;
});

afterEach(() => scope.stop());

/** the storefront previewed has loaded: its schemas and its handshake */
function previewLoaded() {
  designer.schemas.value = SCHEMAS;
  designer.handshake.value = HANDSHAKE;
}

const openDatabasePage = async () => {
  await designer.open({ groupId: "g1", storeId: STORE });
  previewLoaded();
  await nextTick();
};
const productTemplate: FilePage = { storeId: STORE, theme: "default", type: "themes", path: "default/templates/product.json" };
const openTemplate = async () => {
  await designer.open(productTemplate);
  previewLoaded();
  await nextTick();
};
const section = (id: string) => designer.doc.value.content.find((x) => x.id === id) as Item;
const saves = () => platform.to("/api/pagebuilder/designer/save", "POST");

describe("useDesigner: opening", () => {
  it("opens a database page at its version, with the theme's header, as the platform user", async () => {
    await openDatabasePage();

    expect(designer.loadError.value).toBeUndefined();
    expect(designer.groupId.value).toBe("g1");
    expect(designer.doc.value.content.map((x) => [x.type, x.__group])).toEqual([
      ["site-header", "header"],
      ["hero", undefined],
      // a theme without a footer file starts with the storefront's footer section, as saved
      ["site-footer", "footer"],
    ]);
    expect(designer.dirty.value).toBe(false);
    expect(designer.status.value).toEqual({ published: true, hasChanges: false });
    expect(designer.themeStatus.value).toEqual({ publication: 3, drafts: [] });
    const read = platform.to("/api/page-builder-pages/grouped/g1/content")[0];
    expect(read.query).toEqual({ includeVersion: "true" });
    expect(read.headers.authorization).toBe(`Bearer ${TEST_TOKEN}`);
  });

  it("opens a file page from its draft, with the store's flow and the page's state", async () => {
    pageStatus = { published: true, hasChanges: true };
    await designer.open({ storeId: STORE, theme: "default", type: "pages", path: "about.page" });

    expect(designer.file.value?.path).toBe("about.page");
    expect(designer.gitFlow.value).toBe(false);
    expect(designer.doc.value.content.filter((x) => !x.__group).map((x) => x.id)).toEqual(["hero-1"]);
    expect(platform.to("/api/pagebuilder/template").find((x) => x.query.path === "about.page")?.query).toMatchObject({ type: "pages", draft: "true" });
    expect(designer.status.value).toEqual({ published: true, hasChanges: true });
    expect(designer.canPublish.value).toBe(true);
  });

  it("reports a page it can't open and has no edits for it", async () => {
    platform.on("GET", "/api/page-builder-pages/grouped/g1/content", status(403));
    await designer.open({ groupId: "g1", storeId: STORE });

    expect(designer.loadError.value).toBeInstanceOf(ApiError);
    expect(designer.dirty.value).toBe(false);
    expect(await designer.save()).toBe(false);
    expect(saves()).toHaveLength(0);
  });
});

describe("useDesigner: editing and saving", () => {
  it("is dirty after an edit and clean again once the edit is undone", async () => {
    await openDatabasePage();
    designer.selection.value = { kind: "section", id: "hero-1" };

    designer.setField("title", "Hello there");
    expect(designer.dirty.value).toBe(true);
    expect(designer.canPublish.value).toBe(false);

    designer.undo();
    expect(section("hero-1").title).toBe("Welcome");
    expect(designer.dirty.value).toBe(false);
  });

  it("saves the page, the header and the theme files given in one request naming the catalog previewed", async () => {
    await openDatabasePage();
    designer.selection.value = { kind: "section", id: "hero-1" };
    designer.setField("title", "Hello there");
    designer.selection.value = { kind: "section", id: "header-1" };
    designer.setField("announcement", "Free returns");
    const rules = { path: "default/config/template_rules.json", content: { version: 2, conditions: [] } };

    expect(await designer.save([rules])).toBe(true);

    expect(saves()).toHaveLength(1);
    const [save] = saves();
    expect(save.query).toEqual({ storeId: STORE, catalogHash: HANDSHAKE.hash });
    const body = save.body as { page: { groupId: string; eTag: string; content: string }; themeFiles: { path: string; content: string }[] };
    expect(body.page).toMatchObject({ groupId: "g1", eTag: '"v1"' });
    expect(JSON.parse(body.page.content).content).toEqual([{ id: "hero-1", type: "hero", title: "Hello there" }]);
    expect(body.themeFiles.map((x) => x.path)).toEqual(["default/config/template_rules.json", "default/sections/header-group.json"]);
    expect(JSON.parse(body.themeFiles[1].content).content).toEqual([{ id: "header-1", type: "site-header", announcement: "Free returns" }]);
    // saved: the next save goes with the new version, nothing is unsaved, the state is read again
    expect(designer.dirty.value).toBe(false);
    expect(platform.to("/api/pagebuilder/theme/status").length).toBeGreaterThan(1);

    designer.selection.value = { kind: "section", id: "hero-1" };
    designer.setField("title", "Again");
    await designer.save();
    expect((saves()[1].body as { page: { eTag: string } }).page.eTag).toBe('"v2"');
  });

  it("saves only what changed: an edited header alone sends no page", async () => {
    await openDatabasePage();
    designer.selection.value = { kind: "section", id: "header-1" };
    designer.setField("announcement", "Free returns");

    await designer.save();

    const body = saves()[0].body as { page?: unknown; themeFiles: { path: string }[] };
    expect(body.page).toBeUndefined();
    expect(body.themeFiles.map((x) => x.path)).toEqual(["default/sections/header-group.json"]);
  });

  it("keeps the edits unsaved when someone else saved the page meanwhile (412)", async () => {
    await openDatabasePage();
    platform.on("POST", "/api/pagebuilder/designer/save", status(412, "Precondition Failed"));
    designer.selection.value = { kind: "section", id: "hero-1" };
    designer.setField("title", "Mine");

    const failed = await designer.save().catch((e: unknown) => e);

    expect(failed).toBeInstanceOf(ApiError);
    expect((failed as ApiError).status).toBe(412);
    expect(saves()).toHaveLength(1);
    expect(designer.dirty.value).toBe(true);
    expect(section("hero-1").title).toBe("Mine");
    expect(designer.busy.value).toBeUndefined();

    // the next Save still goes with the version the page was read at
    platform.on("POST", "/api/pagebuilder/designer/save", { eTag: '"v9"' });
    expect(await designer.save()).toBe(true);
    expect((saves()[1].body as { page: { eTag: string } }).page.eTag).toBe('"v1"');
  });

  it("refuses to save a database page read without its version (428), sending nothing", async () => {
    platform.on("GET", "/api/page-builder-pages/grouped/g1/content", { content: JSON.stringify(PAGE) });
    await openDatabasePage();
    designer.selection.value = { kind: "section", id: "hero-1" };
    designer.setField("title", "Mine");

    await expect(designer.save()).rejects.toMatchObject({ status: 428 });
    expect(saves()).toHaveLength(0);
  });

  it("names the parts a Save would write: the page, the header, the footer", async () => {
    await openDatabasePage();
    expect(designer.changedParts.value).toEqual([]);
    designer.selection.value = { kind: "section", id: "header-1" };
    designer.setField("announcement", "Free returns");
    expect(designer.changedParts.value).toEqual(["header"]);
    designer.selection.value = { kind: "section", id: "hero-1" };
    designer.setField("title", "Hello there");
    expect(designer.changedParts.value).toEqual(["page", "header"]);
  });

  it("offers every theme part with a draft to discard, and discards only the page and the parts chosen, in one request", async () => {
    pageStatus = { published: true, hasChanges: true };
    themeStatus = { publication: 3, drafts: ["sections/header-group.json", "config/template_rules.json", "config/brand.json", "config/settings_data.json"] };
    await openDatabasePage();
    // the old designer's theme settings are not the Vue designer's to discard
    expect(designer.discardable.value).toEqual({ page: true, theme: ["rules", "header", "brand"] });

    // the footer has no draft: it is not sent
    await designer.discardDrafts({ page: true, theme: ["header", "footer"] });

    const discards = platform.to("/api/pagebuilder/designer/discard", "POST");
    expect(discards).toHaveLength(1);
    expect(discards[0].query).toEqual({ storeId: STORE });
    expect(discards[0].body).toEqual({ page: { groupId: "g1" }, themeFiles: ["default/sections/header-group.json"] });
  });

  it("never discards a theme draft not chosen: a page alone leaves the store's theme drafts", async () => {
    pageStatus = { published: true, hasChanges: true };
    themeStatus = { publication: 3, drafts: ["sections/header-group.json", "config/settings_data.json"] };
    await openDatabasePage();

    await designer.discardDrafts({ page: true, theme: [] });
    expect(platform.to("/api/pagebuilder/designer/discard")[0].body).toEqual({ page: { groupId: "g1" }, themeFiles: [] });

    // nothing chosen: nothing is sent
    await designer.discardDrafts({ page: false, theme: [] });
    expect(platform.to("/api/pagebuilder/designer/discard")).toHaveLength(1);
  });

  it("discards a template's draft as a theme file; a page never published keeps its only copy", async () => {
    themeStatus = { publication: 3, drafts: ["templates/product.json"] };
    await openTemplate();
    await designer.discardDrafts({ page: true, theme: [] });
    expect(platform.to("/api/pagebuilder/designer/discard")[0].body).toEqual({ themeFiles: ["default/templates/product.json"] });

    pageStatus = { published: false, hasChanges: true };
    themeStatus = { publication: 3, drafts: [] };
    await openDatabasePage();
    await designer.discardDrafts({ page: true, theme: [] });
    expect(platform.to("/api/pagebuilder/designer/discard")).toHaveLength(1);
  });

  it("keeps the header and footer as saved for an editor who may not change the theme", async () => {
    designer.groupsEditable.value = false;
    await openDatabasePage();

    // in the preview, not in the tree; their texts are not edited from the preview
    expect(designer.groupsPreview.value?.header).toEqual(HEADER.content);
    expect(designer.groupsShown.value).toBe(false);
    expect(designer.isLocked("header-1")).toBe(true);
    expect(designer.isLocked("hero-1")).toBe(false);
    expect(designer.setText("header-1", ["announcement"], "Free returns")).toBeUndefined();
    expect(section("header-1").announcement).toBe("Free shipping");
    // the page's own still are
    expect(designer.setText("hero-1", ["title"], "Hello there")).toEqual({ kind: "section", id: "hero-1" });
    expect(designer.changedParts.value).toEqual(["page"]);
  });
});

describe("useDesigner: publishing", () => {
  const button = () => themePublishButton({ status: designer.themeStatus.value, unsaved: designer.dirty.value, busy: !!designer.busy.value });

  it("offers Publish theme while theme files wait, counting them; not while something is unsaved", async () => {
    themeStatus = { publication: 3, drafts: ["sections/header-group.json", "templates/product.json"] };
    await openDatabasePage();
    expect(button()).toEqual({ label: { key: "PAGE_BUILDER.DESIGNER.PUBLISH_THEME_COUNT", count: 2 }, disabled: false });

    designer.selection.value = { kind: "section", id: "hero-1" };
    designer.setField("title", "Unsaved");
    expect(button().disabled).toBe(true);
  });

  it("publishes the theme against the catalog previewed; then nothing waits", async () => {
    themeStatus = { publication: 3, drafts: ["sections/header-group.json"] };
    await openDatabasePage();

    expect(await designer.publishTheme()).toBe(4);

    const publish = platform.to("/api/pagebuilder/theme/publish", "POST");
    expect(publish).toHaveLength(1);
    expect(publish[0].query).toEqual({ storeId: STORE, catalogHash: HANDSHAKE.hash });
    expect(button()).toEqual({ label: { key: "PAGE_BUILDER.DESIGNER.PUBLISH_THEME" }, disabled: true });
  });

  it("publishes only the page: its header and footer go live with the theme", async () => {
    pageStatus = { published: true, hasChanges: true };
    themeStatus = { publication: 3, drafts: ["sections/header-group.json"] };
    await openDatabasePage();

    await designer.publish(true);

    expect(platform.to("/api/page-builder-pages/grouped/publishing/g1", "POST").map((x) => x.query)).toEqual([{ publish: "true" }]);
    expect(platform.to("/api/pagebuilder/theme/publish")).toHaveLength(0);
  });

  it("has no Publish of its own for a template: it goes live with the theme", async () => {
    await openTemplate();
    const before = platform.requests.length;

    await designer.publish(true);

    expect(platform.requests.slice(before).filter((x) => x.method === "POST")).toEqual([]);
  });
});

describe("useDesigner: translatable settings and the main section", () => {
  beforeEach(() => {
    designer.languages.value = { all: ["en-US", "de-DE"], default: "en-US" };
    designer.language.value = "de-DE";
  });

  it("writes a template's translatable text as the language's, keeping the default language's", async () => {
    await openTemplate();

    expect(designer.translating(section("hero-t"))).toEqual({ language: "de-DE", defaultLanguage: "en-US", languages: ["en-US", "de-DE"] });
    expect(designer.setText("hero-t", ["title"], "Hallo", "de-DE")).toEqual({ kind: "section", id: "hero-t" });
    expect(section("hero-t").title).toEqual({ "en-US": "Hello", "de-DE": "Hallo" });

    // the default language's edit of a dictionary keeps the others
    designer.setText("hero-t", ["title"], "Hi", "en-US");
    expect(section("hero-t").title).toEqual({ "en-US": "Hi", "de-DE": "Hallo" });
  });

  it("translates the header's texts on a page, never the page's own (a page has one language)", async () => {
    await openDatabasePage();

    designer.setText("header-1", ["announcement"], "Kostenloser Versand", "de-DE");
    expect(section("header-1").announcement).toEqual({ "en-US": "Free shipping", "de-DE": "Kostenloser Versand" });

    expect(designer.translating(section("hero-1"))).toBeUndefined();
    designer.setText("hero-1", ["title"], "Willkommen", "de-DE");
    expect(section("hero-1").title).toBe("Willkommen");
  });

  it("keeps a template's last main section: it can't be removed or hidden, alone or among others", async () => {
    await openTemplate();

    expect(designer.remove({ kind: "section", id: "info-1" })).toBe(LOCKED);
    expect(designer.toggleHidden({ kind: "section", id: "info-1" })).toBe(LOCKED);
    expect(section("info-1")).toEqual({ id: "info-1", type: "product-info" });

    designer.checked.value = ["hero-t", "info-1"];
    expect(designer.removeChecked()).toBe(LOCKED);
    expect(designer.doc.value.content.filter((x) => !x.__group).map((x) => x.id)).toEqual(["info-1"]);
  });

  it("puts a missing main section back with the defaults its schema declares", async () => {
    themeFiles["default/templates/product.json"] = { ...PRODUCT_TEMPLATE, content: [PRODUCT_TEMPLATE.content[0]] };
    await openTemplate();
    expect(designer.mainSection.value).toEqual({ type: "product-info", state: "missing" });

    designer.restoreMainSection();
    const main = designer.doc.value.content.find((x) => x.type === "product-info");
    expect(main).toMatchObject({ type: "product-info", variationsLayout: "b2b" });
    expect(designer.mainSection.value).toEqual({ type: "product-info", state: "shown" });
  });

  it("starts a template without a file from its main section, with the defaults its schema declares", async () => {
    delete themeFiles["default/templates/product.json"];
    await openTemplate();

    expect(designer.doc.value.content.filter((x) => !x.__group)).toEqual([expect.objectContaining({ type: "product-info", variationsLayout: "b2b" })]);
  });

  it("lets one of two main sections go", async () => {
    themeFiles["default/templates/product.json"] = { ...PRODUCT_TEMPLATE, content: [...PRODUCT_TEMPLATE.content, { id: "info-2", type: "product-info" }] };
    await openTemplate();

    expect(designer.remove({ kind: "section", id: "info-1" })).toBeUndefined();
    expect(designer.remove({ kind: "section", id: "info-2" })).toBe(LOCKED);
    expect(designer.mainSection.value).toEqual({ type: "product-info", state: "shown" });
  });
});

describe("api: theme files and what may not exist", () => {
  it("reads a theme file the theme lacks as none, and keeps any other failure's status and body", async () => {
    expect(await api.loadThemeFile(STORE, "default", "default/config/brand.json")).toBeUndefined();
    expect(await api.loadThemeFile(STORE, "default", "default/sections/header-group.json")).toEqual(HEADER);
    platform.on("GET", "/api/pagebuilder/catalog", status(404));
    expect(await api.hasCatalog(STORE, "catalog-hash-1")).toBe(false);

    platform.on("GET", "/api/pagebuilder/template", status(403, "Forbidden"));
    await expect(api.loadThemeFile(STORE, "default", "default/config/brand.json")).rejects.toMatchObject({ status: 403, body: "Forbidden" });
  });

  it("starts the brand of a theme without a brand file from the logos of its settings file, which it never writes", async () => {
    themeFiles["default/config/settings_data.json"] = { current: { logo_image: "/l.svg", favicon_image: "/f.ico" } };
    const look = scope.run(() => useThemeLook())!;
    await look.load(STORE, "default");
    expect(look.look.value.brand).toEqual({ logoUrl: "/l.svg", faviconUrl: "/f.ico" });
    expect(look.dirty.value).toBe(false);

    look.setBrand("logoUrl", "/new.svg");
    expect(look.pending().map((x) => x.file.path)).toEqual(["default/config/brand.json"]);
  });

  it("reads no settings file for a theme with a brand file; a refusal is the server's, not a theme without a brand", async () => {
    themeFiles["default/config/brand.json"] = { logoUrl: "/b.svg" };
    themeFiles["default/config/settings_data.json"] = { current: { logo_image: "/l.svg" } };
    const look = scope.run(() => useThemeLook())!;
    await look.load(STORE, "default");
    expect(look.look.value.brand).toEqual({ logoUrl: "/b.svg" });
    expect(platform.to("/api/pagebuilder/template").map((x) => x.query.path)).not.toContain("default/config/settings_data.json");

    platform.on("GET", "/api/pagebuilder/template", status(403, "Forbidden"));
    await look.load(STORE, "default");
    expect(look.loaded.value).toBe(false);
    expect(look.loadError.value).toBeInstanceOf(ApiError);
    expect((look.loadError.value as ApiError).status).toBe(403);
  });
});
