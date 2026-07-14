import { computed, ref, ComputedRef, onMounted } from "vue";
import {
  useAsync,
  useLoading,
  useApiClient,
  useDataTablePagination,
  type UseDataTablePaginationReturn,
} from "@vc-shell/framework";
import { useI18n } from "vue-i18n";

import useUrlParams from "../useStoreParams";

import {
  PageBuilderPageClient,
  PageBuilderPageSearchCriteria,
  GroupedPageBuilderPage,
  GroupedPageBuilderPageSearchResult,
} from "../../../../api_client/virtocommerce.pagebuildermodule";

const { getApiClient } = useApiClient(PageBuilderPageClient);

export enum PageStatuses {
  Draft = "Draft",
  Published = "Published",
  Archived = "Archived",
}

export enum PageLifecycleFilters {
  Draft = "drafts",
  Active = "active",
  Archived = "archived",
  Pending = "pending",
}

export interface IUsePageBuilderList {
  items: ComputedRef<GroupedPageBuilderPage[]>;
  pagination: UseDataTablePaginationReturn;
  searchQuery: ComputedRef<PageBuilderPageSearchCriteria>;
  loadPages: (query?: PageBuilderPageSearchCriteria) => Promise<void>;
  removePages: (query?: { ids: string[] }) => Promise<void>;
  loading: ComputedRef<boolean>;
  pageStatuses: ComputedRef<{ value: string; label: string }[]>;
  storeId: ComputedRef<string | null>;
  storeContextStatus: ReturnType<typeof useUrlParams>["storeContextStatus"];
}

export interface UsePageBuilderListOptions {
  pageSize?: number;
  sort?: string;
  statuses?: PageStatuses[];
  lifecycle?: PageLifecycleFilters[];
}

export function usePageBuilderList(options?: UsePageBuilderListOptions): IUsePageBuilderList {
  const { storeId, storeContextStatus, initUrlParams, validateStoreContext } = useUrlParams();

  const pageSize = options?.pageSize || 20;
  const searchQuery = ref<PageBuilderPageSearchCriteria>({
    take: pageSize,
    sort: options?.sort,
    statuses: options?.statuses?.join(",") || undefined,
    lifecycle: options?.lifecycle?.join(",") || undefined,
  });
  const searchResult = ref<GroupedPageBuilderPageSearchResult>();
  const { t } = useI18n({ useScope: "global" });

  const { action: loadPages, loading: loadingPages } = useAsync<PageBuilderPageSearchCriteria>(async (_query) => {
    searchQuery.value = { ...searchQuery.value, ...(_query || {}) };

    if (!storeId?.value || !(await validateStoreContext())) {
      searchResult.value = {
        totalCount: 0,
        results: [],
      } as GroupedPageBuilderPageSearchResult;
      return;
    }

    const criteria: PageBuilderPageSearchCriteria = {
      ...searchQuery.value,
      storeId: storeId.value,
    };

    const apiClient = await getApiClient();
    searchResult.value = await apiClient.searchGroups(criteria);
  });

  const { action: removePages, loading: loadingRemovePages } = useAsync<{ ids: string[] }>(async (_query) => {
    const ids = _query?.ids;
    if (ids) {
      const apiClient = await getApiClient();
      await apiClient.archiveGroups(ids);
    }
  });

  const pagination = useDataTablePagination({
    pageSize,
    totalCount: computed(() => searchResult.value?.totalCount ?? 0),
    onPageChange: ({ skip }) => loadPages({ ...searchQuery.value, skip }),
  });

  onMounted(() => {
    initUrlParams();
  });

  const pagination = useDataTablePagination({
    pageSize,
    totalCount: computed(() => searchResult.value?.totalCount ?? 0),
    onPageChange: ({ skip }) => loadPages({ ...searchQuery.value, skip }),
  });

  return {
    items: computed(() => searchResult.value?.results || []),
    pagination,
    searchQuery: computed(() => searchQuery.value),
    loadPages,
    removePages,
    loading: useLoading(loadingPages, loadingRemovePages),
    pageStatuses: computed(() =>
      Object.values(PageStatuses).map((value) => ({
        value,
        label: t(`PAGE_BUILDER.STATUS.${value.toUpperCase()}`),
      })),
    ),
    storeId: computed(() => storeId?.value ?? null),
    storeContextStatus,
  };
}
