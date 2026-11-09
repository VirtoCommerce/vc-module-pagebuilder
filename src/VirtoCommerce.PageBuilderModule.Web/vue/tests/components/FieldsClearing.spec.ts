import { describe, expect, it } from "vitest";
import { mount } from "@vue/test-utils";
import { VcSelect } from "./support/vcShellUi";
import Fields from "../../src/modules/designer/components/Fields.vue";
import type { Field } from "../../src/modules/designer/types";

const NUMBER: Field = { id: "count", type: "number", label: "Count" };
const SELECT: Field = { id: "size", type: "select", label: "Size", options: [{ label: "Small", value: "s" }] };

const mountField = (field: Field, value: unknown) => mount(Fields, { props: { fields: [field], values: { [field.id!]: value }, context: {} } });

describe("a cleared field", () => {
  it("number: leaves the setting out, so the storefront's default applies", async () => {
    const wrapper = mountField(NUMBER, 4);

    await wrapper.find("input").setValue("");

    expect(wrapper.emitted("update")!.at(-1)).toEqual(["count", undefined]);
  });

  it("number: a value stays a number", async () => {
    const wrapper = mountField(NUMBER, 4);

    await wrapper.find("input").setValue("7");

    expect(wrapper.emitted("update")!.at(-1)).toEqual(["count", 7]);
  });

  it("select: leaves the setting out", async () => {
    const wrapper = mountField(SELECT, "s");

    wrapper.findComponent(VcSelect).vm.$emit("update:modelValue", null);

    expect(wrapper.emitted("update")!.at(-1)).toEqual(["size", undefined]);
  });
});
