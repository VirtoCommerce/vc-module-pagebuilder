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

    <template v-if="type !== 'page'">
      <div class="pd-rule-form__field">
        <span class="pd-rule-form__label">{{ $t("PAGE_BUILDER.DESIGNER.RULES.IN_CATEGORIES") }}</span>
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
              @click="set('inCategory', categories.filter((x) => x !== id))"
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
          :label="$t('PAGE_BUILDER.DESIGNER.RULES.IN_CATEGORIES')"
          @update:model-value="set('inCategory', $event)"
        />
      </div>
      <VcSelect
        v-if="type === 'product'"
        :model-value="text('productType') || null"
        :label="$t('PAGE_BUILDER.DESIGNER.RULES.PRODUCT_TYPE')"
        :placeholder="$t('PAGE_BUILDER.DESIGNER.RULES.ANY')"
        :options="productTypes"
        option-value="value"
        option-label="label"
        emit-value
        clearable
        @update:model-value="set('productType', $event ?? '')"
      />
      <fieldset class="pd-rule-form__pair">
        <legend>{{ $t("PAGE_BUILDER.DESIGNER.RULES.PROPERTY") }}</legend>
        <VcInput
          :model-value="named('property').name"
          :placeholder="$t('PAGE_BUILDER.DESIGNER.RULES.NAME')"
          :aria-label="$t('PAGE_BUILDER.DESIGNER.RULES.NAME')"
          @update:model-value="setNamed('property', 'name', $event)"
        />
        <span>=</span>
        <VcInput
          :model-value="String(named('property').equals ?? '')"
          :placeholder="$t('PAGE_BUILDER.DESIGNER.RULES.VALUE')"
          :aria-label="$t('PAGE_BUILDER.DESIGNER.RULES.VALUE')"
          @update:model-value="setNamed('property', 'equals', $event)"
        />
      </fieldset>
    </template>

    <template v-else>
      <VcInput
        :model-value="text('path')"
        :label="$t('PAGE_BUILDER.DESIGNER.RULES.PATH')"
        :hint="$t('PAGE_BUILDER.DESIGNER.RULES.PATH_HINT')"
        placeholder="/landing/*"
        @update:model-value="set('path', $event)"
      />
      <VcInput
        :model-value="text('language')"
        :label="$t('PAGE_BUILDER.DESIGNER.RULES.LANGUAGE')"
        :hint="$t('PAGE_BUILDER.DESIGNER.RULES.LANGUAGE_HINT')"
        placeholder="de-DE"
        @update:model-value="set('language', $event)"
      />
      <!-- the hint below the box (VcSelect's own has one line's room) -->
      <div class="pd-rule-form__field">
        <VcSelect
          :model-value="text('source') || null"
          :label="$t('PAGE_BUILDER.DESIGNER.RULES.SOURCE')"
          :placeholder="$t('PAGE_BUILDER.DESIGNER.RULES.ANY')"
          :options="sources"
          option-value="value"
          option-label="label"
          emit-value
          clearable
          @update:model-value="set('source', $event ?? '')"
        />
        <VcHint>{{ $t("PAGE_BUILDER.DESIGNER.RULES.SOURCE_NOT_YET") }}</VcHint>
      </div>
      <fieldset class="pd-rule-form__pair">
        <legend>{{ $t("PAGE_BUILDER.DESIGNER.RULES.SETTING") }}</legend>
        <VcInput
          :model-value="named('setting').name"
          :placeholder="$t('PAGE_BUILDER.DESIGNER.RULES.NAME')"
          :aria-label="$t('PAGE_BUILDER.DESIGNER.RULES.NAME')"
          @update:model-value="setNamed('setting', 'name', $event)"
        />
        <span>=</span>
        <VcInput
          :model-value="String(named('setting').equals ?? '')"
          :placeholder="$t('PAGE_BUILDER.DESIGNER.RULES.VALUE')"
          :aria-label="$t('PAGE_BUILDER.DESIGNER.RULES.VALUE')"
          @update:model-value="setNamed('setting', 'equals', $event)"
        />
      </fieldset>
    </template>

    <!-- what the designer doesn't edit: kept as it is (Edit as JSON changes it) -->
    <div
      v-for="[name, value] in custom"
      :key="name"
      class="pd-rule-form__custom"
    >
      <div>
        <strong>{{ $t("PAGE_BUILDER.DESIGNER.RULES.CUSTOM") }}</strong>
        <code>{{ name }}: {{ JSON.stringify(value) }}</code>
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
        :disabled="!template"
        @click="save"
      >
        {{ $t("PAGE_BUILDER.DESIGNER.RULES.SAVE") }}
      </VcButton>
    </div>
  </div>
</template>

<script setup lang="ts">
// Add rule / Edit rule: the template, then the conditions the page type has (all must hold). The conditions object is
// edited in place, so conditions the designer doesn't know stay as they were; the ones left empty are dropped.
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import { VcButton, VcHint, VcIcon, VcInput, VcSelect } from "@vc-shell/framework/ui";
import type { CatalogContext } from "../api";
import { SOURCES, cleanWhen, conditionsOf, type TemplateRule } from "../rules";
import { alternateName, type PageType } from "../templates";
import CategoryTree from "./CategoryTree.vue";

const props = defineProps<{
  type: PageType;
  rule: TemplateRule;
  isNew?: boolean;
  /** the theme's templates of this type */
  keys: string[];
  catalog: CatalogContext;
  names: Map<string, string>;
}>();
const emit = defineEmits<{ save: [rule: TemplateRule]; cancel: [] }>();

const { t } = useI18n({ useScope: "global" });
const template = ref(props.rule.template);
const when = ref<Record<string, unknown>>(JSON.parse(JSON.stringify(props.rule.when ?? {})));

const templateOptions = computed(() =>
  [...new Set([props.type, ...props.keys, props.rule.template].filter(Boolean))].map((key) => ({
    value: key,
    label: key.includes(".") ? alternateName(key) : t("PAGE_BUILDER.DESIGNER.TEMPLATES.DEFAULT"),
  })),
);
const productTypes = computed(() =>
  [...new Set(["Physical", "Digital", "BillOfMaterials", text("productType")].filter(Boolean))].map((x) => ({ value: x, label: x })),
);
const sources = computed(() => SOURCES.map((x) => ({ value: x, label: t(`PAGE_BUILDER.DESIGNER.RULES.SOURCES.${x.toUpperCase()}`) })));

const text = (name: string) => {
  const value = when.value[name];
  return typeof value === "string" ? value : "";
};
const categories = computed(() => (Array.isArray(when.value.inCategory) ? (when.value.inCategory as string[]) : []));
const named = (name: string) => ({ name: "", equals: "", ...(when.value[name] as object) }) as { name: string; equals: unknown };
const set = (name: string, value: unknown) => (when.value = { ...when.value, [name]: value });
const setNamed = (name: string, field: "name" | "equals", value: unknown) => set(name, { ...named(name), [field]: value });
const drop = (name: string) => (when.value = Object.fromEntries(Object.entries(when.value).filter(([key]) => key !== name)));

const custom = computed(() => conditionsOf({ template: "", when: when.value }, props.type).custom);
const hasConditions = computed(() => Object.keys(cleanWhen(when.value, props.type)).length > 0);

function save() {
  const conditions = cleanWhen(when.value, props.type);
  const { when: _when, ...rest } = props.rule;
  emit("save", { ...rest, template: template.value, ...(Object.keys(conditions).length ? { when: conditions } : {}) });
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

  &__pair {
    display: grid;
    grid-template-columns: 1fr auto 1fr;
    align-items: center;
    gap: 6px;
    margin: 0;
    padding: 0;
    border: 0;

    legend {
      margin-bottom: 6px;
      padding: 0;
      font-size: 13px;
      font-weight: 500;
      color: var(--neutrals-900);
    }

    span {
      color: var(--neutrals-500);
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

  &__actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
  }
}
</style>
