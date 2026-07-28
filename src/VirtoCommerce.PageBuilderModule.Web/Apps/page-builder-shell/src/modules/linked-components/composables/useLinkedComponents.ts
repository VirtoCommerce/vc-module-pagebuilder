import { computed, onScopeDispose, ref, type ComputedRef, type Ref } from "vue";
import {
  notification,
  parseError,
  useAsync,
  useDataTablePagination,
  useLoading,
  type UseDataTablePaginationReturn,
} from "@vc-shell/framework";
import { useUrlParams } from "../../page-builder";
import { createLatestRequestTracker } from "../../../utilities/latestRequest";
import type {
  LinkedComponent,
  LinkedComponentSearchCriteria,
  LinkedComponentSearchResult,
  RenameLinkedComponentPayload,
} from "../types";
import { createLinkedComponentDetailsLoader } from "../utilities";
import { useLinkedComponentsApi } from "./useLinkedComponentsApi";

const DEFAULT_PAGE_SIZE = 20;

export interface IUseLinkedComponents {
  items: ComputedRef<LinkedComponent[]>;
  loading: ComputedRef<boolean>;
  detailsLoading: ComputedRef<boolean>;
  totalCount: ComputedRef<number>;
  searchValue: Ref<string | undefined>;
  selectedComponent: Ref<LinkedComponent | undefined>;
  pagination: UseDataTablePaginationReturn;
  storeId: ComputedRef<string | null>;
  storeContextStatus: ReturnType<typeof useUrlParams>["storeContextStatus"];
  initialize: () => Promise<void>;
  reload: () => Promise<void>;
  search: (keyword?: string) => Promise<void>;
  selectComponent: (component: LinkedComponent) => Promise<void>;
  refreshComponent: (component: LinkedComponent) => Promise<LinkedComponent>;
  clearSelection: () => void;
  renameComponent: (component: LinkedComponent, name: string) => Promise<void>;
  deleteComponent: (component: LinkedComponent) => Promise<void>;
}

export function useLinkedComponents(): IUseLinkedComponents {
  const { searchLinkedComponents, getLinkedComponent, renameLinkedComponent, deleteLinkedComponent } =
    useLinkedComponentsApi();
  const { storeId, storeContextStatus, initUrlParams, validateStoreContext } = useUrlParams();
  const searchResult = ref<LinkedComponentSearchResult>({ totalCount: 0, results: [] });
  const searchValue = ref<string>();
  const selectedComponent = ref<LinkedComponent>();
  const searchQuery = ref<LinkedComponentSearchCriteria>({
    skip: 0,
    take: DEFAULT_PAGE_SIZE,
  });
  let lastSuccessfulPage = 1;
  let disposed = false;
  const loadingComponents = ref(false);
  const loadingDetails = ref(false);
  const searchRequests = createLatestRequestTracker((loading) => {
    loadingComponents.value = loading;
  });

  const detailsLoader = createLinkedComponentDetailsLoader({
    getComponent: getLinkedComponent,
    getSelectedComponentId: () => selectedComponent.value?.id,
    selectComponent: (component) => {
      selectedComponent.value = component;
    },
    applyComponent,
    clearSelectedComponent: (componentId) => {
      if (selectedComponent.value?.id === componentId) {
        selectedComponent.value = undefined;
      }
    },
    onLoadingChange: (loading) => {
      loadingDetails.value = loading;
    },
  });

  async function loadComponents(query?: LinkedComponentSearchCriteria): Promise<boolean> {
    const request = searchRequests.begin();
    searchQuery.value = { ...searchQuery.value, ...(query ?? {}) };
    const requestCriteria = { ...searchQuery.value };
    const requestStoreId = storeId.value;

    try {
      if (!requestStoreId || !(await validateStoreContext())) {
        if (request.isCurrent()) {
          detailsLoader.invalidate();
          searchResult.value = { totalCount: 0, results: [] };
          selectedComponent.value = undefined;
          lastSuccessfulPage = 1;
          pagination.setPage(1);
          return true;
        }
        return false;
      }

      if (!request.isCurrent() || storeId.value !== requestStoreId) {
        return false;
      }

      const result = await searchLinkedComponents({
        ...requestCriteria,
        storeId: requestStoreId,
      });

      if (!request.isCurrent() || storeId.value !== requestStoreId) {
        return false;
      }

      searchResult.value = result;
      lastSuccessfulPage = Math.floor((requestCriteria.skip ?? 0) / (requestCriteria.take ?? DEFAULT_PAGE_SIZE)) + 1;
      if (selectedComponent.value) {
        selectedComponent.value = searchResult.value.results.find(
          (component) => component.id === selectedComponent.value?.id,
        );
      }
      return true;
    } catch (error) {
      if (request.isCurrent()) {
        throw error;
      }
      return false;
    } finally {
      request.complete();
    }
  }

  async function loadDetails(component: LinkedComponent): Promise<void> {
    await detailsLoader.load(component);
  }

  const { action: refreshComponentAction, loading: loadingRefresh } = useAsync<LinkedComponent, LinkedComponent>(
    async (component) => {
      if (!component) {
        throw new TypeError("Linked component is required.");
      }

      if (selectedComponent.value?.id === component.id) {
        detailsLoader.invalidate();
      }

      const refreshed = await getLinkedComponent(component.id);
      applyComponent(refreshed);
      return refreshed;
    },
    { notify: false },
  );

  const { action: renameComponentAction, loading: loadingRename } = useAsync<RenameLinkedComponentPayload>(
    async (payload) => {
      if (!payload?.component || !payload.name) {
        return;
      }

      const updated = await renameLinkedComponent(payload.component, payload.name.trim());
      applyComponent(updated);
    },
    { notify: false },
  );

  const { action: deleteComponentAction, loading: loadingDelete } = useAsync<LinkedComponent>(
    async (component) => {
      if (!component) {
        return;
      }

      await deleteLinkedComponent(component.id);

      const itemIndex = searchResult.value.results.findIndex((item) => item.id === component.id);
      if (itemIndex >= 0) {
        searchResult.value.results.splice(itemIndex, 1);
        searchResult.value.totalCount = Math.max(0, searchResult.value.totalCount - 1);
      }

      if (selectedComponent.value?.id === component.id) {
        detailsLoader.invalidate();
        selectedComponent.value = undefined;
      }

      const lastPage = Math.max(1, Math.ceil(searchResult.value.totalCount / pagination.pageSize));
      if (pagination.currentPage > lastPage) {
        pagination.setPage(lastPage);
        lastSuccessfulPage = lastPage;
      }
    },
    { notify: false },
  );

  const pagination = useDataTablePagination({
    pageSize: DEFAULT_PAGE_SIZE,
    totalCount: computed(() => searchResult.value.totalCount),
    onPageChange: ({ skip }) => {
      detailsLoader.invalidate();
      selectedComponent.value = undefined;
      void loadComponents({ skip }).catch((error) => {
        pagination.setPage(lastSuccessfulPage);
        notification.error(parseError(error).message);
      });
    },
  });

  async function initialize() {
    initUrlParams();
    await loadComponents();
  }

  async function reload() {
    detailsLoader.invalidate();
    const applied = await loadComponents({ skip: pagination.skip });

    if (!applied) {
      return;
    }

    const component = selectedComponent.value;
    if (component?.id) {
      await loadDetails(component);
    }
  }

  async function search(keyword?: string) {
    searchValue.value = keyword?.trim() || undefined;
    detailsLoader.invalidate();
    selectedComponent.value = undefined;
    pagination.reset();
    await loadComponents({ keyword: searchValue.value, skip: 0 });
  }

  function clearSelection() {
    detailsLoader.invalidate();
    selectedComponent.value = undefined;
  }

  function applyComponent(component: LinkedComponent) {
    if (disposed) {
      return;
    }

    const itemIndex = searchResult.value.results.findIndex((item) => item.id === component.id);

    if (itemIndex >= 0) {
      searchResult.value.results.splice(itemIndex, 1, component);
    }

    if (selectedComponent.value?.id === component.id) {
      selectedComponent.value = component;
    }
  }

  onScopeDispose(() => {
    disposed = true;
    searchRequests.dispose();
    detailsLoader.dispose();
  });

  return {
    items: computed(() => searchResult.value.results),
    loading: useLoading(loadingComponents, loadingDetails, loadingRefresh, loadingRename, loadingDelete),
    detailsLoading: computed(
      () => loadingDetails.value || (loadingComponents.value && selectedComponent.value !== undefined),
    ),
    totalCount: computed(() => searchResult.value.totalCount),
    searchValue,
    selectedComponent,
    pagination,
    storeId: computed(() => storeId.value ?? null),
    storeContextStatus,
    initialize,
    reload,
    search,
    selectComponent: loadDetails,
    refreshComponent: refreshComponentAction,
    clearSelection,
    renameComponent: async (component, name) => renameComponentAction({ component, name }),
    deleteComponent: deleteComponentAction,
  };
}
