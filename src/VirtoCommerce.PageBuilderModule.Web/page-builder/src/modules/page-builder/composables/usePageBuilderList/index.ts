import { computed, ref, ComputedRef, onMounted } from "vue";
import { useAsync, useLoading, useApiClient } from "@vc-shell/framework";
import { useI18n } from "vue-i18n";

import useUrlParams from "./../useUrlParams";

import {
  PageBuilderPageClient,
  IPageBuilderPageSearchCriteria,
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

export interface IUsePageBuilderList {
  items: ComputedRef<GroupedPageBuilderPage[]>;
  totalCount: ComputedRef<number>;
  pages: ComputedRef<number>;
  currentPage: ComputedRef<number>;
  searchQuery: ComputedRef<IPageBuilderPageSearchCriteria>;
  loadActivePages: (query?: IPageBuilderPageSearchCriteria) => Promise<void>;
  loadArchivedPages: (query?: IPageBuilderPageSearchCriteria) => Promise<void>;
  removePages: (query?: { ids: string[] }) => Promise<void>;
  loading: ComputedRef<boolean>;
  pageStatuses: ComputedRef<{ value: string; label: string }[]>;
  storeId: ComputedRef<string | null>;
}

export interface UsePageBuilderListOptions {
  pageSize?: number;
  sort?: string;
}

export function usePageBuilderList(options?: UsePageBuilderListOptions): IUsePageBuilderList {
  const { storeId, initUrlParams } = useUrlParams();

  const pageSize = options?.pageSize || 20;
  const searchQuery = ref<IPageBuilderPageSearchCriteria>({
    take: pageSize,
    sort: options?.sort,
  });
  const searchResult = ref<GroupedPageBuilderPageSearchResult>();
  const { t } = useI18n({ useScope: "global" });

  const { action: loadPages, loading: loadingPages } = useAsync<IPageBuilderPageSearchCriteria>(async (_query) => {
    if (!storeId?.value) {
      searchResult.value = new GroupedPageBuilderPageSearchResult({
        totalCount: 0,
        results: [],
      });
      return;
    }

    searchQuery.value = { ...searchQuery.value, ...(_query || {}) };
    const criteria = new PageBuilderPageSearchCriteria(searchQuery.value);
    criteria.storeId = storeId.value;

    const apiClient = await getApiClient();
    searchResult.value = await apiClient.searchGrouped(criteria);
  });

  function loadActivePages(query?: IPageBuilderPageSearchCriteria) {
    return loadPages({ ...query, status: `${PageStatuses.Draft},${PageStatuses.Published}` });
  }

  function loadArchivedPages(query?: IPageBuilderPageSearchCriteria) {
    return loadPages({ ...query, status: PageStatuses.Archived });
  }

  const { action: removePages, loading: loadingRemovePages } = useAsync<{ ids: string[] }>(async (_query) => {
    const ids = _query?.ids;
    if (ids) {
      const apiClient = await getApiClient();
      await apiClient.archiveGrouped(ids);
    }
  });

  onMounted(() => {
    initUrlParams();
  });

  return {
    items: computed(() => searchResult.value?.results || []),
    totalCount: computed(() => searchResult.value?.totalCount || 0),
    pages: computed(() => Math.ceil((searchResult.value?.totalCount || 1) / pageSize)),
    currentPage: computed(() => Math.ceil((searchQuery.value?.skip || 0) / Math.max(1, pageSize) + 1)),
    searchQuery: computed(() => searchQuery.value),
    loadActivePages,
    loadArchivedPages,
    removePages,
    loading: useLoading(loadingPages, loadingRemovePages),
    pageStatuses: computed(() =>
      Object.values(PageStatuses).map((value) => ({
        value,
        label: t(`PAGE_BUILDER.STATUS.${value.toUpperCase()}`),
      })),
    ),
    storeId: computed(() => storeId?.value ?? null),
  };
}
