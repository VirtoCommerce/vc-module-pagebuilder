import { beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { nextTick } from "vue";
import RulesPanel from "../../src/modules/designer/components/RulesPanel.vue";
import { useTemplateRules } from "../../src/modules/designer/templateRules";
import type { ConditionDescriptor, RulesPick } from "../../src/modules/designer/previewMessages";
import type { TemplateRule } from "../../src/modules/designer/rules";
import type { NamedEntity } from "../../src/modules/designer/api";
import { fakePlatform, themeETag, themeFileAnswer, type FakePlatform } from "./support/fakePlatform";
import { popup } from "./support/vcShell";

// Sortable.js needs a real pointer drag: the panel's drop handler is called as Sortable calls it
const sortable = vi.hoisted(() => ({ options: undefined as undefined | { onUpdate: (e: { oldIndex?: number; newIndex?: number; item: HTMLElement; from: HTMLElement }) => void } }));
vi.mock("@vueuse/integrations/useSortable", () => ({
  useSortable: (_el: unknown, _list: unknown, options: typeof sortable.options) => {
    sortable.options = options;
  },
}));

const nameEquals = { type: "object", element: [{ id: "name", type: "string", label: "Name" }, { id: "equals", type: "string", label: "Value" }] };
/** the conditions a storefront announces in its handshake */
const CONDITIONS: ConditionDescriptor[] = [
  { name: "inCategory", pageTypes: ["product", "category"], label: "In category", value: { type: "list" } },
  { name: "productType", pageTypes: ["product"], label: "Product type", value: { type: "string" } },
  { name: "property", pageTypes: ["product"], label: "Property", value: nameEquals },
  { name: "path", pageTypes: ["page"], label: "Path", value: { type: "string" } },
];

const RULES_FILE = {
  version: 2,
  conditions: ["inCategory", "inStock", "productType"],
  product: [
    { id: "r-printers", template: "product.printer", when: { inCategory: ["cat-printers"] } },
    { id: "r-bundle", template: "product.bundle", when: { productType: "Bundle" }, disabled: true },
    { id: "r-stock", template: "product.printer", when: { inStock: true } },
  ],
  page: [{ id: "r-landing", template: "page.landing", when: { path: "/landing/*" } }],
};

const CATALOG = { storeId: "B2B-store", cultureName: "en-US", currencyCode: "USD" };

let platform: FakePlatform;

beforeEach(() => {
  platform = fakePlatform()
    .on("GET", "/api/pagebuilder/designer/theme-file", themeFileAnswer({ "default/config/template_rules.json": RULES_FILE }))
    .graphql("categories(", { data: { categories: { items: [{ id: "cat-printers", name: "Printers" }, { id: "cat-ink", name: "Ink" }] } } })
    .graphql("childCategories(", {
      data: { childCategories: { childCategories: [{ id: "cat-printers", name: "Printers" }, { id: "cat-ink", name: "Ink" }] } },
    })
    .install();
});

async function mountPanel(
  props: { conditions?: ConditionDescriptor[]; picked?: RulesPick; previewed?: NamedEntity; templateKey?: string; file?: unknown; readonly?: boolean } = {},
) {
  if (props.file) platform.on("GET", "/api/pagebuilder/designer/theme-file", themeFileAnswer({ "default/config/template_rules.json": props.file }));
  const rules = useTemplateRules();
  await rules.load("B2B-store", "default");
  const templateKey = props.templateKey ?? "product.printer";
  const wrapper = mount(RulesPanel, {
    attachTo: document.body,
    props: {
      rules,
      type: "product",
      templateKey,
      keys: ["product", "product.printer", "product.bundle"],
      catalog: CATALOG,
      theme: "default",
      picked: props.picked,
      previewed: props.previewed,
      conditions: "conditions" in props ? props.conditions : CONDITIONS,
      readonly: props.readonly,
    },
  });
  await flushPromises();
  return { wrapper, rules, products: () => rules.rules.value.product as TemplateRule[] };
}

/** the page type's ordered list (not "Applies to") */
const rows = (wrapper: VueWrapper) => wrapper.findAll("li.pd-rules__rule:not(.pd-rules__rule--flat)");
const button = (wrapper: VueWrapper, text: string) => {
  const found = wrapper.findAll("button").find((x) => x.text() === text);
  if (!found) throw new Error(`no button "${text}"`);
  return found;
};
const input = (wrapper: VueWrapper, label: string) => wrapper.find(`input[aria-label="${label}"]`);

describe("RulesPanel", () => {
  it("lists a version 2 file's rules in its order, in plain language, keeping their ids", async () => {
    const { wrapper, products } = await mountPanel();

    // the arrow is spaced by CSS
    expect(rows(wrapper).map((x) => x.find(".pd-rules__text").text())).toEqual([
      "Products in Printers and below→Printer",
      "Products of type Bundle→Bundle",
      "Products inStock: trueUnknown→Printer",
    ]);
    expect(products().map((x) => x.id)).toEqual(["r-printers", "r-bundle", "r-stock"]);
    // "Applies to" the open template: the rules that pick it, numbered as in the whole list
    expect(wrapper.findAll(".pd-rules__rule--flat .pd-rules__number").map((x) => x.text())).toEqual(["1", "3"]);
  });

  it("gives a version 1 file's rules the ids the storefront gives them", async () => {
    const { products } = await mountPanel({ file: { version: 1, product: [{ template: "product.printer", when: { productType: "Printer" } }] } });

    expect(products()).toEqual([{ id: "product-0", template: "product.printer", when: { productType: "Printer" } }]);
  });

  it("adds a rule through the form built from the announced conditions: the conditions set, the empty ones left out", async () => {
    const { wrapper, rules, products } = await mountPanel();

    await button(wrapper, "Add rule").trigger("click");
    await flushPromises();
    // the form offers the product conditions the storefront announced, and only those
    expect(wrapper.find("[role=tree]").exists()).toBe(true);
    expect(input(wrapper, "Product type").exists()).toBe(true);
    expect(input(wrapper, "Name").exists()).toBe(true);
    expect(input(wrapper, "Path").exists()).toBe(false);

    await wrapper.findAll("[role=treeitem] input[type=checkbox]")[1].setValue(true);
    await input(wrapper, "Product type").setValue("Printer");
    // a condition typed and emptied again is not one
    await input(wrapper, "Name").setValue("Color");
    await input(wrapper, "Name").setValue("");
    await button(wrapper, "Save rule").trigger("click");

    const added = products()[3];
    expect(added).toEqual({ id: added.id, template: "product.printer", when: { inCategory: ["cat-ink"], productType: "Printer" } });
    expect(added.id).toMatch(/^[a-z0-9]{8}$/);
    expect(products().slice(0, 3).map((x) => x.id)).toEqual(["r-printers", "r-bundle", "r-stock"]);
    expect(rules.dirty.value).toBe(true);
    expect(rules.pending()?.file).toEqual({
      path: "default/config/template_rules.json",
      eTag: themeETag("default/config/template_rules.json"),
      content: expect.objectContaining({ version: 2 }),
    });
  });

  it("edits a rule in place: its id and its place stay, a condition the site doesn't know is kept", async () => {
    const { wrapper, products } = await mountPanel();

    await rows(wrapper)[2].find(".pd-rules__text").trigger("click");
    // the condition the storefront didn't announce: as JSON, with the warning
    const custom = wrapper.find(".pd-rule-form__custom");
    expect(custom.find("code").text()).toBe("inStock: true");
    expect(custom.find(".pd-rule-form__warning").text()).toBe("The site doesn't know the condition inStock: a rule with it never matches.");

    await input(wrapper, "Product type").setValue("Laser");
    await input(wrapper, "Name").setValue("Color");
    await input(wrapper, "Value").setValue("Black");
    await button(wrapper, "Save rule").trigger("click");

    expect(products()[2]).toEqual({
      id: "r-stock",
      template: "product.printer",
      when: { inStock: true, productType: "Laser", property: { name: "Color", equals: "Black" } },
    });
    expect(products().map((x) => x.id)).toEqual(["r-printers", "r-bundle", "r-stock"]);
  });

  it("shows a condition the site doesn't know as its JSON, marked with the warning", async () => {
    const { wrapper } = await mountPanel();

    const badge = rows(wrapper)[2].find(".pd-rules__badge");
    expect(badge.text()).toBe("Unknown");
    expect(badge.attributes("title")).toBe("The site doesn't know the condition inStock: a rule with it never matches.");
    expect(rows(wrapper)[2].find("strong").text()).toBe("true");
    // the announced ones are not marked
    expect(rows(wrapper)[0].find(".pd-rules__badge").exists()).toBe(false);
  });

  it("moves a dragged rule in the data and puts Sortable's DOM move back; the ids go with their rules", async () => {
    const { wrapper, products } = await mountPanel();
    const list = rows(wrapper)[0].element.parentElement!;
    const items = [...list.children] as HTMLElement[];

    // Sortable has moved the last row to the top before it calls the handler
    list.insertBefore(items[2], items[0]);
    sortable.options!.onUpdate({ oldIndex: 2, newIndex: 0, item: items[2], from: list });
    // the DOM is Vue's again until the data moves the row
    expect([...list.children]).toEqual(items);
    await nextTick();
    await flushPromises();

    expect(products().map((x) => x.id)).toEqual(["r-stock", "r-printers", "r-bundle"]);
    expect(products()[1]).toEqual(RULES_FILE.product[0]);
    expect(rows(wrapper).map((x) => x.find(".pd-rules__text").text())).toEqual([
      "Products inStock: trueUnknown→Printer",
      "Products in Printers and below→Printer",
      "Products of type Bundle→Bundle",
    ]);
    // the rows are Vue's own: each once, in the data's order
    expect(list.children).toHaveLength(3);
  });


  it("shows a switched-off rule as off; the switch turns it on and off again", async () => {
    const { wrapper, products } = await mountPanel();
    const bundle = () => rows(wrapper)[1];

    expect(bundle().classes()).toContain("pd-rules__rule--off");
    expect((bundle().find("input[role=switch]").element as HTMLInputElement).checked).toBe(false);

    await bundle().find("input[role=switch]").setValue(true);
    expect(products()[1]).toEqual({ id: "r-bundle", template: "product.bundle", when: { productType: "Bundle" } });
    expect(bundle().classes()).not.toContain("pd-rules__rule--off");

    await rows(wrapper)[0].find("input[role=switch]").setValue(false);
    expect(products()[0]).toEqual({ ...RULES_FILE.product[0], disabled: true });
  });

  it("removes a rule once the editor confirms, and keeps it when not", async () => {
    const { wrapper, products } = await mountPanel();

    popup.confirm = false;
    await rows(wrapper)[0].find("button[aria-label='Remove rule']").trigger("click");
    await flushPromises();
    expect(products()).toHaveLength(3);

    popup.confirm = true;
    await rows(wrapper)[0].find("button[aria-label='Remove rule']").trigger("click");
    await flushPromises();
    expect(products().map((x) => x.id)).toEqual(["r-bundle", "r-stock"]);
  });

  it("only shows the rules until the storefront has announced its conditions", async () => {
    const { wrapper, rules } = await mountPanel({ conditions: undefined });

    expect(wrapper.text()).toContain("The rules can be edited once the preview has loaded");
    expect(wrapper.findAll("button").some((x) => x.text() === "Add rule" || x.text() === "Edit as JSON")).toBe(false);
    expect(wrapper.find(".pd-rules__handle").exists()).toBe(false);
    expect(wrapper.find("button[aria-label='Remove rule']").exists()).toBe(false);
    expect(rows(wrapper).every((x) => (x.find("input[role=switch]").element as HTMLInputElement).disabled)).toBe(true);
    expect(rows(wrapper).every((x) => x.find(".pd-rules__text").attributes("disabled") !== undefined)).toBe(true);
    // nothing is unknown before the storefront said what it knows
    expect(wrapper.find(".pd-rules__badge").exists()).toBe(false);

    await rows(wrapper)[0].find(".pd-rules__text").trigger("click");
    expect(wrapper.find(".pd-rule-form").exists()).toBe(false);
    expect(rules.dirty.value).toBe(false);
  });

  it("keeps the rules from edits while the file can't be read, and reads it again on Try again", async () => {
    platform.on("GET", "/api/pagebuilder/designer/theme-file", { content: "{ not json", eTag: '"broken"' });
    const rules = useTemplateRules();
    await expect(rules.load("B2B-store", "default")).rejects.toThrow();
    const wrapper = mount(RulesPanel, {
      attachTo: document.body,
      props: { rules, type: "product", templateKey: "product.printer", keys: ["product", "product.printer"], catalog: CATALOG, theme: "default", conditions: CONDITIONS },
    });
    await flushPromises();

    expect(wrapper.text()).toContain("The rules couldn't be read");
    expect(wrapper.findAll("button").some((x) => x.text() === "Add rule" || x.text() === "Edit as JSON")).toBe(false);
    expect(rows(wrapper).every((x) => (x.find("input[role=switch]").element as HTMLInputElement).disabled)).toBe(true);

    platform.on("GET", "/api/pagebuilder/designer/theme-file", themeFileAnswer({ "default/config/template_rules.json": RULES_FILE }));
    await button(wrapper, "Try again").trigger("click");
    await flushPromises();
    expect(wrapper.text()).not.toContain("The rules couldn't be read");
    expect(rules.loaded.value).toBe(true);
    expect(wrapper.findAll("button").some((x) => x.text() === "Add rule")).toBe(true);
  });

  it("only shows the rules to an editor who may not change templates, and says why", async () => {
    const { wrapper } = await mountPanel({ readonly: true });

    expect(wrapper.text()).toContain("You can view the rules but not change them");
    expect(wrapper.text()).not.toContain("The rules can be edited once the preview has loaded");
    expect(wrapper.findAll("button").some((x) => x.text() === "Add rule" || x.text() === "Edit as JSON")).toBe(false);
    expect(wrapper.find(".pd-rules__handle").exists()).toBe(false);
    expect(rows(wrapper).every((x) => (x.find("input[role=switch]").element as HTMLInputElement).disabled)).toBe(true);
    // the storefront has announced its conditions: one it doesn't know is still marked
    expect(wrapper.find(".pd-rules__badge").exists()).toBe(true);
  });

  it("says how many entries of the file it can't read, which a save of the rules leaves out, until the rules are saved", async () => {
    const { wrapper, rules } = await mountPanel({
      file: { ...RULES_FILE, product: [...RULES_FILE.product, { template: 4 }, "printers"], category: { template: "category.a" } },
    });

    const warning = wrapper.find(".pd-rules__note--warning");
    expect(warning.text()).toBe("Entries of the rules file that can't be read here: 3. Saving the rules leaves them out.");
    expect(rows(wrapper)).toHaveLength(3);

    await rows(wrapper)[1].find("input[role=switch]").setValue(true);
    rules.pending()!.saved();
    await nextTick();
    expect(wrapper.find(".pd-rules__note--warning").exists()).toBe(false);
  });

  it("sends the rules with the version they were read at, and after a save with the version it returned", async () => {
    const { wrapper, rules } = await mountPanel();
    await rows(wrapper)[1].find("input[role=switch]").setValue(true);
    expect(rules.pending()!.file.eTag).toBe(themeETag("default/config/template_rules.json"));

    rules.pending()!.saved('"rules-v2"');
    await rows(wrapper)[1].find("input[role=switch]").setValue(false);
    expect(rules.pending()!.file.eTag).toBe('"rules-v2"');
  });

  it("says nothing of unreadable entries for a file it reads whole", async () => {
    const { wrapper } = await mountPanel();
    expect(wrapper.find(".pd-rules__note--warning").exists()).toBe(false);
  });

  it("lights up the rule the preview says the rules pick, and names its template in Test", async () => {
    const picked: RulesPick = { url: "/printer-x", pageType: "product", template: "product.bundle", ruleId: "r-bundle", rule: 1 };
    const { wrapper } = await mountPanel({ picked, previewed: { id: "printer-x", name: "Printer X" } });

    expect(rows(wrapper).map((x) => x.classes().includes("pd-rules__rule--hit"))).toEqual([false, true, false]);
    expect(wrapper.find(".pd-rules__test").text()).toContain("Bundle");
    expect(wrapper.find(".pd-rules__test").text()).toContain("2");
  });

  it("lights up no rule for a verdict about another page type", async () => {
    const picked: RulesPick = { url: "/printers", pageType: "category", template: "category", ruleId: "r-bundle", rule: 1 };
    const { wrapper } = await mountPanel({ picked });

    expect(wrapper.find(".pd-rules__rule--hit").exists()).toBe(false);
  });
});
