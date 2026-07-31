<template>
  <VcDataTable
    class="tw-min-h-0 tw-flex-1"
    :items="components"
    :loading="loading"
    :total-count="totalCount"
    :pagination="pagination"
    :active-item-id="selectedComponentId"
    :row-actions="tableActionBuilder"
    :searchable="true"
    :search-value="searchValue"
    :search-placeholder="t('LINKED_COMPONENTS.SEARCH.PLACEHOLDER')"
    :empty-state="emptyState"
    :not-found-state="notFoundState"
    :show-all-columns="true"
    state-key="page_builder_linked_components"
    @row-click="$emit('select', $event.data)"
    @search="$emit('search', $event || undefined)"
    @pagination-click="$emit('page-change', $event)"
    @click.stop
  >
    <VcColumn
      id="name"
      field="name"
      :title="t('LINKED_COMPONENTS.TABLE.NAME')"
      :always-visible="true"
      mobile-role="title"
    />

    <VcColumn
      id="usageCount"
      field="usageCount"
      :title="t('LINKED_COMPONENTS.TABLE.USAGE')"
      width="20%"
      :always-visible="true"
      mobile-role="field"
    />

    <VcColumn
      id="modifiedDate"
      field="modifiedDate"
      type="date-ago"
      :title="t('LINKED_COMPONENTS.TABLE.MODIFIED')"
      width="25%"
      :always-visible="true"
      mobile-role="field"
    />

    <VcColumn
      id="modifiedBy"
      field="modifiedBy"
      :title="t('LINKED_COMPONENTS.TABLE.MODIFIED_BY')"
      width="22%"
      mobile-role="field"
    />
  </VcDataTable>
</template>

<script lang="ts" setup>
import { computed } from "vue";
import { useI18n } from "vue-i18n";
import type { TableAction, UseDataTablePaginationReturn } from "@vc-shell/framework";
import { VcColumn, VcDataTable } from "@vc-shell/framework/ui";
import type { LinkedComponent } from "../types";

interface Props {
  components: LinkedComponent[];
  totalCount: number;
  pagination: UseDataTablePaginationReturn;
  selectedComponentId?: string;
  searchValue?: string;
  canUpdate: boolean;
  canDelete: boolean;
  loading?: boolean;
}

interface Emits {
  (event: "select", component: LinkedComponent): void;
  (event: "search", keyword?: string): void;
  (event: "page-change", page: number): void;
  (event: "rename", component: LinkedComponent): void;
  (event: "delete", component: LinkedComponent): void;
}

const props = defineProps<Props>();
const emit = defineEmits<Emits>();
const { t } = useI18n({ useScope: "global" });

const emptyState = computed(() => ({
  icon: "lucide-blocks",
  title: t("LINKED_COMPONENTS.EMPTY.TITLE"),
  description: t("LINKED_COMPONENTS.EMPTY.DESCRIPTION"),
}));
const notFoundState = computed(() => ({
  icon: "lucide-search-x",
  title: t("LINKED_COMPONENTS.EMPTY.NOT_FOUND_TITLE"),
  description: t("LINKED_COMPONENTS.EMPTY.NOT_FOUND_DESCRIPTION"),
}));

function tableActionBuilder(component: LinkedComponent): TableAction<LinkedComponent>[] {
  const actions: TableAction<LinkedComponent>[] = [];

  if (props.canUpdate) {
    actions.push({
      id: "rename",
      icon: "lucide-pencil",
      title: t("LINKED_COMPONENTS.ACTIONS.RENAME"),
      type: "info",
      clickHandler: async () => {
        emit("rename", component);
      },
    });
  }

  if (props.canDelete) {
    actions.push({
      id: "delete",
      icon: "lucide-trash-2",
      title:
        component.usageCount > 0
          ? t("LINKED_COMPONENTS.ACTIONS.DELETE_BLOCKED")
          : t("LINKED_COMPONENTS.ACTIONS.DELETE"),
      type: "danger",
      variant: "danger",
      disabled: component.usageCount > 0,
      clickHandler: async () => {
        emit("delete", component);
      },
    });
  }

  return actions;
}
</script>
