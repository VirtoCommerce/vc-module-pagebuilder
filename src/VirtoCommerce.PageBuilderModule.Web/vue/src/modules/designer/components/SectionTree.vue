<template>
  <nav
    class="pd-tree"
    :aria-label="$t('PAGE_BUILDER.DESIGNER.SECTIONS')"
  >
    <!-- v-show: the sortable lists stay the same elements -->
    <div
      v-show="!addingTo"
      class="pd-tree__main"
    >
      <!-- Shopify's section groups: the theme's header, shown on every page -->
      <template v-if="designer.groupsLoaded.value">
        <div class="pd-tree__heading pd-tree__heading--group">
          <span>{{ $t("PAGE_BUILDER.DESIGNER.GROUPS.HEADER") }}</span>
        </div>
        <SectionRows
          :designer="designer"
          :sections="headerSections"
          :offset="0"
          @hover="(id) => emit('hover', id)"
        />
        <button
          type="button"
          class="pd-tree__add"
          @click="addingTo = 'header'"
        >
          <VcIcon
            icon="lucide-circle-plus"
            size="s"
          />
          {{ $t("PAGE_BUILDER.DESIGNER.ADD_SECTION") }}
        </button>
        <div class="pd-tree__heading pd-tree__heading--group">
          <span>{{ $t("PAGE_BUILDER.DESIGNER.GROUPS.TEMPLATE") }}</span>
        </div>
      </template>

      <button
        v-for="row in topRows"
        :key="row.key"
        type="button"
        class="pd-tree__row pd-tree__row--page"
        :class="{ 'pd-tree__row--selected': isPageRow(row.key) }"
        @click="select({ kind: 'page', key: row.key })"
      >
        <VcIcon
          :icon="rowIcon(row)"
          size="s"
          class="pd-tree__icon"
        />
        <span class="pd-tree__name">{{ rowName(row) }}</span>
      </button>

      <div class="pd-tree__heading">
        <span>{{ $t("PAGE_BUILDER.DESIGNER.SECTIONS") }}</span>
        <button
          type="button"
          class="pd-tree__link"
          @click="emit('paste')"
        >
          {{ $t("PAGE_BUILDER.DESIGNER.CLIPBOARD.PASTE") }}
        </button>
        <button
          v-if="pageSections.length"
          type="button"
          class="pd-tree__link"
          @click="toggleSelectMode"
        >
          {{ $t(selecting ? "PAGE_BUILDER.DESIGNER.SELECT_MODE.DONE" : "PAGE_BUILDER.DESIGNER.SELECT_MODE.SELECT") }}
        </button>
      </div>

      <SectionRows
        :designer="designer"
        :sections="pageSections"
        :offset="headerSections.length"
        :selecting="selecting"
        @hover="(id) => emit('hover', id)"
      />

      <div
        v-if="selecting"
        class="pd-tree__bulk"
      >
        <span>{{ $t("PAGE_BUILDER.DESIGNER.SELECT_MODE.COUNT", { count: designer.checked.value.length }) }}</span>
        <VcButton
          v-if="!designer.shared.value"
          size="sm"
          variant="secondary"
          icon="lucide-blocks"
          :disabled="!designer.checked.value.length"
          @click="emit('save-shared')"
        >
          {{ $t("PAGE_BUILDER.DESIGNER.SELECT_MODE.SAVE_SHARED") }}
        </VcButton>
        <VcButton
          size="sm"
          variant="ghost"
          icon="lucide-trash-2"
          class="pd-tree__danger"
          :disabled="!designer.checked.value.length"
          @click="emit('remove-checked')"
        >
          {{ $t("PAGE_BUILDER.DESIGNER.SELECT_MODE.DELETE") }}
        </VcButton>
      </div>

      <button
        v-if="!selecting"
        type="button"
        class="pd-tree__add"
        @click="addingTo = 'page'"
      >
        <VcIcon
          icon="lucide-circle-plus"
          size="s"
        />
        {{ $t("PAGE_BUILDER.DESIGNER.ADD_SECTION") }}
      </button>

      <button
        v-for="row in bottomRows"
        :key="row.key"
        type="button"
        class="pd-tree__row pd-tree__row--page pd-tree__row--bottom"
        :class="{ 'pd-tree__row--selected': isPageRow(row.key) }"
        @click="select({ kind: 'page', key: row.key })"
      >
        <VcIcon
          :icon="rowIcon(row)"
          size="s"
          class="pd-tree__icon"
        />
        <span class="pd-tree__name">{{ rowName(row) }}</span>
      </button>

      <!-- the theme's footer, shown on every page -->
      <template v-if="designer.groupsLoaded.value">
        <div class="pd-tree__heading pd-tree__heading--group">
          <span>{{ $t("PAGE_BUILDER.DESIGNER.GROUPS.FOOTER") }}</span>
        </div>
        <SectionRows
          :designer="designer"
          :sections="footerSections"
          :offset="designer.doc.value.content.length - footerSections.length"
          @hover="(id) => emit('hover', id)"
        />
        <button
          type="button"
          class="pd-tree__add"
          @click="addingTo = 'footer'"
        >
          <VcIcon
            icon="lucide-circle-plus"
            size="s"
          />
          {{ $t("PAGE_BUILDER.DESIGNER.ADD_SECTION") }}
        </button>
      </template>
    </div>

    <AddSection
      v-if="addingTo"
      :designer="designer"
      :group="addingTo === 'page' ? undefined : addingTo"
      @preview="(type) => emit('preview-section', type)"
      @close="addingTo = null"
    />
  </nav>
</template>

<script setup lang="ts">
// The left panel, like Shopify's theme editor: the theme's Header (a section group shown on every page), the page's
// settings and sections (drag to reorder, the eye hides one, each with its blocks and "Add block"), then the theme's
// Footer. "Add section" adds to the area it is in.
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import { VcButton, VcIcon } from "@vc-shell/framework/ui";
import type { SectionGroup, Selection } from "../types";
import type { Designer, PageRow } from "../useDesigner";
import { iconOf } from "../schema";
import AddSection from "./AddSection.vue";
import SectionRows from "./SectionRows.vue";

const props = defineProps<{ designer: Designer }>();
const emit = defineEmits<{
  hover: [sectionId: string | null];
  "preview-section": [type: string | null];
  paste: [];
  "save-shared": [];
  "remove-checked": [];
}>();

const { t } = useI18n({ useScope: "global" });
/** the area "Add section" adds to */
const addingTo = ref<SectionGroup | "page" | null>(null);
// select mode: checkboxes on the page's sections, to delete them or make them a shared component at once
const selecting = ref(false);
function toggleSelectMode() {
  selecting.value = !selecting.value;
  props.designer.checked.value = [];
}

const headerSections = computed(() => props.designer.doc.value.content.filter((x) => x.__group === "header"));
const pageSections = computed(() => props.designer.doc.value.content.filter((x) => !x.__group));
const footerSections = computed(() => props.designer.doc.value.content.filter((x) => x.__group === "footer"));

const topRows = computed(() => props.designer.pageRows.value.filter((x) => x.position === "top"));
const bottomRows = computed(() => props.designer.pageRows.value.filter((x) => x.position === "bottom"));
const rowName = (row: PageRow) => (row.key === "settings" ? t("PAGE_BUILDER.DESIGNER.PAGE_SETTINGS") : row.name);
const rowIcon = (row: PageRow) => (row.key === "settings" ? "lucide-file-cog" : iconOf({ icon: row.icon }));
const isPageRow = (key: string) => {
  const sel = props.designer.selection.value;
  return sel?.kind === "page" && sel.key === key;
};

function select(sel: Selection) {
  props.designer.selection.value = sel;
}
</script>

<!-- not scoped: SectionRows renders the rows (all pd-tree__ classes) -->
<style lang="scss">
.pd-tree {
  display: flex;
  flex-direction: column;
  padding: 8px;
  font-size: 13px;

  &__main {
    display: flex;
    flex-direction: column;
  }

  &__heading {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 14px 8px 6px;
    font-size: 12px;
    font-weight: 600;
    color: var(--neutrals-500);

    span {
      flex: 1;
    }
  }

  &__heading--group {
    padding-top: 16px;
    color: var(--neutrals-700);
    text-transform: uppercase;
    letter-spacing: 0.04em;
    font-size: 11px;
  }

  &__link {
    padding: 0;
    border: 0;
    background: none;
    color: var(--primary-600);
    font: inherit;
    font-weight: 500;
    cursor: pointer;

    &:hover {
      text-decoration: underline;
    }
  }

  &__check {
    flex: none;
    margin: 0 2px;
    accent-color: var(--primary-500);
  }

  &__bulk {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 6px;
    margin-top: 8px;
    padding: 8px;
    border-radius: 8px;
    background: var(--neutrals-100);

    span {
      flex: 1 1 100%;
      color: var(--neutrals-700);
    }
  }

  &__danger {
    color: var(--danger-500);
  }

  &__list,
  &__blocks {
    margin: 0;
    padding: 0;
    list-style: none;
  }

  &__blocks {
    padding-left: 22px;
  }

  &__row {
    width: 100%;
    display: flex;
    align-items: center;
    gap: 6px;
    min-height: 32px;
    padding: 0 6px;
    border: 0;
    border-radius: 6px;
    background: none;
    color: var(--neutrals-900);
    font: inherit;
    text-align: left;
    cursor: pointer;
    user-select: none;

    &:hover,
    &--hovered {
      background: var(--neutrals-100);
    }

    &--selected,
    &--selected:hover {
      background: var(--primary-50);
      color: var(--primary-700);
      font-weight: 600;
    }

    &--hidden .pd-tree__name,
    &--hidden .pd-tree__icon {
      opacity: 0.5;
    }

    &--page {
      font-weight: 500;
    }
  }

  &__handle {
    display: flex;
    color: var(--neutrals-400);
    cursor: grab;
    opacity: 0;
  }

  &__row:hover &__handle {
    opacity: 1;
  }

  &__toggle {
    display: flex;
    padding: 2px;
    border: 0;
    background: none;
    color: var(--neutrals-500);
    cursor: pointer;
  }

  &__chevron--open {
    transform: rotate(90deg);
  }

  &__icon {
    flex: none;
    color: var(--neutrals-600);
  }

  &__name {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
  }

  &__eye {
    display: flex;
    padding: 2px;
    border: 0;
    background: none;
    color: var(--neutrals-500);
    cursor: pointer;
    opacity: 0;
  }

  &__row:hover &__eye,
  &__row--hidden &__eye {
    opacity: 1;
  }

  &__add {
    display: flex;
    align-items: center;
    gap: 6px;
    min-height: 32px;
    margin-top: 4px;
    padding: 0 8px;
    border: 0;
    border-radius: 6px;
    background: none;
    color: var(--primary-600);
    font: inherit;
    cursor: pointer;

    &:hover {
      background: var(--neutrals-100);
    }

    &--block {
      margin-top: 0;
    }
  }

  &__badge {
    flex: none;
    padding: 1px 6px;
    border-radius: 999px;
    background: var(--success-50);
    color: var(--success-700);
    font-size: 11px;
    font-weight: 500;

    &--missing {
      background: var(--danger-50);
      color: var(--danger-700);
    }
  }

  &__row--bottom {
    margin-top: 8px;
  }

  &__menu-item {
    display: flex;
    align-items: center;
    gap: 8px;
  }
}
</style>
