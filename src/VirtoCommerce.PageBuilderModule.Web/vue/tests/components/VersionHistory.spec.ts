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

describe("loading more versions", () => {
  type Page = { versions: HistoryVersion[]; hasMore?: boolean; endCursor?: string };
  const listed = (wrapper: VueWrapper) => wrapper.findAll(".vh__sha").map((x) => x.text());

  it("drops a page asked for before a reload, so the reloaded list does not skip a version", async () => {
    const answers: ((page: Page) => void)[] = [];
    const wrapper = mount(VersionHistory, {
      props: {
        source: () => new Promise<Page>((resolve) => answers.push(resolve)),
        dirty: false,
        busy: false,
        canDiscard: false,
      },
    });
    answers.shift()!({ versions: [version("v3"), version("v2")], hasMore: true, endCursor: "after-v2" });
    await flushPromises();

    await wrapper.find("button.vh__wide").trigger("click");
    const reloaded = (wrapper.vm as unknown as { reload: () => Promise<void> }).reload();
    const [more, reload] = answers.splice(0);
    reload({ versions: [version("v4"), version("v3")], hasMore: true, endCursor: "after-v3" });
    await reloaded;
    more({ versions: [version("v1")] });
    await flushPromises();

    expect(listed(wrapper)).toEqual(["v4", "v3"]);
    expect(wrapper.find("button.vh__wide").exists()).toBe(true);
  });

  it("keeps showing that it loads until the latest request answers", async () => {
    const answers: ((page: Page) => void)[] = [];
    const wrapper = mount(VersionHistory, {
      props: {
        source: () => new Promise<Page>((resolve) => answers.push(resolve)),
        dirty: false,
        busy: false,
        canDiscard: false,
      },
    });
    answers.shift()!({ versions: [version("v2")], hasMore: true, endCursor: "after-v2" });
    await flushPromises();

    await wrapper.find("button.vh__wide").trigger("click");
    void (wrapper.vm as unknown as { reload: () => Promise<void> }).reload();
    const [more, reload] = answers.splice(0);
    more({ versions: [version("v1")] });
    await flushPromises();

    expect(wrapper.find(".vh__spin").exists()).toBe(true);

    reload({ versions: [version("v3"), version("v2")] });
    await flushPromises();

    expect(wrapper.find(".vh__spin").exists()).toBe(false);
    expect(listed(wrapper)).toEqual(["v3", "v2"]);
  });
});
