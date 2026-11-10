import { describe, expect, it } from "vitest";
import { mount } from "@vue/test-utils";
import { defineComponent, h, type PropType } from "vue";
import Fields from "../../src/modules/designer/components/Fields.vue";
import type { Field } from "../../src/modules/designer/types";

// As VcInput does: min and max reach the native input as attributes, step as a prop, and leaving the input emits blur.
const VcInput = defineComponent({
  name: "VcInput",
  props: { modelValue: [Number, String, null] as PropType<number | string | null>, step: String },
  emits: ["update:modelValue", "blur"],
  setup: (props, { emit, attrs }) => () =>
    h("input", {
      type: "number",
      value: props.modelValue ?? "",
      min: attrs.min,
      max: attrs.max,
      step: props.step,
      onInput: (e: Event) => emit("update:modelValue", (e.target as HTMLInputElement).value),
      onBlur: (e: Event) => emit("blur", e),
    }),
  inheritAttrs: false,
});

const COUNT: Field = { id: "count", type: "number", label: "Count", min: 10, max: 50, step: 5 };

const mountCount = (value: unknown) => mount(Fields, { props: { fields: [COUNT], values: { count: value }, context: {} }, global: { stubs: { VcInput } } });

describe("a number with a range", () => {
  it("gives the input the schema's minimum, maximum and step", () => {
    expect(mountCount(20).find("input").attributes()).toMatchObject({ min: "10", max: "50", step: "5" });
  });

  it("keeps what is typed while the input has focus, so a number can be typed past its first digit", async () => {
    const wrapper = mountCount(20);

    await wrapper.find("input").setValue("1");

    expect(wrapper.emitted("update")).toEqual([["count", 1]]);
  });

  it("comes up to the minimum and down to the maximum once the input is left", async () => {
    const low = mountCount(1);
    await low.find("input").trigger("blur");
    const high = mountCount(1000);
    await high.find("input").trigger("blur");

    expect(low.emitted("update")).toEqual([["count", 10]]);
    expect(high.emitted("update")).toEqual([["count", 50]]);
  });

  it("leaves a number in range, and a cleared one, as they are", async () => {
    const inRange = mountCount(20);
    await inRange.find("input").trigger("blur");
    const cleared = mountCount(undefined);
    await cleared.find("input").trigger("blur");

    expect(inRange.emitted("update")).toBeUndefined();
    expect(cleared.emitted("update")).toBeUndefined();
  });
});
