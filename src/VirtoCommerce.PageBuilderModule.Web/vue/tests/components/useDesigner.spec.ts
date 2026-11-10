import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { effectScope, nextTick } from "vue";
import { useDesigner, type Designer } from "../../src/modules/designer/useDesigner";
import * as api from "../../src/modules/designer/api";
import { ApiError, type FilePage } from "../../src/modules/designer/api";
import { CatalogPendingError } from "../../src/modules/designer/designerSave";
import { useThemeLook } from "../../src/modules/designer/useThemeLook";
import { themePublishButton } from "../../src/modules/designer/themePublication";
import type { Handshake } from "../../src/modules/designer/previewMessages";
import type { Item, Schemas } from "../../src/modules/designer/types";
import { fakePlatform, status, themeETag, themeFileAnswer, type FakePlatform, type Request } from "./support/fakePlatform";
import { TEST_TOKEN } from "./support/vcShell";

const STORE = "B2B-store";
const LOCKED = "PAGE_BUILDER.DESIGNER.MAIN_SECTION.LOCKED";

const HANDSHAKE: Handshake = { hash: "catalog-hash-1", capabilities: ["groups"], conditions: [], builtInTemplates: [], paletteTokens: { light: [], dark: [] } };

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

let themeFiles: Record<string, unknown>;
let themeStatus: { publication?: number; drafts: string[]; newDrafts?: string[] };
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
    .on("GET", "/api/pagebuilder/designer/theme-file", (request: Request) => themeFileAnswer(themeFiles)(request))
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
      ["site-footer", "footer"],
    ]);
    expect(designer.dirty.value).toBe(false);
    expect(designer.status.value).toEqual({ published: true, hasChanges: false });
    expect(designer.themeStatus.value).toEqual({ publication: 3, drafts: [], newDrafts: [] });
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

  it("deselects a section an undo takes away; a Duplicate of it does nothing", async () => {
    await openDatabasePage();
    const before = designer.doc.value.content.length;
    designer.addSection("hero");
    const added = designer.selection.value!;
    expect(added).toMatchObject({ kind: "section" });

    designer.undo();
    expect(designer.doc.value.content).toHaveLength(before);
    expect(designer.selection.value).toBeNull();
    designer.duplicate(added);
    expect(designer.doc.value.content).toHaveLength(before);
  });

  it("selects a block's section once an undo takes the block away", async () => {
    await openDatabasePage();
    designer.addBlock("hero-1", "hero");
    expect(designer.selection.value).toMatchObject({ kind: "block", sectionId: "hero-1" });

    designer.undo();
    expect(designer.selection.value).toEqual({ kind: "section", id: "hero-1" });
  });

  it("saves the page, the header and the theme files given in one request naming the catalog previewed", async () => {
    await openDatabasePage();
    designer.selection.value = { kind: "section", id: "hero-1" };
    designer.setField("title", "Hello there");
    designer.selection.value = { kind: "section", id: "header-1" };
    designer.setField("announcement", "Free returns");
    const rules = { path: "default/config/template_rules.json", eTag: '"r1"', content: { version: 2, conditions: [] } };

    expect(await designer.save([rules])).toEqual({});

    expect(saves()).toHaveLength(1);
    const [save] = saves();
    expect(save.query).toEqual({ storeId: STORE, catalogHash: HANDSHAKE.hash });
    const body = save.body as { page: { groupId: string; eTag: string; content: string }; themeFiles: { path: string; content: string }[] };
    expect(body.page).toMatchObject({ groupId: "g1", eTag: '"v1"' });
    expect(JSON.parse(body.page.content).content).toEqual([{ id: "hero-1", type: "hero", title: "Hello there" }]);
    expect(body.themeFiles.map((x) => x.path)).toEqual(["default/config/template_rules.json", "default/sections/header-group.json"]);
    expect(JSON.parse(body.themeFiles[1].content).content).toEqual([{ id: "header-1", type: "site-header", announcement: "Free returns" }]);
    expect(designer.dirty.value).toBe(false);
    expect(platform.to("/api/pagebuilder/theme/status").length).toBeGreaterThan(1);

    designer.selection.value = { kind: "section", id: "hero-1" };
    designer.setField("title", "Again");
    await designer.save();
    expect((saves()[1].body as { page: { eTag: string } }).page.eTag).toBe('"v2"');
  });

  it("saves an old page's settings as they were: its header and hideBreadcrumbs", async () => {
    const settings = { type: "settings", name: "About", header: "About us", hideBreadcrumbs: true };
    platform.on("GET", "/api/page-builder-pages/grouped/g1/content", { content: JSON.stringify({ ...PAGE, settings }), eTag: '"v1"' });
    await openDatabasePage();
    designer.selection.value = { kind: "section", id: "hero-1" };
    designer.setField("title", "Hello there");

    await designer.save();

    const body = saves()[0].body as { page: { content: string } };
    expect(JSON.parse(body.page.content).settings).toEqual(settings);
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

    platform.on("POST", "/api/pagebuilder/designer/save", { eTag: '"v9"' });
    expect(await designer.save()).toEqual({});
    expect((saves()[1].body as { page: { eTag: string } }).page.eTag).toBe('"v1"');
  });

  it("sends each theme file with the version it was read at, and the next Save with the version returned", async () => {
    const header = "default/sections/header-group.json";
    const footer = "default/sections/footer-group.json";
    await openDatabasePage();
    designer.selection.value = { kind: "section", id: "header-1" };
    designer.setField("announcement", "Free returns");
    platform.on("POST", "/api/pagebuilder/designer/save", { themeFiles: [{ path: header, eTag: '"h2"' }] });
    const rules = { path: "default/config/template_rules.json", eTag: '"r1"', content: { version: 2, conditions: [] } };

    expect(await designer.save([rules])).toEqual({ [header]: '"h2"' });

    const sent = (n: number) => (saves()[n].body as { themeFiles: { path: string; eTag: string }[] }).themeFiles.map((x) => [x.path, x.eTag]);
    expect(sent(0)).toEqual([
      ["default/config/template_rules.json", '"r1"'],
      [header, themeETag(header)],
    ]);

    const footerId = designer.doc.value.content.find((x) => x.__group === "footer")!.id;
    designer.selection.value = { kind: "section", id: "header-1" };
    designer.setField("announcement", "Free returns, always");
    designer.doc.value = { ...designer.doc.value, content: designer.doc.value.content.filter((x) => x.id !== footerId) };
    await designer.save();
    expect(sent(1)).toEqual([
      [header, '"h2"'],
      [footer, themeETag(footer, false)],
    ]);
  });

  it("keeps the theme files' versions when someone else saved one meanwhile (412): opening again reads them anew", async () => {
    const header = "default/sections/header-group.json";
    await openDatabasePage();
    platform.on("POST", "/api/pagebuilder/designer/save", status(412, "A theme file was saved by someone else since it was read."));
    designer.selection.value = { kind: "section", id: "header-1" };
    designer.setField("announcement", "Mine");

    await expect(designer.save()).rejects.toMatchObject({ status: 412 });
    expect(designer.dirty.value).toBe(true);

    themeFiles[header] = { ...HEADER, content: [{ ...HEADER.content[0], announcement: "Theirs" }] };
    platform.on("GET", "/api/pagebuilder/designer/theme-file", (request: Request) =>
      request.query.path === header ? { content: JSON.stringify(themeFiles[header]), eTag: '"theirs"' } : themeFileAnswer(themeFiles)(request),
    );
    platform.on("POST", "/api/pagebuilder/designer/save", {});
    await openDatabasePage();
    expect(section("header-1").announcement).toBe("Theirs");
    designer.selection.value = { kind: "section", id: "header-1" };
    designer.setField("announcement", "Mine again");
    await designer.save();
    expect((saves()[1].body as { themeFiles: { eTag: string }[] }).themeFiles.map((x) => x.eTag)).toEqual(['"theirs"']);
  });

  it("sends a template with the version it was read at; a template the theme lacks with the version of none", async () => {
    const template = "default/templates/product.json";
    await openTemplate();
    designer.selection.value = { kind: "section", id: "hero-t" };
    designer.setField("title", "Hi");
    await designer.save();
    expect((saves()[0].body as { themeFiles: { path: string; eTag: string }[] }).themeFiles[0]).toMatchObject({ path: template, eTag: themeETag(template) });

    delete themeFiles[template];
    await openTemplate();
    designer.doc.value = { ...designer.doc.value, content: [...designer.doc.value.content, { id: "hero-new", type: "hero", title: "New" }] };
    await designer.save();
    expect((saves()[1].body as { themeFiles: { path: string; eTag: string }[] }).themeFiles[0]).toMatchObject({ path: template, eTag: themeETag(template, false) });
  });

  it("refuses to save a database page read without its version (428), sending nothing", async () => {
    platform.on("GET", "/api/page-builder-pages/grouped/g1/content", { content: JSON.stringify(PAGE) });
    await openDatabasePage();
    designer.selection.value = { kind: "section", id: "hero-1" };
    designer.setField("title", "Mine");

    await expect(designer.save()).rejects.toMatchObject({ status: 428 });
    expect(saves()).toHaveLength(0);
  });

  it("holds theme files back until the preview names its catalog, sending nothing; the page alone goes", async () => {
    await openDatabasePage();
    designer.handshake.value = undefined;
    designer.selection.value = { kind: "section", id: "header-1" };
    designer.setField("announcement", "Free returns");

    await expect(designer.save()).rejects.toBeInstanceOf(CatalogPendingError);
    expect(saves()).toHaveLength(0);
    expect(designer.dirty.value).toBe(true);

    designer.undo();
    designer.selection.value = { kind: "section", id: "hero-1" };
    designer.setField("title", "Mine");
    expect(await designer.save()).toEqual({});
    expect(saves()[0].query).toEqual({ storeId: STORE });
  });

  it("sends no theme file without the catalog's hash, as a new template's first Save", async () => {
    const template = { path: "default/templates/product.big.json", eTag: '"t0"', content: { settings: {}, content: [] } };

    await expect(api.saveDesigner(STORE, { themeFiles: [template] })).rejects.toBeInstanceOf(CatalogPendingError);
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
    // The Angular designer's theme settings are not the Vue designer's to discard.
    expect(designer.discardable.value).toEqual({ page: true, theme: ["rules", "header", "brand"] });

    await designer.discardDrafts({ page: true, theme: ["header", "footer"] });

    const discards = platform.to("/api/pagebuilder/designer/discard", "POST");
    expect(discards).toHaveLength(1);
    expect(discards[0].query).toEqual({ storeId: STORE });
    expect(discards[0].body).toEqual({ page: { groupId: "g1" }, themeFiles: ["default/sections/header-group.json"] });
  });

  it("does not offer a theme draft with nothing live: it is the file's only copy", async () => {
    pageStatus = { published: true, hasChanges: true };
    themeStatus = { publication: 3, drafts: ["sections/header-group.json", "config/brand.json"], newDrafts: ["config/brand.json"] };
    await openDatabasePage();

    expect(designer.discardable.value).toEqual({ page: true, theme: ["header"] });
  });

  it("does not offer a template with nothing live to go back to", async () => {
    themeStatus = { publication: 3, drafts: ["templates/product.json"], newDrafts: ["templates/product.json"] };
    await openTemplate();

    expect(designer.status.value.hasChanges).toBe(true);
    expect(designer.discardable.value.page).toBe(false);
  });

  it("offers a template with a live version to go back to", async () => {
    themeStatus = { publication: 3, drafts: ["templates/product.json"], newDrafts: [] };
    await openTemplate();

    expect(designer.discardable.value.page).toBe(true);
  });

  it("never discards a theme draft not chosen: a page alone leaves the store's theme drafts", async () => {
    pageStatus = { published: true, hasChanges: true };
    themeStatus = { publication: 3, drafts: ["sections/header-group.json", "config/settings_data.json"] };
    await openDatabasePage();

    await designer.discardDrafts({ page: true, theme: [] });
    expect(platform.to("/api/pagebuilder/designer/discard")[0].body).toEqual({ page: { groupId: "g1" }, themeFiles: [] });

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

    expect(designer.groupsPreview.value?.header).toEqual(HEADER.content);
    expect(designer.groupsShown.value).toBe(false);
    expect(designer.isLocked("header-1")).toBe(true);
    expect(designer.isLocked("hero-1")).toBe(false);
    expect(designer.setText("header-1", ["announcement"], "Free returns")).toBeUndefined();
    expect(section("header-1").announcement).toBe("Free shipping");
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

  const HELLO = { "en-US": "Hello", "de-DE": "Hallo" };
  const copiedHero = { type: "section" as const, content: { id: "hero-t", type: "hero", title: HELLO, blocks: [{ id: "b1", type: "link", label: HELLO }] } };

  it("pastes a template's section into a page with the text of the language edited, not the dictionary", async () => {
    await openDatabasePage();
    designer.selection.value = { kind: "section", id: "hero-1" };

    expect(designer.paste(copiedHero)).toBeUndefined();

    const pasted = designer.selectedItem.value!;
    expect(pasted.__group).toBeUndefined();
    expect(pasted).toMatchObject({ type: "hero", title: "Hallo", blocks: [{ type: "link", label: "Hallo" }] });
  });

  it("keeps the dictionary of a section pasted into a template or the header, which keep texts per language", async () => {
    await openDatabasePage();
    designer.selection.value = { kind: "section", id: "header-1" };
    const announcement = { type: "section" as const, content: { id: "a1", type: "site-header", announcement: HELLO } };
    designer.paste(announcement);
    expect(designer.selectedItem.value).toMatchObject({ __group: "header", announcement: HELLO });

    await openTemplate();
    designer.selection.value = { kind: "section", id: "hero-t" };
    designer.paste(copiedHero);
    expect(designer.selectedItem.value).toMatchObject({ title: HELLO });
  });

  it("never pastes a section for the header or footer only among the page's own", async () => {
    await openDatabasePage();
    designer.selection.value = { kind: "section", id: "hero-1" };
    const before = designer.doc.value.content.length;

    expect(designer.paste({ type: "section", content: { id: "h2", type: "site-header" } })).toBe("PAGE_BUILDER.DESIGNER.CLIPBOARD.GROUP_ONLY");
    expect(designer.doc.value.content).toHaveLength(before);
  });

  it("offers, duplicates and pastes no second section of a type the schema limits to one", async () => {
    await openDatabasePage();
    designer.schemas.value = { ...SCHEMAS, sections: { ...SCHEMAS.sections, "site-header": { ...SCHEMAS.sections["site-header"], limit: 1 } } };
    const before = designer.doc.value.content.length;

    expect(designer.groupSectionTypes("header").map((x) => x.type)).not.toContain("site-header");
    designer.duplicate({ kind: "section", id: "header-1" });
    designer.selection.value = { kind: "section", id: "header-1" };
    expect(designer.paste({ type: "section", content: { id: "h2", type: "site-header" } })).toBe("PAGE_BUILDER.DESIGNER.CLIPBOARD.LIMIT");
    expect(designer.doc.value.content).toHaveLength(before);

    designer.remove({ kind: "section", id: "header-1" });
    expect(designer.groupSectionTypes("header").map((x) => x.type)).toContain("site-header");
  });

  it("pastes a header section the header does not take into the page's own sections with the language's text", async () => {
    await openDatabasePage();
    designer.selection.value = { kind: "section", id: "header-1" };

    designer.paste(copiedHero);

    expect(designer.selectedItem.value).toMatchObject({ type: "hero", title: "Hallo" });
    expect(designer.selectedItem.value!.__group).toBeUndefined();
  });

  it("pastes a block into a page's section with the language's text, and into the header's with the dictionary", async () => {
    await openDatabasePage();
    designer.schemas.value = { ...SCHEMAS, sections: { ...SCHEMAS.sections, hero: { ...SCHEMAS.sections.hero, blocks: ["link"] }, "site-header": { ...SCHEMAS.sections["site-header"], blocks: ["link"] } } };
    const link = { type: "block" as const, content: { id: "b1", type: "link", label: HELLO } };

    designer.selection.value = { kind: "section", id: "hero-1" };
    designer.paste(link);
    expect(designer.selectedItem.value).toMatchObject({ type: "link", label: "Hallo" });

    designer.selection.value = { kind: "section", id: "header-1" };
    designer.paste(link);
    expect(designer.selectedItem.value).toMatchObject({ type: "link", label: HELLO });
  });

  it("pastes into a shared component with the language's text", async () => {
    platform
      .on("GET", "/api/page-builder-shared-components/c1", { id: "c1", name: "Promo" })
      .on("GET", "/api/page-builder-shared-components/c1/content", { settings: { type: "settings" }, content: [{ id: "s1", type: "hero", title: "Promo" }] });
    await designer.open({ sharedComponentId: "c1", storeId: STORE });
    previewLoaded();
    designer.selection.value = { kind: "section", id: "s1" };

    designer.paste(copiedHero);

    expect(designer.selectedItem.value).toMatchObject({ title: "Hallo" });
  });

  it("makes a shared component and detaches one with the language's texts, never a dictionary", async () => {
    platform
      .on("POST", "/api/page-builder-shared-components", { id: "c2", name: "Saved" })
      .on("GET", "/api/page-builder-shared-components/c2", { id: "c2", name: "Saved" })
      .on("GET", "/api/page-builder-shared-components/c2/content", { settings: { type: "settings" }, content: [{ id: "s1", type: "hero", title: HELLO }] });
    platform.on("GET", "/api/page-builder-pages/grouped/g1/content", {
      content: JSON.stringify({ ...PAGE, content: [{ id: "hero-1", type: "hero", title: HELLO }] }),
      eTag: '"v1"',
    });
    await openDatabasePage();
    designer.checked.value = ["hero-1"];

    expect(await designer.saveCheckedAsShared("Saved")).toBeUndefined();
    const created = platform.to("/api/page-builder-shared-components", "POST")[0].body as { content: { content: Item[] } };
    expect(created.content.content).toEqual([{ id: "hero-1", type: "hero", title: "Hallo" }]);

    await designer.detach(designer.selection.value!.kind === "section" ? designer.selection.value!.id : "");
    expect(designer.selectedItem.value).toMatchObject({ type: "hero", title: "Hallo" });
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

  it("keeps the last shown main section: a hidden one left would show none", async () => {
    themeFiles["default/templates/product.json"] = {
      ...PRODUCT_TEMPLATE,
      content: [...PRODUCT_TEMPLATE.content.map((x) => (x.id === "info-1" ? { ...x, hidden: true } : x)), { id: "info-2", type: "product-info" }],
    };
    await openTemplate();

    expect(designer.isMainKept("info-2")).toBe(true);
    expect(designer.remove({ kind: "section", id: "info-2" })).toBe(LOCKED);
    designer.checked.value = ["info-2"];
    expect(designer.removeChecked()).toBe(LOCKED);
    expect(designer.remove({ kind: "section", id: "info-1" })).toBeUndefined();
    expect(designer.mainSection.value).toEqual({ type: "product-info", state: "shown" });
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
  it("reads a theme file with its version, one the theme lacks as none, and keeps any failure's status and body", async () => {
    expect(await api.loadThemeFile(STORE, "default/config/brand.json")).toEqual({ content: undefined, eTag: themeETag("default/config/brand.json", false) });
    expect(await api.loadThemeFile(STORE, "default/sections/header-group.json")).toEqual({ content: HEADER, eTag: themeETag("default/sections/header-group.json") });
    expect(platform.to("/api/pagebuilder/designer/theme-file")[0].query).toEqual({ storeId: STORE, path: "default/config/brand.json" });
    platform.on("GET", "/api/pagebuilder/catalog", status(404));
    expect(await api.hasCatalog(STORE, "catalog-hash-1")).toBe(false);

    platform.on("GET", "/api/pagebuilder/designer/theme-file", status(403, "Forbidden"));
    await expect(api.loadThemeFile(STORE, "default/config/brand.json")).rejects.toMatchObject({ status: 403, body: "Forbidden" });
  });

  it("sends the brand and palettes with the versions they were read at, and after a save with the versions it returned", async () => {
    themeFiles["default/config/brand.json"] = { logoUrl: "/b.svg" };
    const look = scope.run(() => useThemeLook())!;
    await look.load(STORE, "default");
    look.setBrand("logoUrl", "/new.svg");
    look.setColor("light", "color_primary_500", "#000000");

    const pending = look.pending();
    expect(pending.map((x) => [x.file.path, x.file.eTag])).toEqual([
      ["default/config/brand.json", themeETag("default/config/brand.json")],
      ["default/config/palette.json", themeETag("default/config/palette.json", false)],
    ]);

    for (const part of pending) part.saved(`"${part.part}-v2"`);
    look.setBrand("logoUrl", "/newer.svg");
    expect(look.pending().map((x) => x.file.eTag)).toEqual(['"brand-v2"']);
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
    expect(platform.to("/api/pagebuilder/designer/theme-file").map((x) => x.query.path)).not.toContain("default/config/settings_data.json");

    platform.on("GET", "/api/pagebuilder/designer/theme-file", status(403, "Forbidden"));
    await look.load(STORE, "default");
    expect(look.loaded.value).toBe(false);
    expect(look.loadError.value).toBeInstanceOf(ApiError);
    expect((look.loadError.value as ApiError).status).toBe(403);
  });
});

describe("api: a file page's preview link", () => {
  it("names the page by the UTF-8 bytes of its id, as the server encodes it", () => {
    const storefront = { url: "https://shop.test", previewPath: "/designer-preview" };
    const id = (path: string, draft = false) =>
      new URL(api.filePreviewUrl({ storeId: STORE, theme: "default", type: "pages", path }, false, storefront, draft)!).searchParams.get("pageId");
    const server = (raw: string) => Buffer.from(raw, "utf8").toString("base64").replace(/=/g, "-");

    expect(id("о-нас.page")).toBe(server(`${STORE}::pages::о-нас.page`));
    expect(id("über-uns.page", true)).toBe(server(`${STORE}::pages::über-uns.page-draft`));
    expect(id("/blog\\post.page")).toBe(server(`${STORE}::pages::blog/post.page`));
  });
});
