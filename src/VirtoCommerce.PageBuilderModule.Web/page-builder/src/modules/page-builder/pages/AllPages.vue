<template>
  <VcBlade
    :title="bladeTitle"
    width="50%"
    :expanded="expanded"
    :closable="closable"
    :toolbar-items="bladeToolbar"
    @close="$emit('close:blade')"
    @expand="$emit('expand:blade')"
    @collapse="$emit('collapse:blade')"
  >
    <!-- @vue-generic {GroupedPageBuilderPage}-->
    <VcTable
      :expanded="expanded"
      multiselect
      :items="items"
      :columns="columns"
      :pages="pages"
      :current-page="currentPage"
      :total-count="totalCount"
      :selected-item-id="selectedItemId"
      :search-value="searchValue"
      :loading="loading"
      :sort-expression="sortExpression"
      enable-item-actions
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
              :model-value="stagedFilters.status === status.value"
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
  </VcBlade>
</template>

<script lang="ts" setup>
import { computed, onMounted, Ref, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { debounce } from "lodash-es";
import {
  IActionBuilderResult,
  IBladeToolbar,
  IParentCallArgs,
  ITableColumns,
  useBladeNavigation,
  usePopup,
  useTableSort,
} from "@vc-shell/framework";

import { PageStatuses, usePageBuilderList } from "../composables/usePageBuilderList";
import { GroupedPageBuilderPage } from "../../../api_client/virtocommerce.pagebuildermodule";
import PageStatus from "../components/pageStatus.vue";

defineOptions({
  name: "PageBuilder",
  url: "/page-builder",
  isWorkspace: true,
  menuItem: {
    title: "PAGE_BUILDER.MENU.TITLE",
    icon: "material-article",
    priority: 1,
  },
});

interface Props {
  expanded?: boolean;
  closable?: boolean;
  param?: string;
  options?: Record<string, unknown>;
}

interface Emits {
  (event: "parent:call", args: IParentCallArgs): void;
  (event: "close:blade"): void;
  (event: "expand:blade"): void;
  (event: "collapse:blade"): void;
}

const props = withDefaults(defineProps<Props>(), {
  expanded: true,
  closable: true,
});

const emit = defineEmits<Emits>();

const { t } = useI18n({ useScope: "global" });
const { openBlade } = useBladeNavigation();
const { showConfirmation } = usePopup();

const { sortExpression, handleSortChange: tableSortHandler } = useTableSort({
  initialDirection: "DESC",
  initialProperty: "modifiedDate",
});

// Composable
const { items, totalCount, pages, currentPage, searchQuery, storeId, loadPages, removePages, loading, pageStatuses } =
  usePageBuilderList({
    pageSize: 20,
    sort: sortExpression.value,
  });

// State
const selectedItemId = ref<string>();
const selectedItems = ref<string[]>([]);
const searchValue = ref<string>();

const stagedFilters = ref({ status: undefined }) as Ref<{ status: string | undefined }>;

const filtersQuery = ref();

// Blade title
const bladeTitle = computed(() => t("PAGE_BUILDER.PAGES.LIST.TITLE"));

// Columns configuration
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
    sortable: false,
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

// Toolbar configuration
const bladeToolbar = computed((): IBladeToolbar[] => [
  {
    id: "add",
    icon: "material-add",
    title: t("PAGE_BUILDER.PAGES.LIST.TOOLBAR.ADD"),
    clickHandler: async () => {
      await openAddBlade();
    },
  },
  {
    id: "refresh",
    icon: "material-refresh",
    title: t("PAGE_BUILDER.PAGES.LIST.TOOLBAR.REFRESH"),
    clickHandler: async () => {
      await reload();
    },
  },
  {
    id: "delete",
    icon: "material-delete",
    title: t("PAGE_BUILDER.PAGES.LIST.TOOLBAR.REMOVE"),
    disabled: selectedItems.value.length === 0,
    clickHandler: async () => {
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
        selectedItems.value = [];
      }
    },
  },
]);

// Computed
const isFilterActionDisabled = computed(() => {
  return {
    apply: Object.values(stagedFilters.value).every((value) => value === undefined),
    reset: activeFilterCount.value === 0,
  };
});

const activeFilterCount = computed(() => {
  return Object.values(filtersQuery.value ?? {}).filter((value) => value !== undefined).length;
});

// Event handlers
function onItemClick(item: GroupedPageBuilderPage) {
  openBlade({
    blade: { name: "PageDetails" },
    param: item.id,
    options: {
      storeId: storeId?.value ?? undefined,
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

async function openAddBlade() {
  openBlade({
    blade: { name: "PageBuilderDetails" },
    options: {
      storeId: storeId?.value ?? undefined,
    },
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

// Watchers
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

async function reload() {
  await loadPages({
    ...searchQuery.value,
    skip: (currentPage.value - 1) * (searchQuery.value.take ?? 10),
    sort: sortExpression.value,
  });
}

function onHeaderClick(item: ITableColumns) {
  tableSortHandler(item.id);
}

function toggleStatusFilter(statusValue: string, checked: boolean) {
  if (checked) {
    stagedFilters.value.status = statusValue;
  } else {
    stagedFilters.value.status = undefined;
  }
}

async function applyFilters(closePanel: () => void) {
  console.log(stagedFilters.value);
  filtersQuery.value = {
    statuses: stagedFilters.value.status,
  };

  await loadPages({
    ...searchQuery.value,
    ...filtersQuery.value,
  });
  closePanel();
}

async function resetFilters(closePanel: () => void) {
  stagedFilters.value = { status: undefined };
  filtersQuery.value = undefined;
  await loadPages({
    ...searchQuery.value,
    statuses: `${PageStatuses.Draft},${PageStatuses.Published}`,
  });

  closePanel();
}

// Lifecycle hooks
onMounted(async () => {
  await loadPages({
    statuses: `${PageStatuses.Draft},${PageStatuses.Published}`,
  });
});

defineExpose({
  title: bladeTitle,
  reload,
  onItemClick,
});
</script>
