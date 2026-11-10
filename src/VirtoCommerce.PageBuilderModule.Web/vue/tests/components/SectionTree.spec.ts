import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { mount, type VueWrapper } from "@vue/test-utils";
import { effectScope, nextTick } from "vue";
import SectionTree from "../../src/modules/designer/components/SectionTree.vue";
import { useDesigner, type Designer } from "../../src/modules/designer/useDesigner";
import type { Item, Schemas } from "../../src/modules/designer/types";

const SCHEMAS: Schemas = {
  sections: { promo: { name: "Promo", displayField: "title", settings: [{ id: "title", type: "string", translatable: true }], blocks: ["link"] } },
  blocks: { link: { name: "Link", displayField: "label", settings: [{ id: "label", type: "string", translatable: true }] } },
  shared: {},
  objects: {},
};

let designer: Designer;
let scope: ReturnType<typeof effectScope>;

beforeEach(() => {
  scope = effectScope();
  designer = scope.run(() => useDesigner())!;
  designer.schemas.value = SCHEMAS;
});

afterEach(() => scope.stop());

function mountTree(content: Item[]) {
  designer.doc.value = { settings: { id: "settings", type: "settings" }, content };
  return mount(SectionTree, { props: { designer } });
}

const button = (wrapper: VueWrapper, text: string) => wrapper.findAll("button").find((x) => x.text() === text);
const sectionNames = (wrapper: VueWrapper) => wrapper.findAll(".pd-tree__item .pd-tree__name").map((x) => x.text());

describe("SectionTree: select mode", () => {
  it("ends once every section is deleted, so Add section is back and the selection bar is gone", async () => {
    const wrapper = mountTree([{ id: "promo-1", type: "promo" }]);
    await button(wrapper, "Select")!.trigger("click");
    await wrapper.find("input.pd-tree__check").setValue(true);
    expect(button(wrapper, "Add section")).toBeUndefined();

    designer.removeChecked();
    await nextTick();

    expect(button(wrapper, "Add section")).toBeDefined();
    expect(wrapper.find(".pd-tree__bulk").exists()).toBe(false);
  });
});

describe("SectionTree: keyboard", () => {
  it("selects a row on Enter, but not on an Enter meant for a button inside the row", async () => {
    const wrapper = mountTree([{ id: "promo-1", type: "promo", blocks: [{ id: "link-1", type: "link" }] }]);
    await wrapper.find("button.pd-tree__toggle").trigger("click");

    for (const inner of wrapper.findAll("button.pd-tree__eye")) await inner.trigger("keydown", { key: "Enter" });
    await wrapper.find("button.pd-tree__toggle").trigger("keydown", { key: "Enter" });
    expect(designer.selection.value).toBeNull();

    await wrapper.find(".pd-tree__row--block").trigger("keydown", { key: "Enter" });
    expect(designer.selection.value).toEqual({ kind: "block", sectionId: "promo-1", id: "link-1" });
  });
});

describe("SectionTree: names", () => {
  it("names sections and blocks by the default language's text, whichever language is edited", async () => {
    designer.languages.value = { all: ["en-US", "de-DE"], default: "en-US" };
    designer.language.value = "de-DE";
    const wrapper = mountTree([
      { id: "promo-1", type: "promo", title: { "de-DE": "Rabatt", "en-US": "Sale" }, blocks: [{ id: "link-1", type: "link", label: { "de-DE": "Mehr", "en-US": "More" } }] },
    ]);
    await wrapper.find("button.pd-tree__toggle").trigger("click");

    expect(sectionNames(wrapper)).toEqual(["Sale", "More"]);
  });
});
