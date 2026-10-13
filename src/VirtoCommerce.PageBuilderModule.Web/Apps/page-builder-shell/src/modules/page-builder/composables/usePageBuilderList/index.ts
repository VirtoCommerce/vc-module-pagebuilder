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
  MultilanguagePageClient,
  PageBuilderPageClient,
  PageBuilderPageSearchCriteria,
  PageBuilderPageListItem,
  PageBuilderPageListSearchResult,
} from "../../../../api_client/virtocommerce.pagebuildermodule";

const { getApiClient } = useApiClient(PageBuilderPageClient);
const { getApiClient: getMultilanguageApiClient } = useApiClient(MultilanguagePageClient);

export enum PageTypes {
  Landing = "Landing",
  Multilanguage = "Multilanguage",
}

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
  items: ComputedRef<PageBuilderPageListItem[]>;
  pagination: UseDataTablePaginationReturn;
  searchQuery: ComputedRef<PageBuilderPageSearchCriteria>;
  loadPages: (query?: PageBuilderPageSearchCriteria) => Promise<void>;
  removePages: (query?: { items: PageBuilderPageListItem[] }) => Promise<void>;
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
  const searchResult = ref<PageBuilderPageListSearchResult>();
  const { t } = useI18n({ useScope: "global" });

  const { action: loadPages, loading: loadingPages } = useAsync<PageBuilderPageSearchCriteria>(async (_query) => {
    searchQuery.value = { ...searchQuery.value, ...(_query || {}) };

    if (!storeId?.value || !(await validateStoreContext())) {
      searchResult.value = {
        totalCount: 0,
        results: [],
      } as PageBuilderPageListSearchResult;
      return;
    }

    const criteria: PageBuilderPageSearchCriteria = {
      ...searchQuery.value,
      storeId: storeId.value,
    };

    const apiClient = await getApiClient();
    // A Multilanguage Page is a single row that carries its language statuses.
    searchResult.value = await apiClient.searchList(criteria);
  });

  const { action: removePages, loading: loadingRemovePages } = useAsync<{ items: PageBuilderPageListItem[] }>(
    async (_query) => {
      const items = _query?.items ?? [];
      const landingIds = items.filter((x) => x.type !== PageTypes.Multilanguage && x.id).map((x) => x.id!);
      const multilanguageIds = items.filter((x) => x.type === PageTypes.Multilanguage && x.id).map((x) => x.id!);

      if (landingIds.length) {
        const apiClient = await getApiClient();
        await apiClient.archiveGroups(landingIds);
      }

      if (multilanguageIds.length) {
        const apiClient = await getMultilanguageApiClient();
        await Promise.all(multilanguageIds.map((id) => apiClient.archiveAll(id)));
      }
    },
  );

  const pagination = useDataTablePagination({
    pageSize,
    totalCount: computed(() => searchResult.value?.totalCount ?? 0),
    onPageChange: ({ skip }) => loadPages({ ...searchQuery.value, skip }),
  });

  onMounted(() => {
    initUrlParams();
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
