import { beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises, shallowMount } from "@vue/test-utils";
import SharedComponentsBlade from "../../src/modules/shared-components/components/SharedComponentsBlade.vue";
import SharedComponentDetails from "../../src/modules/shared-components/components/SharedComponentDetails.vue";
import type { SharedComponent } from "../../src/modules/shared-components/types";
import { notification } from "./support/vcShell";

const STORE = "B2B-store";
const COMPONENT: SharedComponent = {
  id: "c1",
  storeId: STORE,
  name: "Promo",
  usageCount: 1,
  usagePages: [{ id: "g1", name: "About", status: "Published", cultureName: "en-US" }],
};

const calls = vi.hoisted(() => ({ refreshComponent: vi.fn(), reload: vi.fn(), openPage: vi.fn(), openShared: vi.fn() }));

vi.mock("@vc-shell/framework", async (importOriginal) => {
  const shell = await importOriginal<typeof import("./support/vcShell")>();
  return {
    ...shell,
    useBlade: () => ({ ...shell.useBlade(), exposeToChildren: vi.fn() }),
    focusIfLoose: vi.fn(),
    focusFallbackTarget: () => null,
  };
});

vi.mock("../../src/modules/shared-components/composables", async (importOriginal) => {
  const original = await importOriginal<typeof import("../../src/modules/shared-components/composables")>();
  const { computed: vueComputed, ref: vueRef } = await import("vue");
  return {
    ...original,
    useSharedComponents: () => ({
      items: vueComputed(() => [COMPONENT]),
      loading: vueComputed(() => false),
      detailsLoading: vueComputed(() => false),
      loadError: vueRef(undefined),
      totalCount: vueComputed(() => 1),
      searchValue: vueRef(undefined),
      selectedComponent: vueRef(COMPONENT),
      pagination: { skip: 0, pageSize: 20, currentPage: 1, pages: 1, setPage: vi.fn(), reset: vi.fn(), goToPage: vi.fn() },
      storeId: vueComputed(() => STORE),
      storeContextStatus: vueRef("ready"),
      initialize: vi.fn(async () => undefined),
      reload: calls.reload,
      search: vi.fn(),
      retryLastLoad: vi.fn(),
      selectComponent: vi.fn(),
      refreshComponent: calls.refreshComponent,
      clearSelection: vi.fn(),
      renameComponent: vi.fn(),
      deleteComponent: vi.fn(),
    }),
  };
});

vi.mock("../../src/modules/page-builder/composables/usePageDesigner", () => ({
  usePageDesigner: () => calls.openPage,
  useSharedComponentDesigner: () => calls.openShared,
}));

vi.mock("../../src/modules/page-builder/composables/useStoreParams", () => ({
  default: () => ({ isNewPageBuilder: async () => true }),
}));

beforeEach(() => {
  for (const fn of Object.values(calls)) fn.mockReset();
  calls.reload.mockResolvedValue(undefined);
});

async function mountBlade() {
  const wrapper = shallowMount(SharedComponentsBlade, { global: { stubs: { VcBlade: false } } });
  await flushPromises();
  return { wrapper, details: wrapper.findComponent(SharedComponentDetails) };
}

describe("shared components: after a designer closes", () => {
  it("reports a component it could not read again", async () => {
    calls.refreshComponent.mockRejectedValue(new Error("The component was deleted."));
    const { details } = await mountBlade();

    details.vm.$emit("edit-content", COMPONENT);
    const [store, id, onClose] = calls.openShared.mock.calls[0] as [string, string, () => void];
    onClose();
    await flushPromises();

    expect([store, id]).toEqual([STORE, "c1"]);
    expect(notification.error).toHaveBeenCalledWith("The component was deleted.");
  });

  it("reads the components and their usage again when a page opened from the usage list closes", async () => {
    const { details } = await mountBlade();

    details.vm.$emit("open-designer", COMPONENT.usagePages[0]);
    const [context, events] = calls.openPage.mock.calls[0] as [unknown, { onClose: () => void }];
    expect(context).toMatchObject({ groupId: "g1", storeId: STORE });
    expect(calls.reload).not.toHaveBeenCalled();

    events.onClose();
    await flushPromises();

    expect(calls.reload).toHaveBeenCalledTimes(1);
  });
});
