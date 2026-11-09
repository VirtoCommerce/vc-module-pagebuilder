import { describe, expect, it } from "vitest";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import VersionHistory from "../../src/modules/designer/components/VersionHistory.vue";
import type { HistoryVersion } from "../../src/modules/designer/history";

const version = (id: string): HistoryVersion => ({ id, label: id, message: "Saved", date: "2026-11-09T10:00:00Z", items: [`page:${id}`] }) as HistoryVersion;

async function mountPanel(restoreRefused?: (version: HistoryVersion) => string | undefined) {
  const wrapper = mount(VersionHistory, {
    props: {
      source: async () => ({ versions: [version("v2"), version("v1")] }),
      dirty: false,
      busy: false,
      canDiscard: false,
      restoreRefused,
    },
  });
  await flushPromises();
  return wrapper;
}

const restore = (wrapper: VueWrapper) => wrapper.find("button.vh__action--restore");

describe("restoring a version", () => {
  it("is offered when nothing refuses it", async () => {
    const wrapper = await mountPanel();

    expect(restore(wrapper).attributes("disabled")).toBeUndefined();
  });

  it("is not offered for a version the editor may not restore, and says why", async () => {
    const wrapper = await mountPanel((x) => (x.id === "v1" ? "No access to the header" : undefined));

    expect(restore(wrapper).attributes("disabled")).toBeDefined();
    expect(restore(wrapper).attributes("title")).toBe("No access to the header");
  });
});
