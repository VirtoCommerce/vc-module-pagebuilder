import { describe, expect, it } from "vitest";
import { defineComponent, h } from "vue";
import { mount, type VueWrapper } from "@vue/test-utils";
import RenameSharedComponentPopup from "../../src/modules/shared-components/components/RenameSharedComponentPopup.vue";

// Passes listeners such as keyup.enter through to the input, as the real field does.
const VcInput = defineComponent({
  name: "VcInput",
  props: { modelValue: String },
  emits: ["update:modelValue"],
  setup: (props, { emit }) => () =>
    h("input", { value: props.modelValue, onInput: (e: Event) => emit("update:modelValue", (e.target as HTMLInputElement).value) }),
});

const save = (wrapper: VueWrapper) => wrapper.findAll("button").find((x) => x.text() === "Save")!;

async function failedRename() {
  const wrapper = mount(RenameSharedComponentPopup, {
    props: { component: { id: "c1", storeId: "s1", name: "Old", usageCount: 0, usagePages: [] } },
    global: { stubs: { VcInput } },
  });
  await wrapper.find("input").setValue("New");
  await wrapper.setProps({ serverError: "An unexpected server error occurred." });
  return wrapper;
}

describe("renaming a shared component after the server failed", () => {
  it("still shows why it failed", async () => {
    const wrapper = await failedRename();

    expect(wrapper.findComponent(VcInput).attributes("error-message")).toBe("An unexpected server error occurred.");
  });

  it("can save the same name again", async () => {
    const wrapper = await failedRename();

    expect(save(wrapper).attributes("disabled")).toBeUndefined();

    await save(wrapper).trigger("click");

    expect(wrapper.emitted("rename")).toEqual([["New"]]);
  });

  it("can save the same name again with Enter", async () => {
    const wrapper = await failedRename();

    await wrapper.find("input").trigger("keyup", { key: "Enter" });

    expect(wrapper.emitted("rename")).toEqual([["New"]]);
  });
});
