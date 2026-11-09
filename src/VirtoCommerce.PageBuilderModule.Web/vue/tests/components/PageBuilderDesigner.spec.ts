import { beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises, shallowMount } from "@vue/test-utils";
import PageBuilderDesigner from "../../src/modules/pages/PageBuilderDesigner.vue";
import { apiClient, blade } from "./support/vcShell";
import { fakePlatform } from "./support/fakePlatform";

beforeEach(() => {
  vi.stubGlobal("defineBlade", () => undefined);
  fakePlatform().install();
});

describe("opening the designer", () => {
  it("says the page could not be loaded when its group cannot be read, and stops loading", async () => {
    blade.param.value = "g1";
    apiClient.getGroup = vi.fn(async () => {
      throw new Error("403");
    });

    const wrapper = shallowMount(PageBuilderDesigner, { global: { stubs: { VcBlade: false } } });
    await flushPromises();

    expect(wrapper.find(".pd-message").text()).toContain("couldn't be loaded");
    expect(wrapper.find("[data-loading]").attributes("data-loading")).toBe("false");
  });
});
