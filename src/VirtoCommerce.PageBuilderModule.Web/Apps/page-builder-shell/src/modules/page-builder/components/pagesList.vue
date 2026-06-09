<template>
  <VcDataTable
    class="tw-grow tw-basis-0"
    :items="items"
    :loading="loading"
    :total-count="pagination.totalCount"
    :pagination="pagination"
    :searchable="true"
    :selection-mode="'multiple'"
    :global-filters="computedGlobalFilters"
    :row-actions="actionBuilder"
    v-model:active-item-id="selectedItemId"
    v-model:sort-field="sortField"
    v-model:sort-order="sortOrder"
    v-model:selection="localSelection"
    @row-click="onItemClick"
    @search="onSearchList"
    @pagination-click="pagination.goToPage"
    @filter="onFilter"
  >
    <VcColumn
      id="name"
      :title="$t('PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.NAME')"
      :always-visible="true"
      :sortable="true"
      field="name"
    />
    <VcColumn
      id="cultureName"
      :title="$t('PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.CULTURE_NAME')"
      :always-visible="true"
      :sortable="true"
      field="cultureName"
    />
    <VcColumn
      id="permalink"
      :title="$t('PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.PERMALINK')"
      :always-visible="true"
      :sortable="true"
      field="permalink"
    />
    <VcColumn
      id="modifiedDate"
      :title="$t('PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.MODIFIED_DATE')"
      :always-visible="true"
      :sortable="true"
      type="datetime"
      field="modifiedDate"
    />
    <VcColumn
      id="modifiedBy"
      :title="$t('PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.MODIFIED_BY')"
      :sortable="false"
      field="modifiedBy"
    />
    <VcColumn
      id="status"
      :title="$t('PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.STATUS')"
      :sortable="true"
      field="status"
    >
      <template #body="{ data }">
        <PageStatus
          extended
          :item="data"
        />
      </template>
    </VcColumn>
  </VcDataTable>
  <input
    ref="fileInputRef"
    type="file"
    accept=".json"
    style="display: none"
    @change="onFileSelected"
  />
</template>
<script lang="ts" setup>
import { ref, Ref, computed, watch, onMounted, readonly } from "vue";
import { useI18n } from "vue-i18n";
import { debounce } from "lodash-es";
import { useBlade, usePopup, useDataTableSort, IActionBuilderResult, notification } from "@vc-shell/framework";
import { GroupedPageBuilderPage } from "../../../api_client/virtocommerce.pagebuildermodule";
import { PageLifecycleFilters, usePageBuilderList, useUrlParams, refreshMenuBadges } from "../composables";
import { parseImportFile } from "../composables/usePageContentApi";
import PageStatus from "./pageStatus.vue";

import { VcColumn, VcDataTable } from "@vc-shell/framework/ui";

interface Props {
  lifecycle?: PageLifecycleFilters[];
}

const props = defineProps<Props>();

const { t } = useI18n({ useScope: "global" });
const { param, openBlade } = useBlade();
const { showConfirmation } = usePopup();
const { storeId, initUrlParams } = useUrlParams();

const { sortField, sortOrder, sortExpression } = useDataTableSort({
  initialDirection: "DESC",
  initialField: "modifiedDate",
});

const { items, pagination, searchQuery, loadPages, removePages, loading, pageStatuses } = usePageBuilderList({
  pageSize: 20,
  sort: sortExpression.value,
  lifecycle: props.lifecycle,
});

const selectedItemId = ref<string>();
const localSelection = ref<GroupedPageBuilderPage[]>([]);
const selectedItems = computed<string[]>(() =>
  localSelection.value.map((item) => item.id!).filter((id): id is string => !!id),
);
const fileInputRef = ref<HTMLInputElement | null>(null);
const searchValue = ref<string>();

const computedGlobalFilters = computed(() => [
  {
    id: "statuses",
    label: t("PAGE_BUILDER.PAGES.LIST.TABLE.FILTER.STATUS"),
    filter: {
      options: pageStatuses.value.map((s) => ({
        value: s.value,
        label: s.label,
      })),
      multiple: false,
    },
  },
]);

function onItemClick(event: { data: GroupedPageBuilderPage }) {
  const item = event.data;
  openBlade({
    name: "PageDetails",
    param: item.id,
    options: {
      storeId: storeId.value ?? undefined,
    },
    onOpen() {
      selectedItemId.value = item.id;
    },
    onClose() {
      selectedItemId.value = undefined;
    },
  });
}

const onSearchList = debounce(async (keyword: string | undefined) => {
  searchValue.value = keyword;
  await loadPages({
    ...searchQuery.value,
    keyword,
  });
}, 1000);

const actionBuilder = (item: GroupedPageBuilderPage) => {
  const result: IActionBuilderResult[] = [];

  if (item.status !== "Archived") {
    result.push({
      icon: "lucide-trash-2",
      title: t("PAGE_BUILDER.PAGES.LIST.TABLE.ACTIONS.DELETE"),
      type: "danger",
      clickHandler: async () => {
        if (item.id && (await showConfirmation(t("PAGE_BUILDER.PAGES.ALERTS.DELETE")))) {
          await removePages({ ids: [item.id] });
          await reload();
        }
      },
    });
  }

  return result;
};

watch(
  () => sortExpression.value,
  async (newVal) => {
    await loadPages({
      ...searchQuery.value,
      sort: newVal,
    });
  },
);

watch(
  () => param.value,
  (newVal) => {
    selectedItemId.value = newVal as string | undefined;
  },
  { immediate: true },
);

async function onFilter(event: { filters: Record<string, unknown> }) {
  const statusFilter = event.filters.statuses as string | string[] | undefined;
  const statuses = Array.isArray(statusFilter) ? statusFilter.join(",") : statusFilter;
  pagination.reset();
  await loadPages({
    ...searchQuery.value,
    statuses,
    skip: 0,
  });
}

function openLoadFlow() {
  fileInputRef.value?.click();
}

async function onFileSelected(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  if (!file) return;
  input.value = "";

  const importData = await parseImportFile(file);
  notification.success(t("PAGE_BUILDER.PAGES.ALERTS.LOAD_CONTENT_SUCCESS"));

  openBlade({
    name: "PageDetails",
    options: {
      storeId: storeId.value ?? undefined,
      importData,
    },
  });
}

async function openAddBlade() {
  openBlade({
    name: "PageDetails",
    options: {
      storeId: storeId.value ?? undefined,
    },
  });
}

async function reload() {
  await loadPages({
    ...searchQuery.value,
    skip: pagination.skip,
    sort: sortExpression.value,
  });
  localSelection.value = [];
  refreshMenuBadges();
}

async function removeSelectedPages() {
  if (
    await showConfirmation(
      t("PAGE_BUILDER.PAGES.ALERTS.DELETE_SELECTED_CONFIRMATION.MESSAGE", {
        count: selectedItems.value.length,
      }),
    )
  ) {
    await removePages({ ids: selectedItems.value });
    await reload();
  }
}

onMounted(async () => {
  initUrlParams();
  await loadPages();
});

export interface ExposedPagesList {
  selectedItems: Readonly<Ref<readonly string[], readonly string[]>>;
  reload: () => Promise<void>;
  removeSelectedPages: () => Promise<void>;
  onItemClick: (event: { data: GroupedPageBuilderPage }) => void;
  openAddBlade: () => Promise<void>;
  openLoadFlow: () => void;
}

defineExpose<ExposedPagesList>({
  selectedItems: readonly(selectedItems),
  reload,
  removeSelectedPages,
  onItemClick,
  openAddBlade,
  openLoadFlow,
});
</script>
