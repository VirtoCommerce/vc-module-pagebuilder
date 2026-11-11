import { describe, expect, it } from "vitest";
import { mount } from "@vue/test-utils";
import Fields from "../../src/modules/designer/components/Fields.vue";
import type { Field } from "../../src/modules/designer/types";

const LONG = "Lorem ipsum dolor sit amet. ".repeat(100);

const mountField = (field: Field) => mount(Fields, { props: { fields: [field], values: { [field.id!]: LONG }, context: {} } });
const maxLength = (wrapper: ReturnType<typeof mountField>) => Number(wrapper.find("textarea").attributes("maxlength") ?? Infinity);

describe("a long text field", () => {
  it.each<[string, Field]>([
    ["markdown", { id: "body", type: "markdown", label: "Body" }],
    ["multiline string", { id: "message", type: "string", label: "Message", multiline: true }],
  ])("%s: holds text far longer than 1024 characters", (_, field) => {
    const wrapper = mountField(field);

    expect((wrapper.find("textarea").element as HTMLTextAreaElement).value).toBe(LONG);
    expect(maxLength(wrapper)).toBeGreaterThanOrEqual(100_000);
  });
});
