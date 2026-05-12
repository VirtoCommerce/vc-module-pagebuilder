<template>
  <VcDataTable
    v-model:active-item-id="selectedItemId"
    v-model:sort-field="sortField"
    v-model:sort-order="sortOrder"
    v-model:selection="localSelection"
    state-key="page_builder_pages_list"
    :items="items"
    :total-count="pagination.totalCount"
    :pagination="pagination"
    :loading="loading"
    :searchable="true"
    :selection-mode="'multiple'"
    :item-action-builder="actionBuilder"
    :global-filters="computedGlobalFilters"
    @row-click="onItemClick"
    @search="onSearchList"
    @pagination-click="pagination.goToPage"
    @filter="onFilter"
  >
    <VcColumn
      id="name"
      :title="t('PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.NAME')"
      :always-visible="true"
      :sortable="true"
    />
    <VcColumn
      id="cultureName"
      :title="t('PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.CULTURE_NAME')"
      :always-visible="true"
      :sortable="true"
    />
    <VcColumn
      id="permalink"
      :title="t('PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.PERMALINK')"
      :always-visible="true"
      :sortable="true"
    />
    <VcColumn
      id="modifiedDate"
      :title="t('PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.MODIFIED_DATE')"
      type="datetime"
      :always-visible="true"
      :sortable="true"
    />
    <VcColumn
      id="modifiedBy"
      :title="t('PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.MODIFIED_BY')"
      :sortable="false"
    />
    <VcColumn
      id="status"
      :title="t('PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.STATUS')"
      type="status"
      :sortable="true"
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
import { useDataTableSort, useBlade, usePopup, IActionBuilderResult, notification } from "@vc-shell/framework";
import { GroupedPageBuilderPage } from "../../../api_client/virtocommerce.pagebuildermodule";
import {
  PageLifecycleFilters,
  usePageBuilderList,
  useUrlParams,
  useAiAgentStoreContext,
  refreshMenuBadges,
} from "../composables";
import { parseImportFile } from "../composables/usePageContentApi";
import PageStatus from "./pageStatus.vue";

import { VcColumn, VcDataTable } from "@vc-shell/framework/ui";

interface Props {
  param?: string;
  lifecycle?: PageLifecycleFilters[];
}

const props = defineProps<Props>();

const { t } = useI18n({ useScope: "global" });
const { openBlade } = useBlade();
const { showConfirmation } = usePopup();
const { storeId, initUrlParams } = useUrlParams();

// WORKAROUND: push storeId into the AI agent context so pagebuilder tools
// can read it. See docs/storeId-missing-in-ai-context.md for the proper fix.
useAiAgentStoreContext();

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
const fileInputRef = ref<HTMLInputElement | null>(null);

const computedGlobalFilters = computed(() => [
  {
    id: "statuses",
    label: t("PAGE_BUILDER.PAGES.LIST.TABLE.FILTER.STATUS"),
    filter: {
      options: pageStatuses.value.map((s) => ({
        value: s.value,
        label: s.label,
      })),
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
  console.debug(`Page builder list search by ${keyword}`);
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
  () => props.param,
  (newVal) => {
    selectedItemId.value = newVal;
  },
  { immediate: true },
);

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
        count: localSelection.value.length,
      }),
    )
  ) {
    const ids = localSelection.value.map((item) => item.id!).filter(Boolean);
    await removePages({ ids });
    await reload();
  }
}

async function onFilter(event: { filters: Record<string, unknown> }) {
  const statusFilter = event.filters.statuses as string | undefined;
  await loadPages({
    ...searchQuery.value,
    statuses: statusFilter,
    skip: 0,
  });
}

onMounted(async () => {
  initUrlParams();
  await loadPages();
});

const selectedItems = computed(() => localSelection.value.map((item) => item.id!).filter(Boolean));

export interface ExposedPagesList {
  selectedItems: Readonly<Ref<readonly string[]>>;
  reload: () => Promise<void>;
  removeSelectedPages: () => Promise<void>;
  onItemClick: (event: { data: GroupedPageBuilderPage }) => void;
  openAddBlade: () => Promise<void>;
  openLoadFlow: () => void;
}

defineExpose<ExposedPagesList>({
  selectedItems: readonly(selectedItems) as unknown as Readonly<Ref<readonly string[]>>,
  reload,
  removeSelectedPages,
  onItemClick,
  openAddBlade,
  openLoadFlow,
});
</script>
