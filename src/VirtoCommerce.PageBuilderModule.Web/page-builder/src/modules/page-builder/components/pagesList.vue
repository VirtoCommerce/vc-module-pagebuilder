<template>
  <!-- @vue-generic {GroupedPageBuilderPage}-->
  <VcTable
    enable-item-actions
    multiselect
    :expanded="expanded"
    :items="items"
    :columns="columns"
    :pages="pages"
    :current-page="currentPage"
    :total-count="totalCount"
    :selected-item-id="selectedItemId"
    :search-value="searchValue"
    :loading="loading"
    :sort-expression="sortExpression"
    :item-action-builder="actionBuilder"
    :active-filter-count="activeFilterCount"
    @item-click="onItemClick"
    @selection-changed="onSelectionChange"
    @search:change="onSearchList"
    @pagination-click="onPaginationClick"
    @header-click="onHeaderClick"
  >
    <template #item_status="{ item }">
      <PageStatus :status="item.status" />
    </template>

    <template #filters="{ closePanel }">
      <div class="tw-p-4">
        <h3 class="tw-font-semibold tw-text-sm tw-mb-4">{{ $t("PAGE_BUILDER.PAGES.LIST.TABLE.FILTER.STATUS") }}</h3>
        <div class="tw-flex tw-flex-col tw-gap-2">
          <VcCheckbox
            v-for="status in pageStatuses"
            :key="status.value"
            :model-value="statusFilters.status === status.value"
            @update:model-value="(checked: boolean) => toggleStatusFilter(status.value, checked)"
          >
            {{ status.label }}
          </VcCheckbox>
        </div>

        <div class="tw-flex tw-gap-2 tw-mt-6">
          <VcButton
            :disabled="isFilterActionDisabled.apply"
            @click="applyFilters(closePanel)"
          >
            {{ $t("PAGE_BUILDER.PAGES.LIST.TABLE.FILTER.APPLY") }}
          </VcButton>
          <VcButton
            variant="secondary"
            :disabled="isFilterActionDisabled.reset"
            @click="resetFilters(closePanel)"
          >
            {{ $t("PAGE_BUILDER.PAGES.LIST.TABLE.FILTER.RESET") }}
          </VcButton>
        </div>
      </div>
    </template>
  </VcTable>
</template>
<script lang="ts" setup>
import { ref, Ref, computed, watch, onMounted, readonly } from "vue";
import { useI18n } from "vue-i18n";
import { debounce } from "lodash-es";
import { ITableColumns, useTableSort, useBladeNavigation, usePopup, IActionBuilderResult } from "@vc-shell/framework";
import { GroupedPageBuilderPage } from "../../../api_client/virtocommerce.pagebuildermodule";
import { PageStatuses, usePageBuilderList, useUrlParams } from "../composables";

interface Props {
  expanded?: boolean;
  closable?: boolean;
  param?: string;
  options?: Record<string, unknown>;
  statuses?: PageStatuses[];
}

const props = withDefaults(defineProps<Props>(), {
  expanded: true,
  closable: true,
});

const { t } = useI18n({ useScope: "global" });
const { openBlade } = useBladeNavigation();
const { showConfirmation } = usePopup();
const { storeId } = useUrlParams();

const { sortExpression, handleSortChange: tableSortHandler } = useTableSort({
  initialDirection: "DESC",
  initialProperty: "modifiedDate",
});

const { items, totalCount, pages, currentPage, searchQuery, loadPages, removePages, loading, pageStatuses } =
  usePageBuilderList({
    pageSize: 20,
    sort: sortExpression.value,
    statuses: props.statuses,
  });

const selectedItemId = ref<string>();
const selectedItems = ref<string[]>([]);
const searchValue = ref<string>();
const statusFilters = ref({ status: undefined }) as Ref<{ status: string | undefined }>;
const filtersQuery = ref();

const columns = computed((): ITableColumns[] => [
  {
    id: "name",
    title: t("PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.NAME"),
    alwaysVisible: true,
    sortable: true,
  },
  {
    id: "cultureName",
    title: t("PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.CULTURE_NAME"),
    alwaysVisible: true,
    sortable: true,
  },
  {
    id: "permalink",
    title: t("PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.PERMALINK"),
    alwaysVisible: true,
    sortable: true,
  },
  {
    id: "modifiedDate",
    title: t("PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.MODIFIED_DATE"),
    type: "date-time",
    alwaysVisible: true,
    sortable: true,
  },
  {
    id: "modifiedBy",
    title: t("PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.MODIFIED_BY"),
    sortable: false,
  },
  {
    id: "status",
    title: t("PAGE_BUILDER.PAGES.LIST.TABLE.HEADER.STATUS"),
    sortable: true,
  },
]);

const isFilterActionDisabled = computed(() => {
  return {
    apply: Object.values(statusFilters.value).every((value) => value === undefined),
    reset: activeFilterCount.value === 0,
  };
});

const activeFilterCount = computed(() => {
  return Object.values(filtersQuery.value ?? {}).filter((value) => value !== undefined).length;
});

function onItemClick(item: GroupedPageBuilderPage) {
  openBlade({
    blade: { name: "PageDetails" },
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

function onSelectionChange(selection: GroupedPageBuilderPage[]) {
  selectedItems.value = selection.map((item) => item.id!);
}

const onSearchList = debounce(async (keyword: string | undefined) => {
  console.debug(`Page builder list search by ${keyword}`);
  searchValue.value = keyword;
  await loadPages({
    ...searchQuery.value,
    keyword,
  });
}, 1000);

async function onPaginationClick(page: number) {
  await loadPages({
    ...searchQuery.value,
    skip: (page - 1) * (searchQuery.value.take ?? 20),
  });
}

const actionBuilder = (item: GroupedPageBuilderPage) => {
  const result: IActionBuilderResult[] = [];

  if (item.status !== "Archived") {
    result.push({
      icon: "material-delete",
      title: t("PAGE_BUILDER.PAGES.LIST.TABLE.ACTIONS.DELETE"),
      type: "danger",
      clickHandler: async () => {
        if (item.id && (await showConfirmation(t("PAGE_BUILDER.PAGES.ALERTS.DELETE")))) {
          removePages({ ids: [item.id] });
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

async function openAddBlade() {
  openBlade({
    blade: { name: "PageDetails" },
    options: {
      storeId: storeId.value ?? undefined,
    },
  });
}

async function reload() {
  await loadPages({
    ...searchQuery.value,
    skip: (currentPage.value - 1) * (searchQuery.value.take ?? 10),
    sort: sortExpression.value,
  });
}

async function removeSelectedPages() {
  if (
    await showConfirmation(
      t("PAGE_BUILDER.PAGES.ALERTS.DELETE_SELECTED_CONFIRMATION.MESSAGE", {
        count: selectedItems.value.length,
      }),
    )
  ) {
    // const ids = selectedItems.value.map(x => x.id);
    await removePages({ ids: selectedItems.value });
    await reload();
  }
}

function onHeaderClick(item: ITableColumns) {
  tableSortHandler(item.id);
}

function toggleStatusFilter(statusValue: string, checked: boolean) {
  if (checked) {
    statusFilters.value.status = statusValue;
  } else {
    statusFilters.value.status = undefined;
  }
}

async function applyFilters(closePanel: () => void) {
  filtersQuery.value = {
    statuses: statusFilters.value.status,
  };

  await loadPages({
    ...searchQuery.value,
    ...filtersQuery.value,
  });
  closePanel();
}

async function resetFilters(closePanel: () => void) {
  statusFilters.value = { status: undefined };
  filtersQuery.value = undefined;
  await loadPages({
    ...searchQuery.value,
    statuses: `${PageStatuses.Draft},${PageStatuses.Published}`,
  });

  closePanel();
}

onMounted(async () => {
  await loadPages({
    statuses: `${PageStatuses.Draft},${PageStatuses.Published}`,
  });
});

export interface ExposedPagesList {
  selectedItems: Readonly<Ref<readonly string[], readonly string[]>>;
  reload: () => Promise<void>;
  removeSelectedPages: () => Promise<void>;
  onItemClick: (item: GroupedPageBuilderPage) => void;
  openAddBlade: () => Promise<void>;
}

defineExpose<ExposedPagesList>({
  selectedItems: readonly(selectedItems),
  reload,
  removeSelectedPages,
  onItemClick,
  openAddBlade,
});
</script>
