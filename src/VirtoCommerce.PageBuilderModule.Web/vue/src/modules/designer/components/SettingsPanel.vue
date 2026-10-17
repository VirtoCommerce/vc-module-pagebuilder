<template>
  <aside
    class="pd-settings"
    :aria-label="title"
  >
    <header class="pd-settings__head">
      <VcIcon
        :icon="icon"
        size="s"
        class="pd-settings__icon"
      />
      <span class="pd-settings__title">{{ title }}</span>
      <VcDropdown
        v-if="sel.kind !== 'page'"
        v-model="menuOpen"
        :items="actions"
        floating
        placement="bottom-end"
        @item-click="(x: { run: () => void }) => x.run()"
      >
        <template #trigger="{ isActive, toggle }">
          <VcButton
            variant="ghost"
            size="icon-sm"
            icon="lucide-ellipsis"
            :aria-label="$t('PAGE_BUILDER.DESIGNER.MORE')"
            :aria-expanded="isActive"
            @click="toggle"
          />
        </template>
        <template #item="{ item }">
          <span
            class="pd-settings__action"
            :class="{ 'pd-settings__action--danger': item.danger }"
          >
            <VcIcon
              :icon="item.icon"
              size="s"
            />
            {{ item.title }}
          </span>
        </template>
      </VcDropdown>
      <VcButton
        variant="ghost"
        size="icon-sm"
        icon="lucide-x"
        :aria-label="$t('PAGE_BUILDER.DESIGNER.DESELECT')"
        :title="$t('PAGE_BUILDER.DESIGNER.DESELECT')"
        @click="emit('close')"
      />
    </header>

    <div
      v-if="tabs.length > 1"
      role="tablist"
      class="pd-settings__tabs"
    >
      <button
        v-for="tab in tabs"
        :key="tab"
        type="button"
        role="tab"
        class="pd-settings__tab"
        :class="{ 'pd-settings__tab--active': tab === currentTab }"
        :aria-selected="tab === currentTab"
        @click="activeTab = tab"
      >
        {{ tab }}
      </button>
    </div>

    <div class="pd-settings__body">
      <div
        v-if="placed"
        class="pd-settings__shared"
      >
        <template v-if="component">
          <p>{{ $t("PAGE_BUILDER.DESIGNER.SHARED_INFO", { name: component.name, count: component.usageCount ?? 0 }) }}</p>
          <VcButton
            variant="primary"
            icon="lucide-pencil"
            @click="emit('edit-shared', placed.componentRef)"
          >
            {{ $t("PAGE_BUILDER.DESIGNER.EDIT_SHARED") }}
          </VcButton>
          <VcButton
            variant="secondary"
            icon="lucide-unlink"
            @click="designer.detach(placed.id)"
          >
            {{ $t("PAGE_BUILDER.DESIGNER.DETACH") }}
          </VcButton>
          <VcHint>{{ $t("PAGE_BUILDER.DESIGNER.DETACH_HINT") }}</VcHint>
        </template>
        <p v-else>{{ $t("PAGE_BUILDER.DESIGNER.SHARED_MISSING") }}</p>
      </div>
      <Fields
        v-if="values"
        :key="itemKey"
        :fields="tabFields"
        :values="values"
        :context="context"
        @update="designer.setField"
      />
      <p
        v-if="values && !placed && !tabFields.length"
        class="pd-settings__empty"
      >
        {{ $t("PAGE_BUILDER.DESIGNER.NO_SETTINGS") }}
      </p>
    </div>

    <footer
      v-if="sel.kind !== 'page'"
      class="pd-settings__foot"
    >
      <VcButton
        variant="ghost"
        icon="lucide-trash-2"
        class="pd-settings__remove"
        @click="confirmRemove"
      >
        {{ $t(sel.kind === "block" ? "PAGE_BUILDER.DESIGNER.REMOVE_BLOCK" : "PAGE_BUILDER.DESIGNER.REMOVE_SECTION") }}
      </VcButton>
    </footer>
  </aside>
</template>

<script setup lang="ts">
// The right panel: the selected section's, block's or page's settings, split in the schema's tabs (Content, Settings).
// Close deselects it (the panel hides).
import { computed, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { usePopup } from "@vc-shell/framework";
import { VcButton, VcDropdown, VcHint, VcIcon } from "@vc-shell/framework/ui";
import type { Designer } from "../useDesigner";
import type { Selection } from "../types";
import { iconOf, itemTitle } from "../schema";
import { isComponentRef } from "../document";
import Fields from "./Fields.vue";

const props = defineProps<{ designer: Designer; selection: Selection }>();
const emit = defineEmits<{ copy: []; paste: []; close: []; "edit-shared": [componentId: string] }>();

const { t } = useI18n({ useScope: "global" });
const sel = computed(() => props.selection);
const item = computed(() => props.designer.selectedItem.value);
const schema = computed(() => (sel.value.kind === "block" ? props.designer.blockSchema(item.value?.type) : props.designer.sectionSchema(item.value?.type)));

// a placed shared component: no fields here (it is edited as the component), only its info and Detach
const placed = computed(() => (item.value && isComponentRef(item.value) ? item.value : undefined));
const component = computed(() => (placed.value ? props.designer.components.get(placed.value.componentRef) : undefined));
const pageRow = computed(() => (sel.value.kind === "page" ? props.designer.pageRows.value.find((x) => x.key === (sel.value as { key: string }).key) : undefined));

const title = computed(() => {
  if (sel.value.kind === "page") return pageRow.value && pageRow.value.key !== "settings" ? pageRow.value.name : t("PAGE_BUILDER.DESIGNER.PAGE_SETTINGS");
  if (placed.value) return component.value?.name ?? t("PAGE_BUILDER.DESIGNER.SHARED_COMPONENT");
  return item.value ? itemTitle(item.value, schema.value) : "";
});
const icon = computed(() => {
  if (sel.value.kind === "page") return pageRow.value?.key === "settings" ? "lucide-file-cog" : iconOf({ icon: pageRow.value?.icon });
  return placed.value ? "lucide-link" : iconOf(schema.value);
});
const values = computed(() => (sel.value.kind === "page" ? props.designer.doc.value.settings : item.value));
const itemKey = computed(() => (sel.value.kind === "page" ? `page:${sel.value.key}` : sel.value.id));

const tabs = computed(() => [...new Set(props.designer.selectedFields.value.map((x) => x.tab ?? t("PAGE_BUILDER.DESIGNER.CONTENT")))]);
const activeTab = ref<string>();
const menuOpen = ref(false);
watch(itemKey, () => (activeTab.value = undefined));
// the picked tab while the item has it, else its first
const currentTab = computed(() => (activeTab.value && tabs.value.includes(activeTab.value) ? activeTab.value : tabs.value[0]));
const tabFields = computed(() => props.designer.selectedFields.value.filter((x) => (x.tab ?? t("PAGE_BUILDER.DESIGNER.CONTENT")) === currentTab.value));

// what the schemas' visibility expressions see as `this` (the Angular designer's control context)
const context = computed(() => {
  const doc = props.designer.doc.value;
  return {
    model: values.value,
    section: props.designer.selectedSection.value,
    block: sel.value.kind === "block" ? item.value : undefined,
    schema: schema.value,
    template: doc,
    page: doc.content,
    settings: doc.settings,
    location: { params: { storeId: props.designer.storeId.value } },
  };
});

const { showConfirmation } = usePopup();
async function confirmRemove() {
  const current = sel.value;
  if (await showConfirmation(t("PAGE_BUILDER.DESIGNER.CONFIRM_REMOVE", { name: title.value }))) props.designer.remove(current);
}

const actions = computed(() => {
  const current = sel.value;
  if (current.kind === "page") return [];
  return [
    { title: t("PAGE_BUILDER.DESIGNER.DUPLICATE"), icon: "lucide-copy-plus", run: () => props.designer.duplicate(current) },
    { title: t("PAGE_BUILDER.DESIGNER.CLIPBOARD.COPY"), icon: "lucide-copy", run: () => emit("copy") },
    { title: t("PAGE_BUILDER.DESIGNER.CLIPBOARD.PASTE"), icon: "lucide-clipboard-paste", run: () => emit("paste") },
    ...(placed.value ? [] : [{
      title: t(item.value?.hidden ? "PAGE_BUILDER.DESIGNER.SHOW" : "PAGE_BUILDER.DESIGNER.HIDE"),
      icon: item.value?.hidden ? "lucide-eye" : "lucide-eye-off",
      run: () => props.designer.toggleHidden(current),
    }]),
    { title: t("PAGE_BUILDER.DESIGNER.REMOVE"), icon: "lucide-trash-2", danger: true, run: confirmRemove },
  ];
});
</script>

<style lang="scss" scoped>
// the left rail's scale: 13px text, a 14px title, the same pill tabs
.pd-settings {
  display: flex;
  flex-direction: column;
  min-height: 0;
  height: 100%;
  font-size: 13px;

  &__head {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 12px 12px 12px 16px;
    border-bottom: 1px solid var(--neutrals-200);
  }

  &__icon {
    color: var(--neutrals-600);
  }

  &__head :deep(.vc-dropdown) {
    flex: none;
    width: auto;
  }

  &__title {
    flex: 1;
    min-width: 0;
    font-size: 14px;
    font-weight: 600;
    color: var(--neutrals-900);
    overflow: hidden;
    white-space: nowrap;
    text-overflow: ellipsis;
  }

  &__tabs {
    display: flex;
    gap: 4px;
    padding: 8px 8px 0;
  }

  &__tab {
    flex: 1;
    height: 32px;
    border: 0;
    border-radius: 6px;
    background: none;
    color: var(--neutrals-500);
    font: inherit;
    cursor: pointer;

    &:hover {
      background: var(--neutrals-100);
    }

    &--active,
    &--active:hover {
      background: var(--primary-50);
      color: var(--primary-700);
      font-weight: 600;
    }
  }

  &__body {
    flex: 1;
    min-height: 0;
    overflow-y: auto;
    padding: 16px;
  }

  &__shared {
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    gap: 10px;
    color: var(--neutrals-700);

    p {
      margin: 0;
    }
  }

  &__empty {
    margin: 0;
    color: var(--neutrals-500);
  }

  &__foot {
    padding: 8px 12px;
    border-top: 1px solid var(--neutrals-200);
  }

  &__remove {
    color: var(--danger-500);
  }

  &__action {
    display: flex;
    align-items: center;
    gap: 8px;

    &--danger {
      color: var(--danger-500);
    }
  }
}
</style>
