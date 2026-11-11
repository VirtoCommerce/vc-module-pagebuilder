import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h } from "vue";
import { flushPromises, shallowMount, type VueWrapper } from "@vue/test-utils";
import { createMemoryHistory, createRouter } from "vue-router";
import type { IBladeToolbar } from "@vc-shell/framework";
import { VcBlade } from "@vc-shell/framework/ui";
import PageBuilderDesigner from "../../src/modules/pages/PageBuilderDesigner.vue";
import AssetsLibraryContent from "../../src/modules/asset-library/components/AssetsLibraryContent.vue";
import DiscardDialog from "../../src/modules/designer/components/DiscardDialog.vue";
import SectionTree from "../../src/modules/designer/components/SectionTree.vue";
import VersionHistory from "../../src/modules/designer/components/VersionHistory.vue";
import { pagesChanged, useUrlParams } from "../../src/modules/page-builder/composables";
import { PickAssetKey } from "../../src/modules/designer/types";
import { toClipboard } from "../../src/modules/designer/clipboard";
import { BladeStackKey, blade, notification } from "./support/vcShell";
import { fakePlatform, status, themeFileAnswer, type FakePlatform } from "./support/fakePlatform";

const STORE = "B2B-store";
const GROUP = "/api/page-builder-pages/grouped/g1";
const PAGE = { settings: { type: "settings" }, content: [{ id: "hero-1", type: "hero", title: "Welcome" }] };

beforeEach(() => {
  vi.stubGlobal("defineBlade", () => undefined);
  fakePlatform().install();
});

async function routerAt(address: string) {
  const router = createRouter({ history: createMemoryHistory(), routes: [{ path: "/:any(.*)*", component: { render: () => null } }] });
  await router.push(address);
  return router;
}

const toolbarItem = (wrapper: VueWrapper, id: string) =>
  (wrapper.findComponent(VcBlade).props("toolbarItems") as IBladeToolbar[]).find((x) => x.id === id)!;

describe("opening the designer", () => {
  it("says the page could not be loaded when its group cannot be read, and stops loading", async () => {
    blade.param.value = "g1";
    fakePlatform().on("GET", GROUP, status(403)).install();

    const wrapper = shallowMount(PageBuilderDesigner, { global: { stubs: { VcBlade: false } } });
    await flushPromises();

    expect(wrapper.find(".pd-message").text()).toContain("couldn't be loaded");
    expect(wrapper.findComponent(VcBlade).props("loading")).toBe(false);
  });

  it("has nothing to save or discard when the page is gone", async () => {
    blade.param.value = "g1";
    fakePlatform().on("GET", GROUP, status(404)).install();

    const wrapper = shallowMount(PageBuilderDesigner, { global: { stubs: { VcBlade: false } } });
    await flushPromises();

    expect(wrapper.find(".pd-message").exists()).toBe(true);
    expect(wrapper.findComponent(VcBlade).props("modified")).toBe(false);
    expect(toolbarItem(wrapper, "save").disabled).toBe(true);
    expect(toolbarItem(wrapper, "discard").isVisible).toBe(false);
  });
});

describe("reloading a designer opened by query", () => {
  it("opens the shared component the address names, and keeps it in the blade's query for the next reload", async () => {
    const platform = fakePlatform()
      .on("GET", "/api/page-builder-shared-components/c1", { id: "c1", storeId: STORE, name: "Promo", usageCount: 1 })
      .on("GET", "/api/page-builder-shared-components/c1/content", "[]")
      .install();
    const stack = { updateBladeQuery: vi.fn() };
    const router = await routerAt(`/page-builder-draft/page-builder-designer?storeId=${STORE}&sharedComponentId=c1&sort=name`);

    const wrapper = shallowMount(PageBuilderDesigner, {
      global: { plugins: [router], provide: { [BladeStackKey]: stack }, stubs: { VcBlade: false } },
    });
    await flushPromises();

    expect(platform.to("/api/page-builder-shared-components/c1")).toHaveLength(1);
    expect(stack.updateBladeQuery).toHaveBeenCalledWith("blade", { storeId: STORE, sharedComponentId: "c1" });
    expect(wrapper.find(".pd").exists()).toBe(true);
  });

  it("reads a file page from the address the shell is still navigating to, as on Back", async () => {
    const stack = { updateBladeQuery: vi.fn() };
    const router = await routerAt("/page-builder-draft");
    // The browser's address has moved on, and the router has not finished its navigation there yet.
    router.options.history.push(`/page-builder-draft/page-builder-designer?storeId=${STORE}&path=about.page&type=pages&cultureName=de-DE&sort=name`);

    shallowMount(PageBuilderDesigner, { global: { plugins: [router], provide: { [BladeStackKey]: stack }, stubs: { VcBlade: false } } });
    await flushPromises();

    expect(stack.updateBladeQuery).toHaveBeenCalledWith("blade", { storeId: STORE, path: "about.page", type: "pages", cultureName: "de-DE" });
  });

  it("says the page could not be loaded when the address names nothing to open", async () => {
    const router = await routerAt("/page-builder-draft/page-builder-designer");

    const wrapper = shallowMount(PageBuilderDesigner, { global: { plugins: [router], stubs: { VcBlade: false } } });
    await flushPromises();

    expect(wrapper.find(".pd-message").text()).toContain("couldn't be loaded");
  });
});

describe("full view", () => {
  it("lets go of the window and gives the blade its header back when the page turns out not to be designable", async () => {
    localStorage.setItem("vc-page-designer-full-view", "true");
    blade.param.value = "g1";
    let readGroup!: (group: unknown) => void;
    fakePlatform()
      .on("GET", GROUP, () => new Promise((resolve) => (readGroup = resolve)))
      .install();

    const wrapper = shallowMount(PageBuilderDesigner, { global: { stubs: { VcBlade: false } } });
    await flushPromises();
    const bladeElement = wrapper.find(".vc-blade");
    expect(bladeElement.classes()).toContain("pd-full-view");
    expect((bladeElement.element as HTMLElement).style.position).toBe("fixed");

    readGroup({ id: "g1", storeId: STORE, name: "Old", status: "Archived" });
    await flushPromises();

    expect(wrapper.find(".pd-message").exists()).toBe(true);
    expect(bladeElement.classes()).not.toContain("pd-full-view");
    expect((bladeElement.element as HTMLElement).style.position).toBe("");
    expect(toolbarItem(wrapper, "full-view").isVisible).toBe(false);
    expect(localStorage.getItem("vc-page-designer-full-view")).toBe("true");
  });
});

describe("the preview", () => {
  const NO_STOREFRONT = "the site has no storefront URL";

  function openPageOn(storefrontUrl: string | null) {
    fakePlatform()
      .on("GET", `/api/stores/${STORE}`, { id: STORE, dynamicProperties: [{ name: "DefaultThemeName", values: [{ value: "default" }] }] })
      .on("GET", GROUP, { id: "g1", storeId: STORE, name: "About", status: "Published" })
      .on("GET", `${GROUP}/content`, { content: JSON.stringify(PAGE), eTag: '"v1"' })
      .on("GET", "/api/page-builder-pages/grouped/publish-status/g1", { published: true, hasChanges: true })
      .on("GET", "/api/pagebuilder/designer/theme-file", themeFileAnswer({}))
      .on("GET", "/api/pagebuilder/theme/status", { publication: 3, drafts: [] })
      .on("GET", "/api/pagebuilder/designer/storefront", { url: storefrontUrl, previewPath: "/designer-preview" })
      .install();
    blade.param.value = "g1";
  }

  it("says the site has no storefront URL when it has none", async () => {
    openPageOn(null);
    const wrapper = shallowMount(PageBuilderDesigner, { global: { plugins: [await routerAt("/")], stubs: { VcBlade: false } } });
    await flushPromises();

    expect(wrapper.find("iframe").exists()).toBe(false);
    expect(wrapper.text()).toContain(NO_STOREFRONT);
  });

  it("leaves the no-storefront message out once the storefront's preview has connected", async () => {
    openPageOn("https://shop.test");
    const wrapper = shallowMount(PageBuilderDesigner, {
      attachTo: document.body,
      global: { plugins: [await routerAt("/")], stubs: { VcBlade: false } },
    });
    try {
      await flushPromises();
      const frame = wrapper.find("iframe").element as HTMLIFrameElement;
      const loaded = {
        protocol: 2,
        hash: "0badc0de",
        sections: {},
        blocks: {},
        shared: {},
        objects: {},
        paletteTokens: { light: [], dark: [] },
        capabilities: [],
        conditions: [],
      };

      const data = { source: "preview", type: "loaded", data: loaded };
      window.dispatchEvent(new MessageEvent("message", { source: frame.contentWindow, origin: "https://shop.test", data }));
      await flushPromises();

      expect(wrapper.find(".pd__frame-wait").exists()).toBe(false);
      expect(wrapper.text()).not.toContain(NO_STOREFRONT);
    } finally {
      wrapper.unmount();
    }
  });
});

describe("telling open lists the page changed", () => {
  let platform: FakePlatform;

  async function openPage() {
    platform = fakePlatform()
      .on("GET", `/api/stores/${STORE}`, { id: STORE, dynamicProperties: [{ name: "DefaultThemeName", values: [{ value: "default" }] }] })
      .on("GET", GROUP, { id: "g1", storeId: STORE, name: "About", status: "Published" })
      .on("GET", `${GROUP}/content`, { content: JSON.stringify(PAGE), eTag: '"v1"' })
      .on("GET", "/api/page-builder-pages/grouped/publish-status/g1", { published: true, hasChanges: true })
      .on("GET", "/api/pagebuilder/designer/theme-file", themeFileAnswer({}))
      .on("GET", "/api/pagebuilder/theme/status", { publication: 3, drafts: [] })
      .on("POST", "/api/pagebuilder/designer/discard", {})
      .on("GET", "/api/pagebuilder/history/status", { status: "enabled" })
      .on("POST", "/api/pagebuilder/history/v7/restore", "")
      .install();
    blade.param.value = "g1";
    const wrapper = shallowMount(PageBuilderDesigner, { global: { plugins: [await routerAt("/")], stubs: { VcBlade: false } } });
    await flushPromises();
    return wrapper;
  }

  it("does so when the page's draft is discarded", async () => {
    const wrapper = await openPage();
    const before = pagesChanged.value;

    await wrapper.find('[role="toolbar"] [data-test-id="discard"]').trigger("click");
    await flushPromises();
    wrapper.findComponent(DiscardDialog).vm.$emit("discard", { page: true, theme: [] });
    await flushPromises();

    expect(platform.to("/api/pagebuilder/designer/discard", "POST")).toHaveLength(1);
    expect(pagesChanged.value).toBe(before + 1);
  });

  it("does so when a site version that holds the page is restored", async () => {
    const wrapper = await openPage();
    const before = pagesChanged.value;

    await wrapper.find('[role="toolbar"] [data-test-id="history"]').trigger("click");
    await flushPromises();
    wrapper.findComponent(VersionHistory).vm.$emit("restore", { id: "v7", label: "7", items: ["page:g1"] });
    await flushPromises();

    expect(platform.to("/api/pagebuilder/history/v7/restore", "POST")).toHaveLength(1);
    expect(pagesChanged.value).toBe(before + 1);
  });
});

describe("the store the lists work on", () => {
  afterEach(() => history.replaceState(null, "", "/"));

  it("goes back to the store in the address, and the lists read again, once the designer of another store's page closes", async () => {
    history.replaceState(null, "", "/?storeId=Other-store");
    const storeContext = useUrlParams();
    storeContext.initUrlParams();
    fakePlatform()
      .on("GET", `/api/stores/${STORE}`, { id: STORE, dynamicProperties: [{ name: "DefaultThemeName", values: [{ value: "default" }] }] })
      .on("GET", GROUP, { id: "g1", storeId: STORE, name: "About", status: "Published" })
      .on("GET", `${GROUP}/content`, { content: JSON.stringify(PAGE), eTag: '"v1"' })
      .on("GET", "/api/page-builder-pages/grouped/publish-status/g1", { published: true, hasChanges: true })
      .on("GET", "/api/pagebuilder/designer/theme-file", themeFileAnswer({}))
      .on("GET", "/api/pagebuilder/theme/status", { publication: 3, drafts: [] })
      .install();
    blade.param.value = "g1";
    const wrapper = shallowMount(PageBuilderDesigner, { global: { plugins: [await routerAt("/")], stubs: { VcBlade: false } } });
    await flushPromises();
    expect(storeContext.storeId.value).toBe(STORE);
    const before = pagesChanged.value;

    wrapper.unmount();

    expect(storeContext.storeId.value).toBe("Other-store");
    expect(pagesChanged.value).toBe(before + 1);
  });
});

describe("saving sections as a shared component", () => {
  it("creates one component when Save is clicked again while the first is on its way", async () => {
    let created!: (component: unknown) => void;
    const platform = fakePlatform()
      .on("GET", `/api/stores/${STORE}`, { id: STORE, dynamicProperties: [{ name: "DefaultThemeName", values: [{ value: "default" }] }] })
      .on("GET", GROUP, { id: "g1", storeId: STORE, name: "About", status: "Published" })
      .on("GET", `${GROUP}/content`, { content: JSON.stringify(PAGE), eTag: '"v1"' })
      .on("GET", "/api/page-builder-pages/grouped/publish-status/g1", { published: true, hasChanges: true })
      .on("GET", "/api/pagebuilder/designer/theme-file", themeFileAnswer({}))
      .on("GET", "/api/pagebuilder/theme/status", { publication: 3, drafts: [] })
      .on("POST", "/api/page-builder-shared-components", () => new Promise((resolve) => (created = resolve)))
      .install();
    blade.param.value = "g1";
    const wrapper = shallowMount(PageBuilderDesigner, {
      global: { plugins: [await routerAt("/")], stubs: { VcBlade: false, VcPopup: false, VcInput: false, VcButton: false } },
    });
    await flushPromises();
    (wrapper.vm as unknown as { designer: { checked: { value: string[] } } }).designer.checked.value = ["hero-1"];
    wrapper.findComponent(SectionTree).vm.$emit("save-shared");
    await flushPromises();
    const dialog = wrapper.find('[role="dialog"]');
    await dialog.find("input").setValue("Promo");
    const save = dialog.findAll("button").find((x) => x.text() === "Save")!;

    await save.trigger("click");
    await save.trigger("click");
    created({ id: "c1", storeId: STORE, name: "Promo" });
    await flushPromises();

    expect(platform.to("/api/page-builder-shared-components", "POST")).toHaveLength(1);
  });
});

describe("picking an asset", () => {
  it("opens the asset library on the types the field asked for", async () => {
    blade.param.value = "g1";
    fakePlatform().on("GET", GROUP, status(403)).install();
    const wrapper = shallowMount(PageBuilderDesigner, { global: { stubs: { VcBlade: false, VcPopup: false } } });
    await flushPromises();
    // Fields reach the picker only through this injection.
    const pickAsset = (wrapper.vm.$ as unknown as { provides: Record<symbol, (types?: string[]) => Promise<string | undefined>> }).provides[PickAssetKey as symbol];

    void pickAsset([".svg", "image/png"]);
    await flushPromises();

    expect(wrapper.findComponent(AssetsLibraryContent).props()).toMatchObject({ pickable: true, accept: [".svg", "image/png"] });
  });
});

describe("pasting from the clipboard", () => {
  afterEach(() => {
    delete (navigator as { clipboard?: unknown }).clipboard;
  });

  async function pasteWith(readText: () => Promise<string>) {
    Object.defineProperty(navigator, "clipboard", { value: { readText }, configurable: true });
    fakePlatform()
      .on("GET", `/api/stores/${STORE}`, { id: STORE, dynamicProperties: [{ name: "DefaultThemeName", values: [{ value: "default" }] }] })
      .on("GET", GROUP, { id: "g1", storeId: STORE, name: "About", status: "Published" })
      .on("GET", `${GROUP}/content`, { content: JSON.stringify(PAGE), eTag: '"v1"' })
      .on("GET", "/api/page-builder-pages/grouped/publish-status/g1", { published: true, hasChanges: true })
      .on("GET", "/api/pagebuilder/designer/theme-file", themeFileAnswer({}))
      .on("GET", "/api/pagebuilder/theme/status", { publication: 3, drafts: [] })
      .install();
    blade.param.value = "g1";
    const wrapper = shallowMount(PageBuilderDesigner, {
      global: { plugins: [await routerAt("/")], stubs: { VcBlade: false, VcPopup: false, VcTextarea: false } },
    });
    await flushPromises();
    wrapper.findComponent(SectionTree).vm.$emit("paste");
    await flushPromises();
    return wrapper;
  }

  const pasteDialog = (wrapper: VueWrapper) => wrapper.find('[role="dialog"][aria-label="Paste a section or block"]');

  it("asks for the copied content when the browser does not let the page read the clipboard", async () => {
    const wrapper = await pasteWith(() => Promise.reject(new DOMException("denied", "NotAllowedError")));

    expect(pasteDialog(wrapper).exists()).toBe(true);
    expect(notification.error).not.toHaveBeenCalled();
  });

  it("takes a copied section far longer than 1024 characters in the paste dialog", async () => {
    const wrapper = await pasteWith(() => Promise.reject(new DOMException("denied", "NotAllowedError")));

    expect(Number(pasteDialog(wrapper).find("textarea").attributes("maxlength") ?? Infinity)).toBeGreaterThanOrEqual(100_000);
  });

  it.each([
    ["text that is not a copied item", "hello", "The clipboard doesn't hold a copied section or block."],
    ["a copied block with no section selected", toClipboard("block", { id: "b1", type: "button" }), "Select a section to paste a block into."],
  ])("says why it refuses %s, without asking for the clipboard again", async (_, text, message) => {
    const wrapper = await pasteWith(() => Promise.resolve(text));

    expect(notification.error).toHaveBeenCalledWith(message);
    expect(pasteDialog(wrapper).exists()).toBe(false);
  });
});

describe("the template switcher", () => {
  const SEARCH = `/api/content/themes/${STORE}/search`;
  // Renders the menu as well as the trigger, as an open VcDropdown does.
  const VcDropdown = defineComponent({
    name: "VcDropdown",
    setup: (_, { slots }) => () => h("div", [slots.trigger?.({ isActive: true, toggle: () => undefined }), slots["items-container"]?.({ close: () => undefined })]),
  });

  async function openWithTemplates(answer: unknown) {
    fakePlatform()
      .on("GET", `/api/stores/${STORE}`, { id: STORE, dynamicProperties: [{ name: "DefaultThemeName", values: [{ value: "default" }] }] })
      .on("GET", GROUP, { id: "g1", storeId: STORE, name: "About", status: "Published" })
      .on("GET", `${GROUP}/content`, { content: JSON.stringify(PAGE), eTag: '"v1"' })
      .on("GET", "/api/page-builder-pages/grouped/publish-status/g1", { published: true, hasChanges: true })
      .on("GET", "/api/pagebuilder/designer/theme-file", themeFileAnswer({}))
      .on("GET", "/api/pagebuilder/theme/status", { publication: 3, drafts: [] })
      .on("GET", SEARCH, answer)
      .install();
    blade.param.value = "g1";
    const wrapper = shallowMount(PageBuilderDesigner, {
      global: { plugins: [await routerAt("/")], stubs: { VcBlade: false, TemplateSwitcher: false, VcDropdown } },
    });
    await flushPromises();
    return wrapper;
  }

  const items = (wrapper: VueWrapper) => wrapper.findAll('.pd-switch [role="menuitemradio"]').map((x) => x.text());

  it("lists the theme's alternate templates next to the default ones, with no notice", async () => {
    const wrapper = await openWithTemplates([{ name: "product.printers.json" }]);

    expect(items(wrapper)).toContain("Printers");
    expect(wrapper.find(".pd-switch__notice").exists()).toBe(false);
  });

  it.each([
    [403, "Only the default templates are shown: listing the theme's templates needs permission to read the site content."],
    [500, "The theme's templates couldn't be listed."],
  ])("says why it lists only the default templates when the theme's templates answer %i", async (code, message) => {
    const wrapper = await openWithTemplates(status(code));

    expect(wrapper.find(".pd-switch__notice").text()).toBe(message);
    expect(items(wrapper).filter((x) => x === "Default")).toHaveLength(3);
  });
});
