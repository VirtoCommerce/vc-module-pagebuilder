<template>
  <aside
    class="pd-rules"
    :aria-label="$t('PAGE_BUILDER.DESIGNER.RULES.TITLE')"
  >
    <header class="pd-rules__head">
      <VcIcon
        icon="lucide-split"
        size="s"
        class="pd-rules__muted"
      />
      <h3 class="pd-rules__title">{{ $t("PAGE_BUILDER.DESIGNER.RULES.TITLE") }}</h3>
      <VcButton
        variant="ghost"
        size="icon-sm"
        icon="lucide-x"
        :aria-label="$t('PAGE_BUILDER.DESIGNER.DESELECT')"
        :title="$t('PAGE_BUILDER.DESIGNER.DESELECT')"
        @click="emit('close')"
      />
    </header>

    <div class="pd-rules__body">
      <RuleForm
        v-if="editing"
        :type="type"
        :rule="editing.rule"
        :is-new="editing.index < 0"
        :keys="keys.filter((key) => pageTypeOf(key) === type)"
        :catalog="catalog"
        :names="names"
        @save="saveRule"
        @cancel="editing = undefined"
      />
      <!-- v-show: the sortable list stays the same element -->
      <div v-show="!editing">
        <!-- the rules that pick this template -->
        <section class="pd-rules__section">
          <h4 class="pd-rules__heading">{{ $t("PAGE_BUILDER.DESIGNER.RULES.APPLIES_TO", { name: templateName(templateKey) }) }}</h4>
          <p
            v-if="!isAlternate"
            class="pd-rules__note"
          >
            {{ $t("PAGE_BUILDER.DESIGNER.RULES.APPLIES_DEFAULT") }}
          </p>
          <template v-else>
            <ul
              v-if="mine.length"
              class="pd-rules__list"
            >
              <li
                v-for="index in mine"
                :key="index"
                class="pd-rules__rule pd-rules__rule--flat"
                :class="{ 'pd-rules__rule--off': list[index].disabled }"
              >
                <span class="pd-rules__number">{{ index + 1 }}</span>
                <button
                  type="button"
                  class="pd-rules__text"
                  @click="edit(index)"
                >
                  <template
                    v-for="(part, i) in describe(list[index])"
                    :key="i"
                  >
                    <strong v-if="part.strong">{{ part.text }}</strong>
                    <template v-else>{{ part.text }}</template>
                  </template>
                </button>
              </li>
            </ul>
            <p
              v-else
              class="pd-rules__note"
            >
              {{ $t("PAGE_BUILDER.DESIGNER.RULES.APPLIES_NONE") }}
            </p>
            <VcButton
              size="sm"
              variant="secondary"
              icon="lucide-plus"
              class="pd-rules__add"
              @click="add"
            >
              {{ $t("PAGE_BUILDER.DESIGNER.RULES.ADD") }}
            </VcButton>
          </template>
        </section>

        <!-- one click from what the previewed product (category, page) belongs to -->
        <section
          v-if="isAlternate && suggestions.length"
          class="pd-rules__section"
        >
          <h4 class="pd-rules__heading">{{ $t("PAGE_BUILDER.DESIGNER.RULES.USE_FOR") }}</h4>
          <p class="pd-rules__note">{{ $t("PAGE_BUILDER.DESIGNER.RULES.USE_FOR_HINT", { name: previewed?.name ?? "" }) }}</p>
          <div class="pd-rules__chips">
            <button
              v-for="suggestion in suggestions"
              :key="suggestion.label"
              type="button"
              class="pd-rules__chip"
              :title="suggestion.label"
              @click="useFor(suggestion.when)"
            >
              <VcIcon
                icon="lucide-plus"
                size="xs"
              />
              <span>{{ suggestion.label }}</span>
            </button>
          </div>
        </section>

        <!-- the page type's rules in order: the first that matches wins -->
        <section class="pd-rules__section">
          <h4 class="pd-rules__heading">
            <span>{{ $t(ALL[type]) }}</span>
            <button
              type="button"
              class="pd-rules__link"
              @click="openJson"
            >
              {{ $t("PAGE_BUILDER.DESIGNER.RULES.JSON") }}
            </button>
          </h4>
          <p class="pd-rules__note">{{ $t(list.length ? "PAGE_BUILDER.DESIGNER.RULES.FIRST_MATCH" : "PAGE_BUILDER.DESIGNER.RULES.EMPTY") }}</p>
          <ul
            ref="sortable"
            class="pd-rules__list"
          >
            <li
              v-for="(rule, index) in list"
              :key="keyOf(rule)"
              class="pd-rules__rule"
              :class="{
                'pd-rules__rule--off': rule.disabled,
                'pd-rules__rule--mine': rule.template?.trim() === templateKey,
                'pd-rules__rule--hit': resolvedHere && resolved?.rule === index,
              }"
            >
              <span
                class="pd-rules__handle"
                :title="$t('PAGE_BUILDER.DESIGNER.DRAG')"
              >
                <VcIcon
                  icon="lucide-grip-vertical"
                  size="s"
                />
              </span>
              <button
                type="button"
                class="pd-rules__text"
                :title="$t('PAGE_BUILDER.DESIGNER.RULES.EDIT')"
                @click="edit(index)"
              >
                <template
                  v-for="(part, i) in describe(rule)"
                  :key="i"
                >
                  <strong v-if="part.strong">{{ part.text }}</strong>
                  <template v-else>{{ part.text }}</template>
                </template>
                <span class="pd-rules__arrow">→</span>
                <strong>{{ templateName(rule.template?.trim() ?? "") }}</strong>
                <span
                  v-if="!templateExists(rule.template)"
                  class="pd-rules__badge"
                  :title="$t('PAGE_BUILDER.DESIGNER.RULES.NO_TEMPLATE_HINT')"
                  >{{ $t("PAGE_BUILDER.DESIGNER.RULES.NO_TEMPLATE") }}</span
                >
              </button>
              <VcSwitch
                class="pd-rules__switch"
                :model-value="!rule.disabled"
                :aria-label="$t('PAGE_BUILDER.DESIGNER.RULES.ON')"
                @update:model-value="setDisabled(index, !$event)"
              />
              <VcButton
                variant="ghost"
                size="icon-sm"
                icon="lucide-trash-2"
                :aria-label="$t('PAGE_BUILDER.DESIGNER.RULES.REMOVE')"
                :title="$t('PAGE_BUILDER.DESIGNER.RULES.REMOVE')"
                @click="remove(index)"
              />
            </li>
          </ul>
        </section>

        <!-- the storefront is the only evaluator: the preview says which template it picked, by which rule -->
        <section class="pd-rules__section">
          <h4 class="pd-rules__heading">{{ $t("PAGE_BUILDER.DESIGNER.RULES.TEST") }}</h4>
          <p
            v-if="resolvedHere && previewed"
            class="pd-rules__test"
          >
            <VcIcon
              icon="lucide-flask-conical"
              size="s"
            />
            <span>
              {{
                $t("PAGE_BUILDER.DESIGNER.RULES.TEST_RESULT", {
                  name: previewed.name,
                  template: templateName(resolved!.template),
                  why:
                    resolved!.rule === null
                      ? $t("PAGE_BUILDER.DESIGNER.RULES.TEST_DEFAULT")
                      : $t("PAGE_BUILDER.DESIGNER.RULES.TEST_RULE", { number: resolved!.rule + 1 }),
                })
              }}
            </span>
          </p>
          <p
            v-else
            class="pd-rules__note"
          >
            {{ $t("PAGE_BUILDER.DESIGNER.RULES.TEST_WAITING") }}
          </p>
          <p class="pd-rules__note">{{ $t("PAGE_BUILDER.DESIGNER.RULES.TEST_HINT") }}</p>
        </section>
      </div>
    </div>

    <VcPopup
      v-if="jsonText !== undefined"
      :model-value="true"
      modal-width="tw-max-w-3xl"
      :title="$t('PAGE_BUILDER.DESIGNER.RULES.JSON_TITLE')"
      @close="jsonText = undefined"
    >
      <template #content>
        <div class="pd-rules-json">
          <p>{{ $t("PAGE_BUILDER.DESIGNER.RULES.JSON_HINT", { path: rulesFile }) }}</p>
          <VcTextarea
            v-model="jsonText"
            class="pd-rules-json__code"
            :aria-label="$t('PAGE_BUILDER.DESIGNER.RULES.JSON_TITLE')"
            @update:model-value="jsonErrors = []"
          />
          <ul
            v-if="jsonErrors.length"
            class="pd-rules-json__errors"
            role="alert"
          >
            <li
              v-for="(error, i) in jsonErrors"
              :key="i"
            >
              <code>{{ error.path || $t("PAGE_BUILDER.DESIGNER.RULES.ROOT") }}</code>
              {{ $t(`PAGE_BUILDER.DESIGNER.RULES.ERRORS.${error.code}`, error.params ?? {}) }}
            </li>
          </ul>
          <div class="pd-rules-json__actions">
            <VcButton
              variant="secondary"
              icon="lucide-wand-sparkles"
              @click="formatJson"
            >
              {{ $t("PAGE_BUILDER.DESIGNER.RULES.FORMAT") }}
            </VcButton>
            <VcButton @click="applyJson">
              {{ $t("PAGE_BUILDER.DESIGNER.RULES.APPLY") }}
            </VcButton>
          </div>
        </div>
      </template>
    </VcPopup>
  </aside>
</template>

<script setup lang="ts">
// The template rules, shown in plain language: those picking the open template ("Applies to"), rules made in one
// click from what the previewed product belongs to, the page type's whole ordered list (drag to reorder, switch off,
// remove, click to edit), the preview's verdict (Test), and the file as JSON. Edits change the rules state; the
// designer's Save writes them as a draft and Publish puts them live.
import { computed, nextTick, reactive, ref, useTemplateRef, watch } from "vue";
import { useI18n } from "vue-i18n";
import { useSortable } from "@vueuse/integrations/useSortable";
import { notification, usePopup } from "@vc-shell/framework";
import { VcButton, VcIcon, VcPopup, VcSwitch, VcTextarea } from "@vc-shell/framework/ui";
import { productFacts, searchCategories, type CatalogContext, type NamedEntity, type ProductFacts } from "../api";
import { rulesPath } from "../history";
import { conditionsOf, formatRules, moveItem, parseRules, rulesFor, rulesOf, withRules, type RuleError, type TemplateRule } from "../rules";
import { alternateName, pageTypeOf, type PageType } from "../templates";
import type { Resolved } from "../preview";
import type { TemplateRulesState } from "../templateRules";
import RuleForm from "./RuleForm.vue";

const props = defineProps<{
  rules: TemplateRulesState;
  type: PageType;
  templateKey: string;
  /** the theme's templates */
  keys: string[];
  catalog: CatalogContext;
  theme: string;
  /** what the preview shows the template on: a product's or category's id and name, a page's permalink */
  previewed?: NamedEntity;
  resolved?: Resolved;
}>();
const emit = defineEmits<{ close: [] }>();

const { t } = useI18n({ useScope: "global" });
const { showConfirmation } = usePopup();

const ALL: Record<PageType, string> = {
  product: "PAGE_BUILDER.DESIGNER.RULES.ALL_PRODUCT",
  category: "PAGE_BUILDER.DESIGNER.RULES.ALL_CATEGORY",
  page: "PAGE_BUILDER.DESIGNER.RULES.ALL_PAGE",
};
const rulesFile = computed(() => rulesPath(props.theme));

const list = computed(() => rulesOf(props.rules.rules.value, props.type));
const setList = (next: TemplateRule[]) => (props.rules.rules.value = withRules(props.rules.rules.value, props.type, next));
const isAlternate = computed(() => props.templateKey.includes("."));
const mine = computed(() => rulesFor(props.rules.rules.value, props.templateKey));
const resolvedHere = computed(() => !!props.resolved && props.resolved.pageType === props.type);

// a stable key per rule object while it is shown (rules have no ids)
const ruleKeys = new WeakMap<object, number>();
let lastKey = 0;
const keyOf = (rule: TemplateRule) => {
  if (!ruleKeys.has(rule)) ruleKeys.set(rule, ++lastKey);
  return ruleKeys.get(rule)!;
};

const templateName = (key: string) => (key.includes(".") ? alternateName(key) : t("PAGE_BUILDER.DESIGNER.TEMPLATES.DEFAULT"));
const templateExists = (key?: string) => !!key && (!key.trim().includes(".") || props.keys.includes(key.trim()));

// ── plain language ─────────────────────────────────────────────────────────

/** the names of the categories rules name */
const names = reactive(new Map<string, string>());
watch(
  list,
  async (rules) => {
    const ids = [...new Set(rules.flatMap((x) => (Array.isArray(x.when?.inCategory) ? (x.when.inCategory as unknown[]) : [])))].filter(
      (id): id is string => typeof id === "string" && !names.has(id),
    );
    if (!ids.length) return;
    for (const category of await searchCategories(props.catalog, undefined, ids).catch(() => [])) names.set(category.id, category.name);
  },
  { immediate: true },
);

type Part = { text: string; strong?: boolean };
const CONDITION_KEYS: Record<string, string> = {
  inCategory: "IS.IN_CATEGORY",
  productType: "IS.PRODUCT_TYPE",
  property: "IS.PROPERTY",
  path: "IS.PATH",
  language: "IS.LANGUAGE",
  source: "IS.SOURCE",
  setting: "IS.SETTING",
};
const SUBJECTS: Record<PageType, string> = { product: "SUBJECT_PRODUCT", category: "SUBJECT_CATEGORY", page: "SUBJECT_PAGE" };
const ALWAYS: Record<PageType, string> = { product: "ALWAYS_PRODUCT", category: "ALWAYS_CATEGORY", page: "ALWAYS_PAGE" };

function valueText(name: string, value: unknown): string {
  if (name === "inCategory") return (Array.isArray(value) ? value : [value]).map((id) => names.get(String(id)) ?? String(id)).join(", ");
  if (name === "property" || name === "setting") {
    const named = (value ?? {}) as { name?: unknown; equals?: unknown };
    return `${String(named.name ?? "")} = ${String(named.equals ?? "")}`;
  }
  if (name === "source" && typeof value === "string" && ["designer", "file", "blog"].includes(value)) return t(`PAGE_BUILDER.DESIGNER.RULES.SOURCES.${value.toUpperCase()}`);
  return String(value);
}

/** a message with its {value} in bold */
function withValue(key: string, value: string): Part[] {
  const [before, after = ""] = t(`PAGE_BUILDER.DESIGNER.RULES.${key}`, { value: "\u0001" }).split("\u0001");
  return [{ text: before }, { text: value, strong: true }, { text: after }];
}

/** "Products in Printers and below and of type Physical" (custom conditions as their JSON) */
function describe(rule: TemplateRule): Part[] {
  const { known, custom } = conditionsOf(rule, props.type);
  const conditions = [
    ...known.map(([name, value]) => withValue(CONDITION_KEYS[name], valueText(name, value))),
    ...custom.map(([name, value]) => [{ text: `${name}: ` }, { text: JSON.stringify(value), strong: true }]),
  ];
  if (!conditions.length) return [{ text: t(`PAGE_BUILDER.DESIGNER.RULES.${ALWAYS[props.type]}`) }];
  return [
    { text: `${t(`PAGE_BUILDER.DESIGNER.RULES.${SUBJECTS[props.type]}`)} ` },
    ...conditions.flatMap((parts, i) => (i ? [{ text: t("PAGE_BUILDER.DESIGNER.RULES.AND") }, ...parts] : parts)),
  ];
}

// ── editing ────────────────────────────────────────────────────────────────

/** the rule the form edits: its index (-1: a new one, added at the end) */
const editing = ref<{ index: number; rule: TemplateRule }>();
const add = () => (editing.value = { index: -1, rule: { template: props.templateKey, when: {} } });
const edit = (index: number) => (editing.value = { index, rule: list.value[index] });
function saveRule(rule: TemplateRule) {
  const index = editing.value?.index ?? -1;
  setList(index < 0 ? [...list.value, rule] : list.value.map((x, i) => (i === index ? rule : x)));
  editing.value = undefined;
}
const setDisabled = (index: number, disabled: boolean) =>
  setList(
    list.value.map((rule, i) => {
      if (i !== index) return rule;
      const { disabled: _disabled, ...on } = rule;
      return disabled ? { ...on, disabled: true } : on;
    }),
  );
async function remove(index: number) {
  if (await showConfirmation(t("PAGE_BUILDER.DESIGNER.RULES.CONFIRM_REMOVE"))) setList(list.value.filter((_, i) => i !== index));
}

// drag by the handle; Sortable moves the DOM, the rules are the truth: put the DOM back and move the data
const sortable = useTemplateRef<HTMLElement>("sortable");
useSortable(sortable, [], {
  handle: ".pd-rules__handle",
  animation: 150,
  onUpdate: (e: { oldIndex?: number; newIndex?: number; item: HTMLElement; from: HTMLElement }) => {
    if (e.oldIndex == null || e.newIndex == null) return;
    const { oldIndex, newIndex, item, from } = e;
    from.removeChild(item);
    from.insertBefore(item, from.children[oldIndex] ?? null);
    nextTick(() => setList(moveItem(list.value, oldIndex, newIndex)));
  },
});

// ── "Use this template for…": rules from what the previewed entity belongs to ──

const facts = ref<ProductFacts>();
watch(
  () => [props.type, props.previewed?.id] as const,
  async ([type, id]) => {
    facts.value = undefined;
    if (type === "product" && id) facts.value = await productFacts(props.catalog, id).catch(() => undefined);
  },
  { immediate: true },
);

const suggestions = computed<{ label: string; when: Record<string, unknown> }[]>(() => {
  const previewed = props.previewed;
  if (!previewed) return [];
  const label = (key: string, params: Record<string, string>) => t(`PAGE_BUILDER.DESIGNER.RULES.${key}`, params);
  if (props.type === "category") return [{ label: label("USE_FOR_THIS_CATEGORY", { name: previewed.name }), when: { inCategory: [previewed.id] } }];
  if (props.type === "page") {
    const folder = previewed.id.replace(/\/[^/]*$/, "");
    return [
      { label: label("USE_FOR_PATH", { value: previewed.id }), when: { path: previewed.id } },
      ...(folder ? [{ label: label("USE_FOR_PATH", { value: `${folder}/*` }), when: { path: `${folder}/*` } }] : []),
    ];
  }
  const product = facts.value;
  if (!product) return [];
  const properties = new Map(product.properties.map((x) => [`${x.name}=${String(x.value)}`, x]));
  return [
    ...product.categories.map((x) => ({ label: label("USE_FOR_CATEGORY", { name: x.name }), when: { inCategory: [x.id] } })),
    ...(product.productType ? [{ label: label("USE_FOR_TYPE", { value: product.productType }), when: { productType: product.productType } }] : []),
    ...[...properties.values()]
      .slice(0, 8)
      .map((x) => ({ label: label("USE_FOR_PROPERTY", { name: x.label || x.name, value: String(x.value) }), when: { property: { name: x.name, equals: x.value } } })),
  ];
});

/** a rule for the open template, first: it is about this very product, so nothing broader before it may win */
function useFor(when: Record<string, unknown>) {
  for (const id of Array.isArray(when.inCategory) ? when.inCategory : []) {
    const category = facts.value?.categories.find((x) => x.id === id) ?? (props.previewed?.id === id ? props.previewed : undefined);
    if (category) names.set(category.id, category.name);
  }
  const rule: TemplateRule = { template: props.templateKey, when };
  setList([rule, ...list.value]);
  notification.success(t("PAGE_BUILDER.DESIGNER.RULES.ADDED", { text: describe(rule).map((x) => x.text).join("") }));
}

// ── Edit as JSON ───────────────────────────────────────────────────────────

const jsonText = ref<string>();
const jsonErrors = ref<RuleError[]>([]);
function openJson() {
  jsonErrors.value = [];
  jsonText.value = formatRules(props.rules.rules.value);
}
function formatJson() {
  const { rules, errors } = parseRules(jsonText.value ?? "");
  // a file with mistakes is formatted too, as long as it is JSON
  if (rules || errors[0]?.code !== "SYNTAX") jsonText.value = JSON.stringify(JSON.parse(jsonText.value ?? ""), null, 2);
  jsonErrors.value = errors;
}
function applyJson() {
  const { rules, errors } = parseRules(jsonText.value ?? "");
  jsonErrors.value = errors;
  if (!rules) return;
  props.rules.rules.value = rules;
  jsonText.value = undefined;
}
</script>

<style lang="scss" scoped>
.pd-rules {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
  font-size: 13px;

  &__head {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 12px 12px 12px 16px;
    border-bottom: 1px solid var(--neutrals-200);
  }

  &__title {
    flex: 1;
    margin: 0;
    font-size: 14px;
    font-weight: 600;
    color: var(--neutrals-900);
  }

  &__muted {
    color: var(--neutrals-500);
  }

  &__body {
    flex: 1;
    min-height: 0;
    overflow-y: auto;
    padding: 16px;
  }

  &__section + &__section {
    margin-top: 20px;
  }

  &__heading {
    display: flex;
    align-items: center;
    gap: 8px;
    margin: 0 0 6px;
    color: var(--neutrals-700);
    font-size: 11px;
    font-weight: 600;
    letter-spacing: 0.04em;
    text-transform: uppercase;

    span {
      flex: 1;
    }
  }

  &__link {
    padding: 0;
    border: 0;
    background: none;
    color: var(--primary-600);
    font: inherit;
    font-size: 12px;
    font-weight: 500;
    letter-spacing: 0;
    text-transform: none;
    cursor: pointer;

    &:hover {
      text-decoration: underline;
    }
  }

  &__note {
    margin: 0 0 8px;
    color: var(--neutrals-500);
    line-height: 18px;
  }

  &__list {
    display: flex;
    flex-direction: column;
    gap: 4px;
    margin: 0;
    padding: 0;
    list-style: none;
  }

  &__rule {
    display: flex;
    align-items: flex-start;
    gap: 4px;
    padding: 6px 4px;
    border: 1px solid var(--neutrals-200);
    border-radius: 8px;
    background: var(--additional-50);

    &--flat {
      align-items: baseline;
      padding: 6px 8px;
    }

    &--mine {
      border-color: var(--primary-200);
    }

    &--hit {
      box-shadow: 0 0 0 2px var(--success-200);
    }

    &--off .pd-rules__text {
      color: var(--neutrals-400);
    }
  }

  &__number {
    flex: none;
    color: var(--neutrals-500);
    font-size: 12px;
    font-variant-numeric: tabular-nums;
  }

  &__handle {
    flex: none;
    display: flex;
    padding-top: 2px;
    color: var(--neutrals-400);
    cursor: grab;
  }

  &__text {
    flex: 1;
    min-width: 0;
    padding: 0;
    border: 0;
    background: none;
    color: var(--neutrals-900);
    font: inherit;
    line-height: 18px;
    text-align: left;
    overflow-wrap: anywhere;
    cursor: pointer;

    strong {
      font-weight: 600;
    }
  }

  &__arrow {
    margin: 0 4px;
    color: var(--neutrals-500);
  }

  &__badge {
    display: inline-block;
    margin-left: 4px;
    padding: 0 6px;
    border-radius: 999px;
    background: var(--warning-50);
    color: var(--warning-700);
    font-size: 11px;
  }

  &__switch {
    flex: none;
    width: auto;
    margin-top: 1px;
  }

  &__add {
    margin-top: 4px;
  }

  &__chips {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
  }

  &__chip {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    max-width: 100%;
    padding: 4px 10px 4px 8px;
    border: 1px solid var(--primary-200);
    border-radius: 999px;
    background: var(--primary-50);
    color: var(--primary-700);
    font: inherit;
    font-size: 12px;
    cursor: pointer;

    span {
      overflow: hidden;
      white-space: nowrap;
      text-overflow: ellipsis;
    }

    &:hover {
      background: var(--primary-100);
    }
  }

  &__test {
    display: flex;
    gap: 6px;
    margin: 0 0 8px;
    padding: 8px 10px;
    border-radius: 8px;
    background: var(--success-50);
    color: var(--success-700);
    line-height: 18px;

    :deep(.vc-icon) {
      flex: none;
      margin-top: 1px;
    }
  }
}

.pd-rules-json {
  display: flex;
  flex-direction: column;
  gap: 12px;
  width: min(720px, 80vw);

  p {
    margin: 0;
    color: var(--neutrals-600);
  }

  &__code :deep(textarea) {
    min-height: 360px;
    font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
    font-size: 12px;
    line-height: 18px;
    white-space: pre;
  }

  &__errors {
    margin: 0;
    padding: 8px 12px 8px 28px;
    border-radius: 8px;
    background: var(--danger-50);
    color: var(--danger-700);

    code {
      margin-right: 4px;
      font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
      font-weight: 600;
    }
  }

  &__actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
  }
}
</style>
