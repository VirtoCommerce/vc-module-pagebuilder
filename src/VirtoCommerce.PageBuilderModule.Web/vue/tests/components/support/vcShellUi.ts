import { defineComponent, h, type PropType } from "vue";

// The vc-shell UI kit as plain elements: what the designer passes (labels, values, options, disabled) is in the DOM,
// and the events it listens to come from native ones. Nothing of vc-shell's own rendering is tested here.

type Option = Record<string, unknown>;
type Pick = string | ((option: Option) => unknown);
const pick = (option: Option, by: Pick | undefined, fallback: string) => (typeof by === "function" ? by(option) : option[by ?? fallback]);

export const VcIcon = defineComponent({
  name: "VcIcon",
  props: { icon: String, size: String },
  setup: (props) => () => h("i", { "data-icon": props.icon }),
});

export const VcButton = defineComponent({
  name: "VcButton",
  props: { icon: String, variant: String, size: String, disabled: Boolean },
  setup: (props, { slots }) => () => h("button", { type: "button", disabled: props.disabled, "data-icon": props.icon }, slots.default?.()),
});

export const VcHint = defineComponent({ name: "VcHint", setup: (_, { slots }) => () => h("small", slots.default?.()) });

export const VcBlade = defineComponent({
  name: "VcBlade",
  props: { loading: Boolean, title: String, subtitle: String, toolbarItems: Array, modified: Boolean, width: String },
  setup: (props, { slots }) => () => h("section", { "data-loading": String(props.loading) }, slots.default?.()),
});

export const VcBanner = defineComponent({
  name: "VcBanner",
  props: { variant: String, icon: String },
  setup: (_, { slots }) => () => h("div", { role: "alert" }, slots.default?.()),
});

export const VcStatus = defineComponent({ name: "VcStatus", props: { variant: String }, setup: (_, { slots }) => () => h("span", slots.default?.()) });

export const VcSwitch = defineComponent({
  name: "VcSwitch",
  props: { modelValue: Boolean, disabled: Boolean, label: String, hint: String },
  emits: ["update:modelValue"],
  setup: (props, { emit, attrs }) => () =>
    h("input", {
      type: "checkbox",
      role: "switch",
      "aria-label": attrs["aria-label"] ?? props.label,
      checked: props.modelValue,
      disabled: props.disabled,
      onChange: (e: Event) => emit("update:modelValue", (e.target as HTMLInputElement).checked),
    }),
  inheritAttrs: false,
});

const textInput = (name: string, tag: "input" | "textarea") =>
  defineComponent({
    name,
    props: { modelValue: [String, Number, null] as PropType<string | number | null>, label: String, hint: String, placeholder: String, type: String, required: Boolean },
    emits: ["update:modelValue"],
    setup: (props, { emit, attrs }) => () =>
      h("label", { class: attrs.class }, [
        h("span", props.label),
        h(tag, {
          value: props.modelValue ?? "",
          placeholder: props.placeholder,
          "aria-label": attrs["aria-label"] ?? props.label,
          onInput: (e: Event) => emit("update:modelValue", (e.target as HTMLInputElement).value),
        }),
        props.hint ? h("small", props.hint) : null,
      ]),
    inheritAttrs: false,
  });

export const VcInput = textInput("VcInput", "input");
export const VcTextarea = textInput("VcTextarea", "textarea");
export const VcEditor = textInput("VcEditor", "textarea");
export const VcColorInput = textInput("VcColorInput", "input");

/** A native select: its options by index, the chosen option's value emitted (the designer uses emit-value) */
export const VcSelect = defineComponent({
  name: "VcSelect",
  props: {
    modelValue: { type: null as unknown as PropType<unknown>, default: undefined },
    options: { type: Array as PropType<Option[]>, default: () => [] },
    optionValue: [String, Function] as PropType<Pick>,
    optionLabel: [String, Function] as PropType<Pick>,
    label: String,
    placeholder: String,
  },
  emits: ["update:modelValue"],
  setup: (props, { emit }) => () => {
    const values = props.options.map((x) => pick(x, props.optionValue, "value"));
    const selected = values.findIndex((x) => x === props.modelValue);
    return h("label", [
      h("span", props.label),
      h(
        "select",
        {
          "aria-label": props.label,
          value: selected < 0 ? "" : String(selected),
          onChange: (e: Event) => {
            const index = (e.target as HTMLSelectElement).value;
            emit("update:modelValue", index === "" ? null : values[Number(index)]);
          },
        },
        [h("option", { value: "" }, props.placeholder ?? ""), ...props.options.map((x, i) => h("option", { value: String(i) }, String(pick(x, props.optionLabel, "label"))))],
      ),
    ]);
  },
});

export const VcPopup = defineComponent({
  name: "VcPopup",
  props: { title: String, modelValue: Boolean, modalWidth: String },
  emits: ["close"],
  setup: (props, { slots, emit }) => () =>
    h("div", { role: "dialog", "aria-label": props.title }, [
      h("h2", props.title),
      slots.content?.(),
      slots.footer?.(),
      h("button", { type: "button", class: "vc-popup-close", onClick: () => emit("close") }, "close"),
    ]),
});

export const VcDropdown = defineComponent({ name: "VcDropdown", setup: (_, { slots }) => () => h("div", slots.default?.()) });
