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
      :items="items"
      :columns="columns"
      :pages="pages"
      :current-page="currentPage"
      :total-count="totalCount"
      :selected-item-id="selectedItemId"
      :search-value="searchValue"
      :loading="loading"
      :sort-expression="sortExpression"
      :active-filter-count="activeFilterCount"
      @item-click="onItemClick"
      @search:change="onSearchList"
      @pagination-click="onPaginationClick"
      @header-click="onHeaderClick"
    >
      <template #item_status="{ item }">
        <PageStatus :status="item.status" />
      </template>
    </VcTable>
  </VcBlade>
</template>

<script lang="ts" setup>
import { computed, onMounted, Ref, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { debounce } from "lodash-es";
import { IBladeToolbar, IParentCallArgs, ITableColumns, useBladeNavigation, useTableSort } from "@vc-shell/framework";

import { PageStatuses, usePageBuilderList } from "../composables/usePageBuilderList";
import { GroupedPageBuilderPage } from "../../../api_client/virtocommerce.pagebuildermodule";
import PageStatus from "../components/pageStatus.vue";

defineOptions({
  name: "ArchivedPages",
  url: "/all",
  isWorkspace: true,
  menuItem: {
    title: "PAGE_BUILDER.MENU.ARCHIVED_TITLE",
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

const { sortExpression, handleSortChange: tableSortHandler } = useTableSort({
  initialDirection: "DESC",
  initialProperty: "modifiedDate",
});

// Composable
const { items, totalCount, pages, currentPage, searchQuery, storeId, loadPages, loading } = usePageBuilderList({
  pageSize: 20,
  sort: sortExpression.value,
});

// State
const selectedItemId = ref<string>();
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
    id: "refresh",
    icon: "material-refresh",
    title: t("PAGE_BUILDER.PAGES.LIST.TOOLBAR.REFRESH"),
    clickHandler: async () => {
      await reload();
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

// Lifecycle hooks
onMounted(async () => {
  await loadPages({
    statuses: PageStatuses.Archived,
  });
});

defineExpose({
  title: bladeTitle,
  reload,
  onItemClick,
});
</script>
