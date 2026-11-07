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
        :label="label(field)"
        :placeholder="placeholder(field)"
        :hint="field.info"
        :required="field.required"
        :class="{ 'pd-fields__code': field.type === 'markdown' }"
        @update:model-value="setText(field, $event)"
      />
      <VcEditor
        v-else-if="field.type === 'text'"
        :toolbar="['bold', 'italic', 'underline', 'link', 'bulletList', 'orderedList']"
        :custom-buttons="anchorPicker"
        :model-value="text(field)"
        :label="label(field)"
        :placeholder="placeholder(field)"
        :hint="field.info"
        @update:model-value="setText(field, $event)"
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
      <!-- a store link list, by its name -->
      <div
        v-else-if="field.type === 'menu'"
        class="pd-fields__select"
      >
        <VcSelect
          :model-value="values[field.id!]"
          :label="field.label"
          :placeholder="$t('PAGE_BUILDER.DESIGNER.MENU_PLACEHOLDER')"
          :options="menuOptions(storeMenus ?? [], values[field.id!])"
          :loading="storeMenus === undefined"
          option-value="value"
          option-label="label"
          searchable
          clearable
          emit-value
          @update:model-value="set(field, $event ?? null)"
        />
        <VcHint v-if="field.info">{{ field.info }}</VcHint>
        <VcHint v-if="menuFailed">{{ $t("PAGE_BUILDER.DESIGNER.MENUS_FAILED") }}</VcHint>
        <VcHint
          v-if="isOverridden(overridden, menuField(group))"
          class="pd-fields__overridden"
        >
          {{ $t("PAGE_BUILDER.DESIGNER.WHITE_LABELING") }}
        </VcHint>
      </div>
      <!-- a palette token, so that the color follows the light and dark theme; no color of its own -->
      <div
        v-else-if="field.type === 'color-token'"
        class="pd-fields__select"
      >
        <div class="pd-fields__token">
          <span
            class="pd-fields__swatch"
            :style="{ background: palette?.colors[String(values[field.id!])] }"
          />
          <VcSelect
            :model-value="values[field.id!]"
            :label="field.label"
            :placeholder="$t('PAGE_BUILDER.DESIGNER.TOKEN_PLACEHOLDER')"
            :options="tokenOptions(palette?.tokens ?? [], palette?.colors ?? {}, values[field.id!])"
            :option-value="(x: TokenOption) => x.value"
            :option-label="(x: TokenOption) => (x.unknown ? `${x.label} · ${$t('PAGE_BUILDER.DESIGNER.THEME.UNKNOWN_TOKEN')}` : x.label)"
            searchable
            :clearable="!field.required"
            emit-value
            @update:model-value="set(field, $event ?? null)"
          />
        </div>
        <VcHint v-if="field.info">{{ field.info }}</VcHint>
        <VcHint v-if="!palette?.tokens.length">{{ $t("PAGE_BUILDER.DESIGNER.THEME.TOKENS_PENDING") }}</VcHint>
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
          :group="group"
          :translation="translation"
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
            :group="group"
            :translation="translation"
            :fields="field.element ?? []"
            :values="rowOf(row)"
            :context="{ ...context, item: listOf(field), index, element: row, parent: context }"
            @update="(id, value) => setRow(field, index, { ...rowOf(row), [id]: value })"
          />
        </details>
      </div>

      <!-- the Angular designer's search: a query (or a button) runs the field's request; its answer is the setting's value -->
      <div
        v-else-if="field.type === 'search'"
        class="pd-fields__select"
      >
        <VcButton
          v-if="(field as SearchField).button"
          variant="secondary"
          size="sm"
          icon="lucide-search"
          :loading="searching.has(field)"
          @click="search(field as SearchField, null)"
        >
          {{ (field as SearchField).button === true ? $t("PAGE_BUILDER.DESIGNER.SEARCH.BUTTON") : (field as SearchField).button }}
        </VcButton>
        <VcInput
          v-else
          :model-value="searchQuery(field)"
          :label="field.label"
          :placeholder="field.placeholder"
          :loading="searching.has(field)"
          clearable
          @update:model-value="typed(field as SearchField, $event)"
        />
        <VcHint v-if="field.info">{{ field.info }}</VcHint>
        <VcHint v-if="searchFailed.has(field)">{{ $t("PAGE_BUILDER.DESIGNER.SEARCH.FAILED") }}</VcHint>
        <p
          v-else-if="searchValue(field)?.__nodata"
          class="pd-fields__note"
        >
          {{ (field as SearchField).nodataText ?? $t("PAGE_BUILDER.DESIGNER.SEARCH.NO_DATA") }}
        </p>
        <dl
          v-else-if="searchValue(field) && (field as SearchField).displayInfo?.length"
          class="pd-fields__found"
        >
          <template
            v-for="row in (field as SearchField).displayInfo"
            :key="row.label"
          >
            <dt>{{ row.label }}</dt>
            <dd>
              <img
                v-if="row.type === 'image' && typeof searchDisplayValue(values[field.id!], row) === 'string'"
                :src="String(searchDisplayValue(values[field.id!], row))"
                alt=""
              />
              <template v-else>{{ shownValue(searchDisplayValue(values[field.id!], row)) }}</template>
            </dd>
          </template>
        </dl>
        <pre
          v-else-if="searchValue(field)"
          class="pd-fields__found pd-fields__code"
          >{{ JSON.stringify(searchValue(field)?.value ?? null, null, 2) }}</pre
        >
      </div>

      <VcInput
        v-else
        :model-value="text(field)"
        :label="label(field)"
        :placeholder="placeholder(field)"
        :hint="field.info"
        @update:model-value="setText(field, $event)"
      />

      <!-- a translatable text: which language it is, and the others' -->
      <p
        v-if="translationNote(field)"
        class="pd-fields__translation"
      >
        {{ translationNote(field) }}
      </p>
    </template>
  </div>
</template>

<script setup lang="ts">
// A schema's fields as form controls (the Angular designer's control registry), editing `values`; nested objects
// and lists render this component again. Changes go up as update(id, value): nothing is mutated here.
import { computed, inject, onBeforeUnmount, onMounted, reactive, ref } from "vue";
import { useI18n } from "vue-i18n";
import { VcButton, VcColorInput, VcEditor, VcHint, VcIcon, VcInput, VcSelect, VcSwitch, VcTextarea, type CustomToolbarButton, type CustomToolbarItem } from "@vc-shell/framework/ui";
import { PageAnchorsKey, PaletteTokensKey, StoreMenusKey, WhiteLabelingKey, type Field, type RemoteRequest, type SectionGroup } from "../types";
import { menuOptions } from "../storeMenus";
import { tokenOptions, type TokenOption } from "../themeLook";
import { isOverridden, menuField } from "../whiteLabeling";
import { fill, isVisible, pick, schemaRequest, valuesOf } from "../schema";
import { translationOf, withTranslation, type Translating } from "../translatable";
import { SEARCH_DEBOUNCE_MS, requestResult, runSearch, searchDisplayValue, type SearchField, type SearchValue } from "../search";
import ImageField from "./ImageField.vue";
import { getAuthHeaders } from "../../page-builder/composables/usePageContentApi";

const props = defineProps<{
  fields: Field[];
  values: Record<string, unknown>;
  /** what visibility expressions see as `this` */
  context: Record<string, unknown>;
  /** the section group of the item edited: its `menu` settings are white-labeling's to replace */
  group?: SectionGroup;
  /** the item is a theme file's: its translatable texts are edited in this language */
  translation?: Translating;
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

const palette = inject(PaletteTokensKey, undefined);
const overridden = inject(WhiteLabelingKey, undefined);

// the store's link lists, read once the fields show a `menu` setting
const loadStoreMenus = inject(StoreMenusKey, undefined);
const storeMenus = ref<unknown[]>();
const menuFailed = ref(false);
onMounted(async () => {
  if (!props.fields.some((x) => x.type === "menu")) return;
  try {
    storeMenus.value = loadStoreMenus ? await loadStoreMenus() : [];
  } catch {
    storeMenus.value = [];
    menuFailed.value = true;
  }
});

const shown = computed(() => props.fields.filter((field) => isVisible(field, props.context)));

const set = (field: Field, value: unknown) => field.id && emit("update", field.id, value);

// a translatable text of a theme file: its own text in the language edited, the others' kept
const TEXT_TYPES = ["string", "markdown", "text"];
const translated = (field: Field) => (props.translation && field.translatable && TEXT_TYPES.includes(field.type) ? props.translation : undefined);
const text = (field: Field) => {
  const value = props.values[field.id!];
  const at = translated(field);
  if (at) return translationOf(value, at).own ?? "";
  return value == null ? "" : String(value);
};
function setText(field: Field, value: unknown) {
  const at = translated(field);
  set(field, at ? withTranslation(props.values[field.id!], at, String(value ?? "")) : value);
}
// the language shows only where the store has several
const manyLanguages = computed(() => (props.translation?.languages?.length ?? 0) > 1);
const label = (field: Field) => (translated(field) && manyLanguages.value && field.label ? `${field.label} · ${props.translation!.language}` : field.label);
/** a language without its own text shows the text it falls back to */
const placeholder = (field: Field) => {
  const at = translated(field);
  return (at && translationOf(props.values[field.id!], at).fallback?.text) || field.placeholder;
};
function translationNote(field: Field): string | undefined {
  const at = translated(field);
  if (!at || !manyLanguages.value) return undefined;
  const state = translationOf(props.values[field.id!], at);
  if (state.fallback?.language) return t("PAGE_BUILDER.DESIGNER.TRANSLATION.FALLBACK", { language: at.language, fallback: state.fallback.language });
  if (state.fallback) return t("PAGE_BUILDER.DESIGNER.TRANSLATION.SAME_EVERYWHERE");
  const others = state.languages.filter((x) => x.toLowerCase() !== at.language.toLowerCase());
  return others.length ? t("PAGE_BUILDER.DESIGNER.TRANSLATION.OTHER_LANGUAGES", { languages: others.join(", ") }) : t("PAGE_BUILDER.DESIGNER.TRANSLATION.SAME_EVERYWHERE");
}
const objectOf = (field: Field) => ({ ...valuesOf(field.element ?? []), ...((props.values[field.id!] as object) ?? {}) }) as Record<string, unknown>;
const listOf = (field: Field) => (Array.isArray(props.values[field.id!]) ? (props.values[field.id!] as unknown[]) : []);
const rowOf = (row: unknown) => (row && typeof row === "object" ? (row as Record<string, unknown>) : {});

// a translatable label shows in the language edited
const shownText = (value: unknown) => (props.translation && value && typeof value === "object" ? translationOf(value, props.translation).own ?? translationOf(value, props.translation).fallback?.text : value);
function rowTitle(field: Field, row: unknown, index: number) {
  const byField = field.displayField ? shownText(pick(row, field.displayField)) : undefined;
  const first = (field.element ?? [])
    .filter((x) => x.type === "string")
    .map((x) => shownText(pick(row, x.id!)))
    .find((x) => typeof x === "string" && x.trim());
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
/** A schema's request with the context filled in; none when its URL isn't http(s) */
async function schemaFetch(request: RemoteRequest, context: Record<string, unknown>): Promise<Response | undefined> {
  const target = schemaRequest(fill(request.url, context), window.location);
  if (!target) return undefined;
  const body = fill(request.body, context);
  return fetch(target.url, {
    method: (request.method ?? "get").toUpperCase(),
    // the platform's API wants the user's token (a store's link lists, say); the theme's requests elsewhere go without it
    headers: { ...(target.platform ? await getAuthHeaders() : {}), ...(body ? { "Content-Type": "application/json" } : {}) },
    body: body ? JSON.stringify(body) : undefined,
  });
}

function createLoader(field: Field) {
  return async (keyword?: string, _skip?: number, ids?: string[]) => {
    const request = field.request!;
    const res = await schemaFetch(request, { ...props.context, __searchQuery: keyword ?? "" });
    if (!res) return { results: [], totalCount: 0 };
    const json = res.ok ? await res.json() : undefined;
    let results = requestResult(request, json) as Record<string, unknown>[] | undefined;
    results = Array.isArray(results) ? results : [];
    if (ids?.length) results = results.filter((x) => ids.includes(String(x[request.value ?? "value"])));
    // rows keyed by request.label / request.value (option-label / option-value say which)
    return { results: results as unknown as NonNullable<Field["options"]>, totalCount: results.length };
  };
}

// a `search` field: its query runs once typing pauses; a later query wins over an answer still on its way
const searching = reactive(new Set<Field>());
const searchFailed = reactive(new Set<Field>());
const searchRuns = new Map<Field, number>();
const pendingQueries = new Map<Field, ReturnType<typeof setTimeout>>();
const typedQueries = reactive(new Map<Field, string>());
onBeforeUnmount(() => pendingQueries.forEach(clearTimeout));

const searchValue = (field: Field) => {
  const value = props.values[field.id!];
  return value && typeof value === "object" ? (value as SearchValue) : undefined;
};
const searchQuery = (field: Field) => typedQueries.get(field) ?? searchValue(field)?.__searchQuery ?? "";
const shownValue = (value: unknown) => (value == null ? "" : typeof value === "object" ? JSON.stringify(value) : String(value));

function typed(field: SearchField, query: unknown) {
  const text = query == null ? "" : String(query);
  typedQueries.set(field, text);
  clearTimeout(pendingQueries.get(field));
  pendingQueries.set(
    field,
    setTimeout(() => search(field, text), field.debounceTime || SEARCH_DEBOUNCE_MS),
  );
}

async function search(field: SearchField, query: string | null) {
  const run = (searchRuns.get(field) ?? 0) + 1;
  searchRuns.set(field, run);
  searching.add(field);
  searchFailed.delete(field);
  try {
    const value = await runSearch(field, props.context, query, async (request, context) => {
      const res = await schemaFetch(request, context);
      if (!res?.ok) throw new Error(`The search request failed: ${res?.status ?? "not an http(s) URL"}`);
      return requestResult(request, await res.json());
    });
    if (searchRuns.get(field) === run) set(field, value);
  } catch (e) {
    if (searchRuns.get(field) !== run) return;
    console.warn("[page-builder] A search field's request failed", e);
    searchFailed.add(field);
    set(field, null);
  } finally {
    if (searchRuns.get(field) === run) searching.delete(field);
    // the query typed shows until the answer to it is in
    if (query !== null && typedQueries.get(field) === query) typedQueries.delete(field);
  }
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

  &__token {
    display: flex;
    align-items: flex-end;
    gap: 8px;

    > :last-child {
      flex: 1;
      min-width: 0;
    }
  }

  // the token's color in the theme's light palette; empty when the palette leaves it to the storefront's preset
  &__swatch {
    flex: none;
    width: 28px;
    height: 28px;
    margin-bottom: 4px;
    border: 1px solid var(--neutrals-300);
    border-radius: 6px;
    background-image: linear-gradient(45deg, var(--neutrals-100) 25%, transparent 25%, transparent 75%, var(--neutrals-100) 75%);
    background-size: 8px 8px;
  }

  &__overridden {
    color: var(--warning-600);
  }

  // under its control: the gap between fields is the next field's
  &__translation {
    margin: -10px 0 0;
    font-size: 12px;
    color: var(--neutrals-500);
  }

  // what a search found
  &__found {
    margin: 0;
    padding: 8px;
    border: 1px solid var(--neutrals-200);
    border-radius: 6px;
    font-size: 12px;
    white-space: pre-wrap;
    word-break: break-word;

    dt {
      color: var(--neutrals-500);
    }

    dd {
      margin: 0 0 6px;
      color: var(--neutrals-900);
    }

    img {
      max-width: 100%;
      max-height: 120px;
    }
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
