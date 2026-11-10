import { beforeEach, describe, expect, it } from "vitest";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import CategoryTree from "../../src/modules/designer/components/CategoryTree.vue";
import { fakePlatform, status, type Request } from "./support/fakePlatform";

const CATALOG = { storeId: "B2B-store", cultureName: "en-US", currencyCode: "USD" };
const TOP = [{ id: "cat-printers", name: "Printers", childCategories: [{ id: "cat-laser" }] }];
const UNDER_PRINTERS = [{ id: "cat-laser", name: "Laser printers" }];

let failing: Set<string>;

beforeEach(() => {
  failing = new Set();
  fakePlatform()
    .graphql("childCategories(", (request: Request) => {
      const parent = (request.body as { variables: { categoryId?: string } }).variables.categoryId ?? "";
      if (failing.has(parent)) return status(500);
      return { data: { childCategories: { childCategories: parent ? UNDER_PRINTERS : TOP } } };
    })
    .install();
});

async function mountTree() {
  const wrapper = mount(CategoryTree, { props: { catalog: CATALOG, modelValue: [], names: new Map<string, string>() } });
  await flushPromises();
  return wrapper;
}

const names = (wrapper: VueWrapper) => wrapper.findAll(".pd-cats__label span").map((x) => x.text());
const note = (wrapper: VueWrapper) => wrapper.find(".pd-cats__note");

describe("CategoryTree", () => {
  it("offers Try again when the top categories fail to load, and shows them once they do", async () => {
    failing.add("");
    const wrapper = await mountTree();
    expect(note(wrapper).text()).toContain("The categories couldn't be loaded.");

    failing.clear();
    await note(wrapper).find("button").trigger("click");
    await flushPromises();

    expect(names(wrapper)).toEqual(["Printers"]);
    expect(note(wrapper).exists()).toBe(false);
  });

  it("stops saying the categories failed once a subcategory list loads on a second try", async () => {
    failing.add("cat-printers");
    const wrapper = await mountTree();
    const toggle = wrapper.find("button.pd-cats__toggle");
    await toggle.trigger("click");
    await flushPromises();
    expect(note(wrapper).exists()).toBe(true);

    failing.clear();
    await toggle.trigger("click");
    await toggle.trigger("click");
    await flushPromises();

    expect(names(wrapper)).toEqual(["Printers", "Laser printers"]);
    expect(note(wrapper).exists()).toBe(false);
  });
});
