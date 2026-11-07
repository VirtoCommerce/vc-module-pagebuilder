<template>
  <VcDropdown
    v-model="open"
    class="pd-switch"
    floating
    placement="bottom-start"
    max-height="70vh"
    :aria-label="$t('PAGE_BUILDER.DESIGNER.TEMPLATES.SWITCH')"
  >
    <template #trigger="{ isActive, toggle }">
      <button
        type="button"
        class="pd-switch__trigger"
        :aria-expanded="isActive"
        :aria-label="$t('PAGE_BUILDER.DESIGNER.TEMPLATES.SWITCH')"
        :disabled="disabled"
        @click="toggle"
      >
        <VcIcon
          :icon="current ? TYPE_ICONS[pageTypeOf(current)!] : 'lucide-file-text'"
          size="s"
          class="pd-switch__icon"
        />
        <span class="pd-switch__kind">{{ current ? $t(TYPE_LABELS[pageTypeOf(current)!]) : $t("PAGE_BUILDER.DESIGNER.TEMPLATES.PAGES") }}</span>
        <strong class="pd-switch__name">{{ current ? nameOf(current) : page }}</strong>
        <VcIcon
          icon="lucide-chevron-down"
          size="xs"
          class="pd-switch__icon"
        />
      </button>
    </template>
    <template #items-container="{ close }">
      <div
        class="pd-switch__menu"
        role="menu"
      >
        <template v-if="page">
          <p class="pd-switch__group">{{ $t("PAGE_BUILDER.DESIGNER.TEMPLATES.PAGES") }}</p>
          <button
            type="button"
            role="menuitemradio"
            class="pd-switch__item"
            :class="{ 'pd-switch__item--on': !current }"
            :aria-checked="!current"
            @click="pick(null, close)"
          >
            <VcIcon
              icon="lucide-file-text"
              size="s"
            />
            <span>{{ page }}</span>
          </button>
        </template>
        <template
          v-for="type in PAGE_TYPES"
          :key="type"
        >
          <p class="pd-switch__group">{{ $t(TYPE_LABELS[type]) }}</p>
          <button
            v-for="key in keysOf(type)"
            :key="key"
            type="button"
            role="menuitemradio"
            class="pd-switch__item"
            :class="{ 'pd-switch__item--on': key === current }"
            :aria-checked="key === current"
            @click="pick(key, close)"
          >
            <VcIcon
              :icon="TYPE_ICONS[type]"
              size="s"
            />
            <span>{{ nameOf(key) }}</span>
            <small v-if="ruleCount(key)">{{ $t("PAGE_BUILDER.DESIGNER.TEMPLATES.RULE_COUNT", { count: ruleCount(key) }) }}</small>
          </button>
          <button
            type="button"
            role="menuitem"
            class="pd-switch__item pd-switch__item--create"
            @click="
              close();
              emit('create', type);
            "
          >
            <VcIcon
              icon="lucide-plus"
              size="s"
            />
            <span>{{ $t("PAGE_BUILDER.DESIGNER.TEMPLATES.CREATE") }}</span>
          </button>
        </template>
      </div>
    </template>
  </VcDropdown>
</template>

<script setup lang="ts">
// The canvas bar's switcher, as Shopify's theme editor has it: the page being designed, then each page type's
// templates (Default and its alternates) and "Create template". Picking one opens it in this designer.
import { ref } from "vue";
import { useI18n } from "vue-i18n";
import { VcDropdown, VcIcon } from "@vc-shell/framework/ui";
import { PAGE_TYPES, alternateName, pageTypeOf, type PageType } from "../templates";

const props = defineProps<{
  /** the template open, or null for the page */
  current: string | null;
  /** the page the designer was opened with (none: a template was) */
  page?: string;
  /** the theme's templates */
  keys: string[];
  /** how many rules pick each template */
  ruleCount: (key: string) => number;
  disabled?: boolean;
}>();
const emit = defineEmits<{ open: [key: string | null]; create: [type: PageType] }>();

const { t } = useI18n({ useScope: "global" });
const open = ref(false);

const TYPE_LABELS: Record<PageType, string> = {
  product: "PAGE_BUILDER.DESIGNER.TEMPLATES.PRODUCT",
  category: "PAGE_BUILDER.DESIGNER.TEMPLATES.CATEGORY",
  page: "PAGE_BUILDER.DESIGNER.TEMPLATES.PAGE",
};
const TYPE_ICONS: Record<PageType, string> = { product: "lucide-package", category: "lucide-layout-grid", page: "lucide-layout-template" };

/** a type's templates: its default (there even without a file: the site's built-in one), then its alternates */
const keysOf = (type: PageType) => [type, ...props.keys.filter((key) => key !== type && pageTypeOf(key) === type)];
const nameOf = (key: string) => (key.includes(".") ? alternateName(key) : t("PAGE_BUILDER.DESIGNER.TEMPLATES.DEFAULT"));

function pick(key: string | null, close: () => void) {
  close();
  if (key !== props.current) emit("open", key);
}
</script>

<style lang="scss" scoped>
.pd-switch {
  // VcDropdown fills its row by default
  flex: none;
  width: auto;

  &__trigger {
    display: flex;
    align-items: center;
    gap: 6px;
    max-width: 320px;
    height: 32px;
    padding: 0 10px;
    border: 1px solid var(--neutrals-200);
    border-radius: 8px;
    background: var(--additional-50);
    color: var(--neutrals-900);
    font: inherit;
    font-size: 13px;
    cursor: pointer;

    &:hover:not(:disabled) {
      border-color: var(--neutrals-300);
    }

    &:disabled {
      cursor: default;
      opacity: 0.6;
    }
  }

  &__icon {
    flex: none;
    color: var(--neutrals-500);
  }

  &__kind {
    flex: none;
    color: var(--neutrals-500);
  }

  &__name {
    min-width: 0;
    overflow: hidden;
    font-weight: 600;
    white-space: nowrap;
    text-overflow: ellipsis;
  }

  &__menu {
    display: flex;
    flex-direction: column;
    width: 280px;
    padding: 4px;
    font-size: 13px;
  }

  &__group {
    margin: 0;
    padding: 10px 8px 4px;
    color: var(--neutrals-500);
    font-size: 11px;
    font-weight: 600;
    letter-spacing: 0.04em;
    text-transform: uppercase;
  }

  &__item {
    display: flex;
    align-items: center;
    gap: 8px;
    min-height: 32px;
    padding: 0 8px;
    border: 0;
    border-radius: 6px;
    background: none;
    color: var(--neutrals-900);
    font: inherit;
    text-align: left;
    cursor: pointer;

    span {
      flex: 1;
      min-width: 0;
      overflow: hidden;
      white-space: nowrap;
      text-overflow: ellipsis;
    }

    small {
      flex: none;
      color: var(--neutrals-500);
      font-size: 12px;
    }

    :deep(.vc-icon) {
      flex: none;
      color: var(--neutrals-500);
    }

    &:hover {
      background: var(--neutrals-100);
    }

    &--on,
    &--on:hover {
      background: var(--primary-50);
      color: var(--primary-700);
      font-weight: 600;
    }

    &--create {
      color: var(--primary-600);

      :deep(.vc-icon) {
        color: var(--primary-600);
      }
    }
  }
}
</style>
