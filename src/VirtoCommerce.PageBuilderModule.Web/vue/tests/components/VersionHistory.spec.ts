import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { compileStyle, parse } from "vue/compiler-sfc";
import VersionHistory from "../../src/modules/designer/components/VersionHistory.vue";
import type { HistoryVersion } from "../../src/modules/designer/history";

const version = (id: string): HistoryVersion => ({ id, label: id, description: "Saved", date: "2026-11-09T10:00:00Z", items: [`page:${id}`] });

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

describe("a row's Preview and Restore", () => {
  it("stay in the tab order while the row is neither hovered nor focused", async () => {
    const file = resolve(dirname(fileURLToPath(import.meta.url)), "../../src/modules/designer/components/VersionHistory.vue");
    const { descriptor } = parse(readFileSync(file, "utf8"));
    const css = compileStyle({ source: descriptor.styles[0].content, filename: file, id: "vh", preprocessLang: "scss" }).code;
    expect(css).toMatch(/@media \(hover: ?hover\)/);
    const sheet = document.createElement("style");
    // jsdom's cascade applies only `screen` media blocks, so the hover-only block that clips them is rewritten to one.
    sheet.textContent = css.replace(/@media \(hover: ?hover\)/g, "@media screen");
    document.head.append(sheet);
    try {
      const wrapper = mount(VersionHistory, {
        attachTo: document.body,
        props: { source: async () => ({ versions: [version("v2"), version("v1")] }), dirty: false, busy: false, canDiscard: false },
      });
      await flushPromises();

      const actions = wrapper.findAll("button.vh__action");
      expect(actions.map((x) => x.text())).toEqual(["Preview", "Preview", "Restore"]);
      for (const action of actions) {
        const style = getComputedStyle(action.element);
        expect(style.display).not.toBe("none");
        expect(style.visibility).not.toBe("hidden");
      }
      expect(getComputedStyle(actions[0].element).position).toBe("absolute");
    } finally {
      sheet.remove();
    }
  });
});
