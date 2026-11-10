import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { effectScope } from "vue";
import AddSection from "../../src/modules/designer/components/AddSection.vue";
import SharedPanel from "../../src/modules/designer/components/SharedPanel.vue";
import { useDesigner } from "../../src/modules/designer/useDesigner";
import { fakePlatform, status } from "./support/fakePlatform";

const STORE = "B2B-store";
const SEARCH = "/api/page-builder-shared-components/search";

let scope: ReturnType<typeof effectScope>;

beforeEach(() => {
  vi.useFakeTimers();
  scope = effectScope();
});

afterEach(() => {
  scope.stop();
  vi.useRealTimers();
});

async function settle() {
  await vi.runAllTimersAsync();
  await flushPromises();
}

describe("searching a store's shared components", () => {
  it("says the search failed when the shared components can't be read, not that the store has none", async () => {
    const platform = fakePlatform().on("POST", SEARCH, status(500)).install();

    const wrapper = mount(SharedPanel, { props: { storeId: STORE } });
    await settle();

    expect(platform.to(SEARCH)).toHaveLength(1);
    expect(wrapper.find(".pd-shared__empty").text()).toBe("The search failed.");
  });

  it("is not done when a section is added to the header or footer, where shared components can't go", async () => {
    const platform = fakePlatform().on("POST", SEARCH, { results: [] }).install();
    const designer = scope.run(() => useDesigner())!;
    designer.storeId.value = STORE;

    mount(AddSection, { props: { designer, group: "header" } });
    await settle();
    expect(platform.to(SEARCH)).toHaveLength(0);

    mount(AddSection, { props: { designer } });
    await settle();
    expect(platform.to(SEARCH)).toHaveLength(1);
  });
});
