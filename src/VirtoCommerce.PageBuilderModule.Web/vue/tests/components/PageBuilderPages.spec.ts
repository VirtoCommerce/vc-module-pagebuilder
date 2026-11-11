import { beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent, ref } from "vue";
import { mount } from "@vue/test-utils";
import PageBuilderPages from "../../src/modules/pages/PageBuilderPages.vue";
import { BladeMaximizedKey, blade, resetVcShell } from "./support/vcShell";

vi.mock("../../src/modules/page-builder/components", () => ({ PagesList: defineComponent({ render: () => null }) }));
vi.mock("../../src/modules/page-builder/composables", async () => {
  const { ref: vueRef } = await import("vue");
  const count = () => vueRef<number | undefined>();
  return {
    PageLifecycleFilters: { Draft: "drafts", Active: "active", Archived: "archived", Pending: "pending" },
    pageCounts: { draft: count(), pending: count(), active: count(), archived: count(), all: count() },
    refreshMenuBadges: vi.fn(),
    useUrlParams: () => ({ setStoreId: vi.fn() }),
  };
});
vi.mock("../../src/modules/page-builder/composables/usePagesListToolbar", async () => {
  const { ref: vueRef } = await import("vue");
  return { usePagesListToolbar: () => vueRef([]) };
});

beforeEach(() => {
  resetVcShell();
  vi.stubGlobal("defineBlade", () => undefined);
});

describe("PageBuilderPages", () => {
  it("opens maximized and maximizes again when it becomes the active blade", () => {
    const maximized = ref(false);
    mount(PageBuilderPages, { global: { provide: { [BladeMaximizedKey]: maximized } } });
    expect(maximized.value).toBe(true);

    maximized.value = false;
    const [[activated]] = blade.onActivated.mock.calls;
    activated();
    expect(maximized.value).toBe(true);
  });
});
