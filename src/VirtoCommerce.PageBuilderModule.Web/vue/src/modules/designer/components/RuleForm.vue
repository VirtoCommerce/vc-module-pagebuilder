<template>
  <div class="pd-rule-form">
    <div class="pd-rule-form__head">
      <VcButton
        variant="ghost"
        size="icon-sm"
        icon="lucide-arrow-left"
        :aria-label="$t('PAGE_BUILDER.DESIGNER.RULES.CANCEL')"
        @click="emit('cancel')"
      />
      <strong>{{ $t(isNew ? "PAGE_BUILDER.DESIGNER.RULES.NEW_RULE" : "PAGE_BUILDER.DESIGNER.RULES.EDIT") }}</strong>
    </div>

    <VcSelect
      v-model="template"
      :label="$t('PAGE_BUILDER.DESIGNER.RULES.TEMPLATE')"
      :options="templateOptions"
      option-value="value"
      option-label="label"
      emit-value
      required
    />

    <p class="pd-rule-form__heading">{{ $t("PAGE_BUILDER.DESIGNER.RULES.CONDITIONS") }}</p>
    <div
      v-for="condition in offered"
      :key="condition.descriptor.name"
      class="pd-rule-form__field"
    >
      <template v-if="condition.input.kind === 'categories'">
        <span class="pd-rule-form__label">{{ condition.label }}</span>
        <div
          v-if="categories.length"
          class="pd-rule-form__chips"
        >
          <span
            v-for="id in categories"
            :key="id"
            class="pd-rule-form__chip"
          >
            {{ names.get(id) ?? id }}
            <button
              type="button"
              :aria-label="$t('PAGE_BUILDER.DESIGNER.REMOVE')"
              @click="set(condition.descriptor.name, categories.filter((x) => x !== id))"
            >
              <VcIcon
                icon="lucide-x"
                size="xs"
              />
            </button>
          </span>
        </div>
        <CategoryTree
          :catalog="catalog"
          :names="names"
          :model-value="categories"
          :label="condition.label"
          @update:model-value="set(condition.descriptor.name, $event)"
        />
      </template>
      <VcInput
        v-else-if="condition.input.kind === 'texts'"
        :model-value="textsOf(condition.descriptor.name)"
        :label="condition.label"
        :hint="$t('PAGE_BUILDER.DESIGNER.RULES.TEXTS_HINT')"
        @update:model-value="setTexts(condition.descriptor.name, String($event ?? ''))"
      />
      <Fields
        v-else
        :fields="[condition.input.field]"
        :values="when"
        :context="{}"
        @update="set"
      />
      <div
        v-if="condition.descriptor.name === PROPERTY_CONDITION && properties.length"
        class="pd-rule-form__chips"
      >
        <button
          v-for="property in properties"
          :key="`${property.name}=${String(property.value)}`"
          type="button"
          class="pd-rule-form__pick"
          @click="set(PROPERTY_CONDITION, { name: property.name, equals: property.value })"
        >
          {{ property.label || property.name }} = {{ String(property.value) }}
        </button>
      </div>
      <p
        v-if="problems[condition.descriptor.name]"
        class="pd-rule-form__error"
        role="alert"
      >
        {{ $t(`PAGE_BUILDER.DESIGNER.RULES.ERRORS.${problems[condition.descriptor.name]!.code}`, problems[condition.descriptor.name]!.params ?? {}) }}
      </p>
    </div>

    <div
      v-for="{ name, value } in unknown"
      :key="name"
      class="pd-rule-form__custom"
    >
      <div>
        <strong>{{ $t("PAGE_BUILDER.DESIGNER.RULES.CUSTOM") }}</strong>
        <code>{{ name }}: {{ JSON.stringify(value) }}</code>
        <small class="pd-rule-form__warning">{{ $t("PAGE_BUILDER.DESIGNER.RULES.UNKNOWN_CONDITION", { name }) }}</small>
        <small>{{ $t("PAGE_BUILDER.DESIGNER.RULES.CUSTOM_HINT") }}</small>
      </div>
      <VcButton
        variant="ghost"
        size="icon-sm"
        icon="lucide-trash-2"
        :aria-label="$t('PAGE_BUILDER.DESIGNER.REMOVE')"
        @click="drop(name)"
      />
    </div>

    <p
      v-if="!hasConditions"
      class="pd-rule-form__note"
    >
      {{ $t("PAGE_BUILDER.DESIGNER.RULES.NO_CONDITIONS") }}
    </p>

    <div class="pd-rule-form__actions">
      <VcButton
        variant="secondary"
        @click="emit('cancel')"
      >
        {{ $t("PAGE_BUILDER.DESIGNER.RULES.CANCEL") }}
      </VcButton>
      <VcButton
        :disabled="!template || Object.keys(problems).length > 0"
        @click="save"
      >
        {{ $t("PAGE_BUILDER.DESIGNER.RULES.SAVE") }}
      </VcButton>
    </div>
  </div>
</template>

<script setup lang="ts">
// The conditions object is edited in place, so conditions the storefront did not announce stay as they were.
import { computed, reactive, ref } from "vue";
import { useI18n } from "vue-i18n";
import { VcButton, VcIcon, VcInput, VcSelect } from "@vc-shell/framework/ui";
import type { CatalogContext, ProductFacts } from "../api";
import type { ConditionDescriptor } from "../previewMessages";
import {
  BUILT_IN_CONDITIONS,
  CATEGORY_CONDITION,
  PROPERTY_CONDITION,
  categoryIds,
  checkCondition,
  cleanWhen,
  conditionInput,
  conditionsFor,
  optionLabel,
  splitConditions,
  splitTexts,
  type ConditionProblem,
} from "../ruleConditions";
import type { TemplateRule } from "../rules";
import { alternateName, type PageType } from "../templates";
import type { Field } from "../types";
import CategoryTree from "./CategoryTree.vue";
import Fields from "./Fields.vue";

const props = defineProps<{
  type: PageType;
  rule: TemplateRule;
  isNew?: boolean;
  keys: string[];
  catalog: CatalogContext;
  names: Map<string, string>;
  conditions: ConditionDescriptor[];
  properties: ProductFacts["properties"];
}>();
const emit = defineEmits<{ save: [rule: TemplateRule]; cancel: [] }>();

const { t, te } = useI18n({ useScope: "global" });
const template = ref(props.rule.template);
const when = ref<Record<string, unknown>>(JSON.parse(JSON.stringify(props.rule.when ?? {})));

const templateOptions = computed(() =>
  [...new Set([props.type, ...props.keys, props.rule.template].filter(Boolean))].map((key) => ({
    value: key,
    label: key.includes(".") ? alternateName(key) : t("PAGE_BUILDER.DESIGNER.TEMPLATES.DEFAULT"),
  })),
);

const SUB_LABELS: Record<string, string> = { name: "NAME", equals: "VALUE" };
const rulesText = (key: string) => t(`PAGE_BUILDER.DESIGNER.RULES.${key}`);

function localized(field: Field, descriptor: ConditionDescriptor): Field {
  const known = BUILT_IN_CONDITIONS[descriptor.name];
  return {
    ...field,
    label: known ? rulesText(known.label) : (descriptor.label ?? descriptor.name),
    ...(known?.hint ? { info: rulesText(known.hint) } : {}),
    ...(field.options ? { options: field.options.map((x) => ({ ...x, label: optionLabel(x, { t, te }) })) } : {}),
    ...(field.element ? { element: field.element.map((x) => ({ ...x, label: x.id && SUB_LABELS[x.id] ? rulesText(SUB_LABELS[x.id]) : x.label })) } : {}),
  };
}

const offered = computed(() =>
  conditionsFor(props.conditions, props.type).map((descriptor) => {
    const input = conditionInput(descriptor);
    const known = BUILT_IN_CONDITIONS[descriptor.name];
    return {
      descriptor,
      label: known ? rulesText(known.label) : (descriptor.label ?? descriptor.name),
      input: input.kind === "field" ? { ...input, field: localized(input.field, descriptor) } : input,
    };
  }),
);
const unknown = computed(() => splitConditions(when.value, props.conditions, props.type).unknown);

const categories = computed(() => categoryIds(when.value[CATEGORY_CONDITION]));
const set = (name: string, value: unknown) => (when.value = { ...when.value, [name]: value });
const drop = (name: string) => (when.value = Object.fromEntries(Object.entries(when.value).filter(([key]) => key !== name)));

const typed = reactive(new Map<string, string>());
const textsOf = (name: string) => typed.get(name) ?? (Array.isArray(when.value[name]) ? (when.value[name] as unknown[]).join(", ") : "");
function setTexts(name: string, text: string) {
  typed.set(name, text);
  set(name, splitTexts(text));
}

const cleaned = computed(() => cleanWhen(when.value, props.conditions, props.type));
const hasConditions = computed(() => Object.keys(cleaned.value).length > 0);
const problems = computed(() => {
  const found: Record<string, ConditionProblem> = {};
  for (const { name, value, descriptor } of splitConditions(cleaned.value, props.conditions, props.type).known) {
    const problem = checkCondition(descriptor, value);
    if (problem) found[name] = problem;
  }
  return found;
});

function save() {
  const { when: _when, ...rest } = props.rule;
  emit("save", { ...rest, template: template.value, ...(hasConditions.value ? { when: cleaned.value } : {}) });
}
</script>

<style lang="scss" scoped>
.pd-rule-form {
  display: flex;
  flex-direction: column;
  gap: 14px;

  :deep(.vc-label__content) {
    font-size: 13px;
  }

  &__head {
    display: flex;
    align-items: center;
    gap: 6px;
    margin: -4px 0 0 -6px;
    font-size: 14px;
  }

  &__heading {
    margin: 4px 0 -6px;
    color: var(--neutrals-700);
    font-size: 11px;
    font-weight: 600;
    letter-spacing: 0.04em;
    text-transform: uppercase;
  }

  &__field {
    display: flex;
    flex-direction: column;
    gap: 6px;
  }

  &__label {
    font-size: 13px;
    font-weight: 500;
    color: var(--neutrals-900);
  }

  &__chips {
    display: flex;
    flex-wrap: wrap;
    gap: 4px;
  }

  &__chip {
    display: inline-flex;
    align-items: center;
    gap: 2px;
    padding: 2px 4px 2px 8px;
    border-radius: 999px;
    background: var(--primary-50);
    color: var(--primary-700);
    font-size: 12px;

    button {
      display: flex;
      padding: 2px;
      border: 0;
      border-radius: 999px;
      background: none;
      color: inherit;
      cursor: pointer;

      &:hover {
        background: var(--primary-100);
      }
    }
  }

  &__custom {
    display: flex;
    align-items: flex-start;
    gap: 6px;
    padding: 8px 10px;
    border: 1px dashed var(--neutrals-300);
    border-radius: 8px;

    div {
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: 2px;
      min-width: 0;
    }

    code {
      overflow-wrap: anywhere;
      font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
      font-size: 12px;
    }

    small {
      color: var(--neutrals-500);
    }
  }

  &__note {
    margin: 0;
    color: var(--neutrals-500);
  }

  &__error {
    margin: 0;
    color: var(--danger-700);
    font-size: 12px;
  }

  &__custom &__warning {
    color: var(--warning-700);
  }

  &__pick {
    max-width: 100%;
    padding: 2px 8px;
    border: 1px solid var(--primary-200);
    border-radius: 999px;
    background: var(--primary-50);
    color: var(--primary-700);
    font: inherit;
    font-size: 12px;
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
    cursor: pointer;

    &:hover {
      background: var(--primary-100);
    }
  }

  &__actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
  }
}
</style>
