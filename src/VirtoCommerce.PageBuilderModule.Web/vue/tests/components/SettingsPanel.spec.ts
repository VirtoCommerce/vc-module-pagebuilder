import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { defineComponent, effectScope, h, type PropType } from "vue";
import SettingsPanel from "../../src/modules/designer/components/SettingsPanel.vue";
import { useDesigner, type Designer } from "../../src/modules/designer/useDesigner";
import type { Schemas } from "../../src/modules/designer/types";
import { fakePlatform, status } from "./support/fakePlatform";
import { notification } from "./support/vcShell";

// As VcDropdown does: a pick runs the item, and closes the menu only when asked to with closeOnSelect.
const VcDropdown = defineComponent({
  name: "VcDropdown",
  props: { modelValue: Boolean, items: { type: Array as PropType<{ title: string }[]>, default: () => [] }, closeOnSelect: Boolean },
  emits: ["update:modelValue", "item-click"],
  setup(props, { emit, slots }) {
    const pick = (item: unknown) => {
      emit("item-click", item);
      if (props.closeOnSelect) emit("update:modelValue", false);
    };
    return () =>
      h("div", [
        slots.trigger?.({ isActive: props.modelValue, toggle: () => emit("update:modelValue", !props.modelValue) }),
        props.modelValue ? h("ul", { role: "menu" }, props.items.map((item) => h("li", { role: "menuitem", onClick: () => pick(item) }, item.title))) : null,
      ]);
  },
});

const SCHEMAS: Schemas = {
  sections: { promo: { name: "Promo", displayField: "title", settings: [{ id: "title", type: "string", translatable: true }] } },
  blocks: {},
  shared: {},
  objects: {},
};

let designer: Designer;
let scope: ReturnType<typeof effectScope>;

beforeEach(() => {
  scope = effectScope();
  designer = scope.run(() => useDesigner())!;
  designer.schemas.value = SCHEMAS;
  designer.languages.value = { all: ["en-US", "de-DE"], default: "en-US" };
  designer.language.value = "de-DE";
  designer.doc.value = { settings: { id: "settings", type: "settings" }, content: [{ id: "promo-1", type: "promo", title: { "de-DE": "Rabatt", "en-US": "Sale" } }] };
  designer.selection.value = { kind: "section", id: "promo-1" };
});

afterEach(() => scope.stop());

const mountPanel = () => mount(SettingsPanel, { props: { designer, selection: designer.selection.value! }, global: { stubs: { VcDropdown } } });
const menuItem = (wrapper: VueWrapper, text: string) => wrapper.findAll('[role="menuitem"]').find((x) => x.text() === text)!;

describe("SettingsPanel", () => {
  it("closes the actions menu once an action is picked", async () => {
    const wrapper = mountPanel();
    await wrapper.find('button[aria-label="More actions"]').trigger("click");

    await menuItem(wrapper, "Copy").trigger("click");

    expect(wrapper.emitted("copy")).toHaveLength(1);
    expect(wrapper.find('[role="menu"]').exists()).toBe(false);
  });

  it("titles a section by the default language's text, whichever language is edited", () => {
    expect(mountPanel().find(".pd-panel-head__title").text()).toBe("Sale");
  });

  it("tells the user, and leaves the placement in place, when the shared component's sections can't be read to detach it", async () => {
    fakePlatform().on("GET", "/api/page-builder-shared-components/footer/content", status(404)).install();
    designer.doc.value = { ...designer.doc.value, content: [{ id: "placed-1", type: "componentRef", componentRef: "footer" }] };
    designer.components.set("footer", { name: "Footer", usageCount: 2, content: [] });
    designer.selection.value = { kind: "section", id: "placed-1" };
    const wrapper = mountPanel();

    await wrapper.findAll("button").find((x) => x.text() === "Detach")!.trigger("click");
    await flushPromises();

    expect(notification.error).toHaveBeenCalledWith("The shared component couldn't be detached.");
    expect(designer.doc.value.content).toEqual([{ id: "placed-1", type: "componentRef", componentRef: "footer" }]);
  });
});
