<template>
  <div class="pd-fields">
    <template
      v-for="field in shown"
      :key="field.id ?? field.label"
    >
      <p
        v-if="field.type === 'header'"
        class="pd-fields__header"
      >
        {{ field.label }}
      </p>
      <p
        v-else-if="field.type === 'paragraph'"
        class="pd-fields__note"
      >
        {{ field.label ?? field.info }}
      </p>

      <VcTextarea
        v-else-if="(field.type === 'string' && field.multiline) || field.type === 'markdown'"
        :model-value="text(field)"
        :label="field.label"
        :placeholder="field.placeholder"
        :hint="field.info"
        :required="field.required"
        :class="{ 'pd-fields__code': field.type === 'markdown' }"
        @update:model-value="set(field, $event)"
      />
      <VcEditor
        v-else-if="field.type === 'text'"
        :toolbar="['bold', 'italic', 'underline', 'link', 'bulletList', 'orderedList']"
        :custom-buttons="anchorPicker"
        :model-value="text(field)"
        :label="field.label"
        :placeholder="field.placeholder"
        :hint="field.info"
        @update:model-value="set(field, $event)"
      />
      <VcInput
        v-else-if="field.type === 'number' || field.type === 'slider'"
        type="number"
        :model-value="values[field.id!] as number | null"
        :label="field.label"
        :placeholder="field.placeholder"
        :hint="field.info"
        :required="field.required"
        @update:model-value="set(field, $event === '' || $event == null ? null : Number($event))"
      />
      <VcSwitch
        v-else-if="field.type === 'checkbox'"
        :model-value="!!(values[field.id!] ?? field.default)"
        :label="field.label"
        :hint="field.info"
        @update:model-value="set(field, $event)"
      />
      <!-- the hint below the box (VcSelect's own has one line's room, a longer one ran into the next field) -->
      <div
        v-else-if="field.type === 'select'"
        class="pd-fields__select"
      >
        <VcSelect
          :model-value="values[field.id!]"
          :label="field.label"
          :placeholder="field.placeholder"
          :required="field.required"
          :options="field.request ? remoteOptions(field) : (field.options ?? [])"
          :option-value="field.request?.value ?? 'value'"
          :option-label="field.request?.label ?? 'label'"
          :searchable="!!field.searchable || !!field.request"
          :clearable="!field.required"
          emit-value
          @update:model-value="set(field, $event ?? null)"
        />
        <VcHint v-if="field.info">{{ field.info }}</VcHint>
      </div>
      <VcColorInput
        v-else-if="field.type === 'color'"
        :model-value="values[field.id!] as string | null"
        :label="field.label"
        @update:model-value="set(field, $event)"
      />
      <ImageField
        v-else-if="field.type === 'images' || field.type === 'files'"
        :model-value="values[field.id!] as string | undefined"
        :label="field.label"
        :hint="field.info"
        :image="field.type === 'images'"
        @update:model-value="set(field, $event)"
      />
      <VcInput
        v-else-if="field.type === 'calendar'"
        type="date"
        :model-value="text(field).slice(0, 10)"
        :label="field.label"
        :hint="field.info"
        @update:model-value="set(field, $event || null)"
      />

      <fieldset
        v-else-if="field.type === 'object'"
        class="pd-fields__group"
      >
        <legend>{{ field.title ?? field.label }}</legend>
        <Fields
          :fields="field.element ?? []"
          :values="objectOf(field)"
          :context="{ ...context, item: objectOf(field), parent: context }"
          @update="(id, value) => set(field, { ...objectOf(field), [id]: value })"
        />
      </fieldset>

      <div
        v-else-if="field.type === 'list'"
        class="pd-fields__list"
      >
        <div class="pd-fields__list-head">
          <span class="pd-fields__label">{{ field.label }}</span>
          <VcButton
            variant="ghost"
            size="sm"
            icon="lucide-plus"
            @click="addRow(field)"
          >
            {{ $t("PAGE_BUILDER.DESIGNER.ADD_ITEM") }}
          </VcButton>
        </div>
        <details
          v-for="(row, index) in listOf(field)"
          :key="index"
          class="pd-fields__row"
          :open="openRow === `${field.id}:${index}`"
        >
          <summary @click.prevent="openRow = openRow === `${field.id}:${index}` ? undefined : `${field.id}:${index}`">
            <VcIcon
              icon="lucide-chevron-right"
              size="s"
              class="pd-fields__chevron"
            />
            <span class="pd-fields__row-title">{{ rowTitle(field, row, index) }}</span>
            <span class="pd-fields__row-actions">
              <VcButton
                variant="ghost"
                size="icon-sm"
                icon="lucide-arrow-up"
                :disabled="index === 0"
                :aria-label="$t('PAGE_BUILDER.DESIGNER.MOVE_UP')"
                @click.stop="moveRow(field, index, -1)"
              />
              <VcButton
                variant="ghost"
                size="icon-sm"
                icon="lucide-arrow-down"
                :disabled="index === listOf(field).length - 1"
                :aria-label="$t('PAGE_BUILDER.DESIGNER.MOVE_DOWN')"
                @click.stop="moveRow(field, index, 1)"
              />
              <VcButton
                variant="ghost"
                size="icon-sm"
                icon="lucide-trash-2"
                :aria-label="$t('PAGE_BUILDER.DESIGNER.REMOVE')"
                @click.stop="removeRow(field, index)"
              />
            </span>
          </summary>
          <Fields
            v-if="openRow === `${field.id}:${index}`"
            class="pd-fields__row-body"
            :fields="field.element ?? []"
            :values="rowOf(row)"
            :context="{ ...context, item: listOf(field), index, element: row, parent: context }"
            @update="(id, value) => setRow(field, index, { ...rowOf(row), [id]: value })"
          />
        </details>
      </div>

      <VcInput
        v-else
        :model-value="text(field)"
        :label="field.label"
        :hint="field.info"
        @update:model-value="set(field, $event)"
      />
    </template>
  </div>
</template>

<script setup lang="ts">
// A schema's fields as form controls (the Angular designer's control registry), editing `values`; nested objects
// and lists render this component again. Changes go up as update(id, value): nothing is mutated here.
import { computed, inject, ref } from "vue";
import { useI18n } from "vue-i18n";
import { VcButton, VcColorInput, VcEditor, VcHint, VcIcon, VcInput, VcSelect, VcSwitch, VcTextarea, type CustomToolbarButton, type CustomToolbarItem } from "@vc-shell/framework/ui";
import { PageAnchorsKey, type Field } from "../types";
import { fill, isVisible, pick, valuesOf } from "../schema";
import ImageField from "./ImageField.vue";
import { getAuthHeaders } from "../../page-builder/composables/usePageContentApi";

const props = defineProps<{
  fields: Field[];
  values: Record<string, unknown>;
  /** what visibility expressions see as `this` */
  context: Record<string, unknown>;
}>();

/** the rich-text editor (tiptap) the toolbar items act on */
type Editor = Parameters<CustomToolbarButton["action"]>[0];

// rich text: a link to a place on the page (a section, a block, an anchor in some text), as #anchor; same tab (the
// editor's links open a new one by default)
const anchors = inject(PageAnchorsKey, undefined);
const anchorPicker = computed<CustomToolbarItem[]>(() =>
  anchors?.value.length
    ? [
        {
          id: "anchor",
          label: t("PAGE_BUILDER.DESIGNER.LINK_ANCHOR"),
          // the first option names the list; it shows while the selection has no link to an anchor
          options: [
            { value: "", label: t("PAGE_BUILDER.DESIGNER.LINK_ANCHOR"), action: () => undefined },
            ...anchors.value.map((anchor) => ({
              value: anchor.value,
              label: `${anchor.label} (#${anchor.value})`,
              action: (editor: Editor, value: string) =>
                editor.chain().focus().extendMarkRange("link").setMark("link", { href: `#${value}`, target: null, rel: null }).run(),
            })),
          ],
          getValue: (editor) => {
            const href = String(editor.getAttributes("link").href ?? "");
            return href.startsWith("#") ? href.slice(1) : "";
          },
        },
      ]
    : [],
);
const emit = defineEmits<{ update: [id: string, value: unknown] }>();

const { t } = useI18n({ useScope: "global" });
const openRow = ref<string>();

const shown = computed(() => props.fields.filter((field) => isVisible(field, props.context)));

const set = (field: Field, value: unknown) => field.id && emit("update", field.id, value);
const text = (field: Field) => {
  const value = props.values[field.id!];
  return value == null ? "" : String(value);
};
const objectOf = (field: Field) => ({ ...valuesOf(field.element ?? []), ...((props.values[field.id!] as object) ?? {}) }) as Record<string, unknown>;
const listOf = (field: Field) => (Array.isArray(props.values[field.id!]) ? (props.values[field.id!] as unknown[]) : []);
const rowOf = (row: unknown) => (row && typeof row === "object" ? (row as Record<string, unknown>) : {});

function rowTitle(field: Field, row: unknown, index: number) {
  const byField = field.displayField ? pick(row, field.displayField) : undefined;
  const first = (field.element ?? []).filter((x) => x.type === "string").map((x) => pick(row, x.id!)).find((x) => typeof x === "string" && x.trim());
  return String(byField || first || t("PAGE_BUILDER.DESIGNER.ITEM", { number: index + 1 }));
}

function addRow(field: Field) {
  const rows = listOf(field);
  set(field, [...rows, valuesOf(field.element ?? [])]);
  openRow.value = `${field.id}:${rows.length}`;
}
function setRow(field: Field, index: number, row: unknown) {
  set(field, listOf(field).map((x, i) => (i === index ? row : x)));
}
function removeRow(field: Field, index: number) {
  set(field, listOf(field).filter((_, i) => i !== index));
  openRow.value = undefined;
}
function moveRow(field: Field, index: number, by: number) {
  const rows = [...listOf(field)];
  rows.splice(index + by, 0, ...rows.splice(index, 1));
  set(field, rows);
  openRow.value = undefined;
}

// a select whose options come from a request (e.g. the categories of the store, by GraphQL); one loader per field,
// so the select doesn't reload whenever another field changes
const loaders = new Map<Field, (keyword?: string, skip?: number, ids?: string[]) => Promise<{ results: NonNullable<Field["options"]>; totalCount: number }>>();
function remoteOptions(field: Field) {
  if (!loaders.has(field)) loaders.set(field, createLoader(field));
  return loaders.get(field)!;
}
function createLoader(field: Field) {
  return async (keyword?: string, _skip?: number, ids?: string[]) => {
    const request = field.request!;
    const body = fill(request.body, { ...props.context, __searchQuery: keyword ?? "" });
    const res = await fetch(fill(request.url, props.context), {
      method: (request.method ?? "get").toUpperCase(),
      // the platform's API wants the signed-in user's token (a store's link lists, say)
      headers: { ...(await getAuthHeaders()), ...(body ? { "Content-Type": "application/json" } : {}) },
      body: body ? JSON.stringify(body) : undefined,
    });
    const json = res.ok ? await res.json() : undefined;
    let results = (request.response?.result ? pick(json, request.response.result) : json) as Record<string, unknown>[] | undefined;
    results = Array.isArray(results) ? results : [];
    if (ids?.length) results = results.filter((x) => ids.includes(String(x[request.value ?? "value"])));
    // rows keyed by request.label / request.value (option-label / option-value say which)
    return { results: results as unknown as NonNullable<Field["options"]>, totalCount: results.length };
  };
}
</script>

<style lang="scss" scoped>
// the rich-text editor's header (formatting buttons, then view buttons) is wider than the settings panel: let it wrap
:deep(.vc-editor__header) {
  flex-wrap: wrap;
}

// the rich-text toolbar's "Link to anchor" list, looking like the toolbar's buttons (VcEditor leaves it unstyled)
:deep(.vc-editor-dropdown select) {
  max-width: 11rem;
  height: 2rem;
  padding: 0 4px;
  border: 0;
  border-radius: 6px;
  background: transparent;
  color: var(--neutrals-600);
  font-size: 12px;
  cursor: pointer;

  &:hover {
    background: var(--neutrals-100);
  }
}

.pd-fields {
  display: flex;
  flex-direction: column;
  gap: 14px;

  // the controls' labels at the rail's size (the framework's are 14px)
  :deep(.vc-label__content) {
    font-size: 13px;
  }

  &__select {
    display: flex;
    flex-direction: column;
    gap: 4px;
  }

  // a group of settings, labelled as the tree labels its groups (HEADER, TEMPLATE…)
  &__header {
    margin: 10px 0 -4px;
    color: var(--neutrals-700);
    font-size: 11px;
    font-weight: 600;
    letter-spacing: 0.04em;
    text-transform: uppercase;
  }

  &__note {
    margin: 0;
    font-size: 13px;
    color: var(--neutrals-500);
  }

  &__code :deep(textarea) {
    font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
    font-size: 13px;
  }

  &__label {
    font-size: 13px;
    font-weight: 500;
    color: var(--neutrals-900);
  }

  &__group {
    margin: 0;
    padding: 12px;
    border: 1px solid var(--neutrals-200);
    border-radius: 8px;

    legend {
      padding: 0 4px;
      font-weight: 500;
      color: var(--neutrals-900);
    }
  }

  &__list {
    display: flex;
    flex-direction: column;
    gap: 6px;
  }

  &__list-head {
    display: flex;
    align-items: center;
    justify-content: space-between;
  }

  &__row {
    border: 1px solid var(--neutrals-200);
    border-radius: 8px;

    summary {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 6px 8px;
      list-style: none;
      cursor: pointer;

      &::-webkit-details-marker {
        display: none;
      }
    }

    &[open] .pd-fields__chevron {
      transform: rotate(90deg);
    }
  }

  &__chevron {
    color: var(--neutrals-500);
    transition: transform 0.15s;
  }

  &__row-title {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
    color: var(--neutrals-900);
  }

  &__row-actions {
    display: flex;
    opacity: 0.6;
  }

  &__row:hover &__row-actions {
    opacity: 1;
  }

  &__row-body {
    padding: 4px 12px 12px;
  }
}
</style>
