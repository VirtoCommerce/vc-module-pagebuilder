import { beforeEach, describe, expect, it, vi } from "vitest";
import { nextTick } from "vue";
import { flushPromises, mount } from "@vue/test-utils";
import SitePagesTab from "../../src/modules/widgets/SitePagesTab.vue";

const tab = vi.hoisted(() => ({ designPage: vi.fn(), loadPages: vi.fn(), page: {} as Record<string, unknown> }));

vi.mock("@vc-shell/framework", async (importOriginal) => ({
  ...(await importOriginal<typeof import("./support/vcShell")>()),
  formatDateRelative: () => "today",
}));

vi.mock("@vc-shell/framework/ui", async (importOriginal) => {
  const ui = await importOriginal<typeof import("./support/vcShellUi")>();
  const { defineComponent: define, h: render } = await import("vue");
  const box = (name: string) => define({ name, setup: () => () => render("div") });
  return { ...ui, VcLoading: box("VcLoading"), VcPagination: box("VcPagination") };
});

vi.mock("../../src/modules/page-builder/composables", async () => {
  const { computed: vueComputed, ref: vueRef } = await import("vue");
  return {
    PageLifecycleFilters: { Draft: "drafts", Active: "active", Archived: "archived", Pending: "pending" },
    PageTypes: { Landing: "Landing", Multilanguage: "Multilanguage" },
    pagesChanged: vueRef(0),
    usePageBuilderList: () => ({
      items: vueComputed(() => [{ id: "g1", name: "About", permalink: "/about", type: "Landing", page: { id: "g1", status: "Draft", cultureName: "en-US", ...tab.page } }]),
      pagination: { pages: 1, currentPage: 1, skip: 0, reset: vi.fn(), goToPage: vi.fn() },
      searchQuery: vueComputed(() => ({})),
      loadPages: tab.loadPages,
      loading: vueComputed(() => false),
      storeContextStatus: vueRef("ready"),
    }),
    usePageDesigner: () => tab.designPage,
    useUrlParams: () => ({ setStoreId: vi.fn() }),
  };
});

beforeEach(() => {
  tab.designPage.mockReset();
  tab.loadPages.mockReset();
  tab.page = {};
});

describe("the site's pages tab", () => {
  it("marks a page open only while its designer blade is, not when the designer opens in another tab", async () => {
    const wrapper = mount(SitePagesTab, { props: { storeId: "B2B-store" } });
    await flushPromises();
    const row = () => wrapper.find(".site-pages__row");

    await wrapper.find(".site-pages__open").trigger("click");
    await flushPromises();

    expect(tab.designPage).toHaveBeenCalledTimes(1);
    expect(row().classes()).not.toContain("site-pages__row--open");

    const [, events] = tab.designPage.mock.calls[0] as [unknown, { onOpen: () => void; onClose: () => void }];
    events.onOpen();
    await nextTick();
    expect(row().classes()).toContain("site-pages__row--open");

    tab.loadPages.mockClear();
    events.onClose();
    await nextTick();
    expect(row().classes()).not.toContain("site-pages__row--open");
    expect(tab.loadPages).toHaveBeenCalledTimes(1);
  });

  it("names the lifecycle of a page and of the filter as the menu does", async () => {
    tab.page = { status: "Published", startDate: new Date(Date.now() + 86_400_000) };
    const wrapper = mount(SitePagesTab, { props: { storeId: "B2B-store" } });
    await flushPromises();

    expect(wrapper.find(".site-pages__status").text()).toBe("Pending");
    const options = wrapper.findComponent({ name: "VcSelect" }).props("options") as { label: string }[];
    expect(options.map((x) => x.label)).toEqual(["All Pages", "Draft", "Pending", "Active", "Archived"]);
  });
});
