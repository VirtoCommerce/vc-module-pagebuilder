import { beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h, nextTick } from "vue";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import PageBuilderPageDetails from "../../src/modules/page-builder/pages/PageBuilderPageDetails.vue";

// Replaced by a reactive ref when the framework mock loads.
const form = vi.hoisted(() => ({ modified: { value: false } }));

vi.mock("@vc-shell/framework", async (importOriginal) => {
  const shell = await importOriginal<typeof import("./support/vcShell")>();
  const { ref: vueRef } = await import("vue");
  form.modified = vueRef(false);
  return {
    ...shell,
    useBlade: () => ({ ...shell.useBlade(), options: vueRef({ storeId: "s1" }), closeSelf: vi.fn(), replaceWith: vi.fn() }),
    useBladeForm: () => ({
      canSave: vueRef(true),
      isModified: form.modified,
      setBaseline: vi.fn(),
      markReady: vi.fn(),
      formMeta: vueRef({ valid: true }),
    }),
  };
});

vi.mock("@vc-shell/framework/ui", async (importOriginal) => {
  const ui = await importOriginal<typeof import("./support/vcShellUi")>();
  const { defineComponent: define, h: render } = await import("vue");
  const box = (name: string) => define({ name, setup: (_, { slots }) => () => render("div", slots.default?.()) });
  return { ...ui, VcCard: box("VcCard"), VcCol: box("VcCol"), VcContainer: box("VcContainer"), VcSelect: box("VcSelect") };
});

vi.mock("../../src/modules/page-builder/composables/usePageBuilderDetails", async () => {
  const { computed: vueComputed, ref: vueRef } = await import("vue");
  return {
    usePageBuilderDetails: () => ({
      item: vueRef({ id: "g1", name: "Page", permalink: "/page", storeId: "s1", status: "Published" }),
      seo: vueRef({}),
      status: vueRef({ hasChanges: false }),
      isReadOnly: vueComputed(() => false),
      loading: vueComputed(() => false),
      loadGroup: vi.fn(async () => undefined),
      saveGroup: vi.fn(),
      deleteGroup: vi.fn(),
      publishGroup: vi.fn(),
      unpublishGroup: vi.fn(),
      openDraftDesigner: vi.fn(),
      downloadContent: vi.fn(),
      clonePage: vi.fn(),
      convertToMultilanguage: vi.fn(),
    }),
  };
});

vi.mock("../../src/modules/page-builder/composables/useStoreParams", async () => {
  const { ref: vueRef } = await import("vue");
  return {
    default: () => ({ storeId: vueRef("s1"), setStoreId: vi.fn(), getStoreUrl: async () => "https://shop.test", getLanguages: async () => ["en-US"] }),
  };
});
vi.mock("../../src/modules/page-builder/composables/useAiAgentContextWithStore", () => ({ default: vi.fn() }));
vi.mock("../../src/modules/page-builder/composables/usePageBuilderList", () => ({ notifyPagesChanged: vi.fn() }));
vi.mock("../../src/modules/page-builder/composables/usePermalinkUsage", () => ({
  usePermalinkUsage: () => ({ confirmPermalinkUsage: vi.fn(async () => true) }),
}));
vi.mock("../../src/modules/page-builder/components/pageStatus.vue", () => ({ default: defineComponent({ render: () => null }) }));
vi.mock("../../src/modules/page-builder/components/pageAdvancedOptions.vue", () => ({ default: defineComponent({ render: () => null }) }));

interface ToolbarItem {
  id: string;
  disabled?: boolean;
}

const Field = defineComponent({
  name: "Field",
  setup: (_, { slots }) => () => h("div", slots.default?.({ errors: [], errorMessage: undefined, handleChange: () => undefined })),
});

const toolbarItem = (wrapper: VueWrapper, id: string) =>
  (wrapper.findComponent({ name: "VcBlade" }).props("toolbarItems") as ToolbarItem[]).find((x) => x.id === id);

async function mountPage() {
  const wrapper = mount(PageBuilderPageDetails, { global: { stubs: { Field } } });
  await flushPromises();
  return wrapper;
}

describe("unpublishing a page from its details", () => {
  beforeEach(() => {
    vi.stubGlobal("defineBlade", () => undefined);
    form.modified.value = false;
  });

  it("is offered when the details have no unsaved edits", async () => {
    const wrapper = await mountPage();

    expect(toolbarItem(wrapper, "unpublishPage")?.disabled).toBe(false);
  });

  it("is not offered while the details have unsaved edits, which reloading the page would throw away", async () => {
    const wrapper = await mountPage();

    form.modified.value = true;
    await nextTick();

    expect(toolbarItem(wrapper, "unpublishPage")?.disabled).toBe(true);
  });
});
