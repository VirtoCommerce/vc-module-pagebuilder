import { computed, defineComponent, h, inject, onMounted, provide, reactive, ref, toValue, watch, type InjectionKey, type PropType, type Ref } from "vue";
import { usePermissions } from "./vcShell";

type Option = Record<string, unknown>;
type Pick = string | ((option: Option) => unknown);
// As VcSelect: a primitive option is its own value and label, and an object without the key stands for itself.
const pick = (option: unknown, by: Pick | undefined, fallback: string) => {
  if (option === null || typeof option !== "object") return option;
  if (typeof by === "function") return by(option as Option);
  const key = by ?? fallback;
  return key in option ? (option as Option)[key] : option;
};

const BladeSkeletonKey: InjectionKey<Ref<boolean>> = Symbol("BladeSkeleton");

export const VcIcon = defineComponent({
  name: "VcIcon",
  props: { icon: String, size: String },
  setup: (props) => () => h("i", { "data-icon": props.icon }),
});

// As VcButton: a loading button is disabled, and no button shows while its blade has its first-load skeleton.
export const VcButton = defineComponent({
  name: "VcButton",
  props: { icon: String, variant: String, size: String, disabled: Boolean, loading: Boolean },
  setup: (props, { slots }) => {
    const skeleton = inject(BladeSkeletonKey, ref(false));
    return () =>
      skeleton.value
        ? null
        : h("button", { type: "button", disabled: props.disabled || props.loading, "aria-busy": props.loading || undefined, "data-icon": props.icon }, slots.default?.());
  },
});

export const VcHint = defineComponent({ name: "VcHint", setup: (_, { slots }) => () => h("small", slots.default?.()) });

interface ToolbarItem {
  id?: string;
  title?: string | Ref<string | undefined>;
  isVisible?: boolean | Ref<boolean | undefined> | (() => boolean | undefined);
  disabled?: boolean | Ref<boolean | undefined>;
  permissions?: string | string[];
  clickHandler?: () => unknown;
}

// As the real toolbar's: a disabled button stays focusable (aria-disabled) and ignores clicks, as it does while its click runs.
export const VcBlade = defineComponent({
  name: "VcBlade",
  props: {
    loading: Boolean,
    title: String,
    subtitle: String,
    toolbarItems: { type: Array as PropType<ToolbarItem[]>, default: () => [] },
    modified: Boolean,
    width: String,
  },
  setup: (props, { slots }) => {
    const { hasAccess } = usePermissions();
    const loadedOnce = ref(false);
    watch(
      () => props.loading,
      (loading, was) => {
        if (was && !loading) loadedOnce.value = true;
      },
    );
    provide(
      BladeSkeletonKey,
      computed(() => props.loading && !loadedOnce.value),
    );
    const visible = (item: ToolbarItem) =>
      hasAccess(item.permissions) && (item.isVisible === undefined || !!(typeof item.isVisible === "function" ? item.isVisible() : toValue(item.isVisible)));
    const waiting = reactive(new Set<string | number>());
    const click = async (item: ToolbarItem, key: string | number) => {
      if (toValue(item.disabled) || waiting.has(key) || !item.clickHandler) return;
      waiting.add(key);
      try {
        await item.clickHandler();
      } finally {
        waiting.delete(key);
      }
    };
    const button = (item: ToolbarItem, index: number) => {
      const key = item.id ?? index;
      const inert = !!toValue(item.disabled) || waiting.has(key);
      return h("button", { key, type: "button", "data-test-id": item.id, "aria-disabled": inert || undefined, onClick: () => click(item, key) }, toValue(item.title));
    };
    return () => h("section", { class: "vc-blade" }, [h("div", { role: "toolbar" }, props.toolbarItems.filter(visible).map(button)), slots.default?.()]);
  },
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

// As VcInput: a number or integer input emits a number, or null once cleared; a negative number turns positive.
const typed = (type: string | undefined, raw: string) => {
  if (type !== "number" && type !== "integer") return raw;
  if (raw === "") return null;
  const value = Math.abs(parseFloat(raw));
  return type === "integer" ? Math.trunc(value) : value;
};

const textInput = (name: string, tag: "input" | "textarea", maxlength?: string) =>
  defineComponent({
    name,
    props: {
      modelValue: [String, Number, null] as PropType<string | number | null>,
      label: String,
      hint: String,
      placeholder: String,
      type: String,
      required: Boolean,
      disabled: Boolean,
      clearable: Boolean,
      maxlength: { type: [String, Number], default: maxlength },
      debounce: [String, Number],
    },
    emits: ["update:modelValue", "blur"],
    setup: (props, { emit, attrs }) => {
      let timer: ReturnType<typeof setTimeout> | undefined;
      const update = (value: string | number | null) => {
        if (value === props.modelValue) return;
        clearTimeout(timer);
        if (props.debounce === undefined) emit("update:modelValue", value);
        else timer = setTimeout(() => emit("update:modelValue", value), Number(props.debounce));
      };
      return () =>
        h("label", { class: attrs.class }, [
          h("span", props.label),
          h(tag, {
            type: tag === "input" ? (props.type === "integer" ? "number" : props.type) : undefined,
            value: props.modelValue ?? "",
            placeholder: props.placeholder,
            disabled: props.disabled,
            maxlength: props.maxlength,
            "aria-label": attrs["aria-label"] ?? props.label,
            onInput: (e: Event) => update(typed(props.type, (e.target as HTMLInputElement).value)),
            onBlur: (e: Event) => emit("blur", e),
          }),
          props.clearable && props.modelValue && !props.disabled && props.type !== "password"
            ? h("button", { type: "button", class: "vc-input__clear", "aria-label": "Clear", onClick: () => emit("update:modelValue", null) })
            : null,
          props.hint ? h("small", props.hint) : null,
        ]);
    },
    inheritAttrs: false,
  });

export const VcInput = textInput("VcInput", "input", "1024");
export const VcTextarea = textInput("VcTextarea", "textarea", "1024");
export const VcEditor = textInput("VcEditor", "textarea");
export const VcColorInput = textInput("VcColorInput", "input");

type Loader = (keyword?: string, skip?: number, ids?: unknown[]) => Promise<{ results?: unknown[] } | undefined>;

// As VcSelect: a loader is asked for the selected value once mounted, and for the first page when the select opens.
export const VcSelect = defineComponent({
  name: "VcSelect",
  props: {
    modelValue: { type: null as unknown as PropType<unknown>, default: undefined },
    options: { type: [Array, Function] as PropType<unknown[] | Loader>, default: () => [] },
    optionValue: [String, Function] as PropType<Pick>,
    optionLabel: [String, Function] as PropType<Pick>,
    label: String,
    placeholder: String,
  },
  emits: ["update:modelValue"],
  setup: (props, { emit }) => {
    const loaded = ref<unknown[]>([]);
    const load = async (...args: Parameters<Loader>) => {
      if (typeof props.options === "function") loaded.value = (await props.options(...args))?.results ?? [];
    };
    onMounted(() => {
      if (props.modelValue != null) void load(undefined, undefined, [props.modelValue]);
    });
    return () => {
      const options = typeof props.options === "function" ? loaded.value : props.options;
      const values = options.map((x) => pick(x, props.optionValue, "id"));
      const selected = values.findIndex((x) => x === props.modelValue);
      return h("label", [
        h("span", props.label),
        h(
          "select",
          {
            "aria-label": props.label,
            value: selected < 0 ? "" : String(selected),
            onFocus: () => void load(undefined, 0),
            onChange: (e: Event) => {
              const index = (e.target as HTMLSelectElement).value;
              emit("update:modelValue", index === "" ? null : values[Number(index)]);
            },
          },
          [h("option", { value: "" }, props.placeholder ?? ""), ...options.map((x, i) => h("option", { value: String(i) }, String(pick(x, props.optionLabel, "title"))))],
        ),
      ]);
    };
  },
});

export const VcPopup = defineComponent({
  name: "VcPopup",
  props: {
    title: String,
    modelValue: { type: Boolean, default: undefined },
    closable: { type: Boolean, default: true },
    closeOnEscape: { type: Boolean, default: undefined },
    modalWidth: String,
  },
  emits: ["close", "update:modelValue"],
  setup: (props, { slots, emit }) => {
    const close = (reason: "action" | "escape") => {
      emit("update:modelValue", false);
      emit("close", reason);
    };
    const closeFromAction = () => {
      if (props.closable) close("action");
    };
    const onKeydown = (e: KeyboardEvent) => {
      if (e.key !== "Escape") return;
      e.preventDefault();
      if (props.closeOnEscape ?? props.closable) close("escape");
    };
    return () =>
      props.modelValue === false
        ? null
        : h("div", { role: "dialog", "aria-label": props.title, onKeydown }, [
            h("div", { class: "vc-popup__header" }, [
              h("h3", slots.header?.() ?? props.title),
              props.closable ? h("button", { type: "button", class: "vc-popup__close-btn", "aria-label": "Close", onClick: closeFromAction }) : null,
            ]),
            h("div", { class: "vc-popup__content" }, slots.content?.()),
            slots.footer ? h("div", { class: "vc-popup__footer" }, slots.footer({ close: closeFromAction })) : null,
          ]);
  },
});

export const VcDropdown = defineComponent({ name: "VcDropdown", setup: (_, { slots }) => () => h("div", slots.default?.()) });

export const VcForm = defineComponent({ name: "VcForm", setup: (_, { slots }) => () => h("form", slots.default?.()) });
