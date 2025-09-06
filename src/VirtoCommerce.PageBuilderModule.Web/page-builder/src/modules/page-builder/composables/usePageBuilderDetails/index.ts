import { computed, ref, reactive, Ref, ComputedRef, onMounted } from "vue";
import { useAsync, useLoading, useApiClient, useModificationTracker } from "@vc-shell/framework";
import {
  FilePublishStatus,
  PageBuilderPageClient,
  PageBuilderPage,
} from "../../../../api_client/virtocommerce.pagebuildermodule";

import useCultureNames, { ICultureNameResult } from "./../useCultureNames";
import useUserGroups, { IUserGroupsResult } from "./../useUserGroups";
import useUrlParams from "./../useUrlParams";

const { getApiClient } = useApiClient(PageBuilderPageClient);

export interface IUsePageBuilderDetails {
  item: Ref<PageBuilderPage>;
  status: Ref<FilePublishStatus>;
  isModified: Readonly<Ref<boolean>>;
  loading: ComputedRef<boolean>;
  loadPage: () => Promise<void>;
  savePage: () => Promise<PageBuilderPage>;
  deletePage: () => Promise<void>;
  loadCultureNames: (storeId?: string) => Promise<ICultureNameResult>;
  loadUserGroups: () => Promise<IUserGroupsResult>;
  isReadOnly: ComputedRef<boolean>;
  statusText: ComputedRef<string>;
  publishPage: () => Promise<void>;
  unpublishPage: () => Promise<void>;
  openPageDesigner: () => void;
}

export interface UsePageBuilderDetailsOptions {
  id?: string;
  storeId?: string;
}

export function usePageBuilderDetails(options?: UsePageBuilderDetailsOptions): IUsePageBuilderDetails {
  const { getCultureNames } = useCultureNames();
  const { getUserGroups } = useUserGroups();
  const { storeId, initUrlParams } = useUrlParams();

  const item = ref<PageBuilderPage>(new PageBuilderPage());
  const isNew = ref(!options?.id);
  const status = ref<FilePublishStatus>(new FilePublishStatus());

  let pageStoreId: string | undefined;

  const { currentValue, isModified, resetModificationState } = useModificationTracker(item);

  const { action: loadPage, loading: loadingPage } = useAsync(async () => {
    if (options?.id) {
      const apiClient = await getApiClient();
      const result = await apiClient.getPageInGroupForEdit(options.id);
      status.value = await apiClient.publishStatus(options.id);
      currentValue.value = reactive(result);
    } else {
      currentValue.value = reactive(new PageBuilderPage());
    }
    resetModificationState();
  });

  const { action: savePage, loading: savingPage } = useAsync(async () => {
    const apiClient = await getApiClient();
    const page = currentValue.value;
    let result: PageBuilderPage;

    if (isNew.value) {
      page.storeId = pageStoreId;
      result = await apiClient.createPage(page);
    } else {
      result = await apiClient.updatePage(page);
    }

    currentValue.value = reactive(result);
    resetModificationState();
    return result;
  });

  const { action: deletePage, loading: deletingPage } = useAsync(async () => {
    if (currentValue.value.id) {
      const apiClient = await getApiClient();
      await apiClient.archiveGrouped([currentValue.value.id]);
    }
  });

  const { action: publishPage, loading: publishingPage } = useAsync(async () => {
    const groupId = currentValue.value?.groupId;
    const apiClient = await getApiClient();
    await apiClient.publishing(groupId, true);

    if (currentValue.value) {
      await loadPage();
    }
  });

  const { action: unpublishPage, loading: unpublishingPage } = useAsync(async () => {
    // check if the page has changes
    if (status.value?.hasChanges) {
      throw new Error("PAGE_BUILDER.PAGES.ALERTS.UNPUBLISH_WITH_DRAFT");
    }

    const groupId = currentValue.value?.groupId;
    const apiClient = await getApiClient();
    await apiClient.publishing(groupId, false);

    if (currentValue.value) {
      await loadPage();
    }
  });

  function openPageDesigner() {
    // Get platform URL from env
    const platformUrl: string = (
      (import.meta.env.DEV && import.meta.env.APP_PLATFORM_URL) ||
      window.location.origin
    ).replace(/\/$/, "");
    const designerUrl = `${platformUrl}/Modules/$(VirtoCommerce.PageBuilderModule)/Content/builder/index.html`;
    const groupId = currentValue.value?.groupId;
    const pageStoreId = currentValue.value?.storeId;

    if (groupId && pageStoreId) {
      const url = `${designerUrl}?storeId=${pageStoreId}#/pages?type=pages&groupId=${groupId}`;
      window.open(url, "_blank");
    } else {
      throw new Error("Can't open page.");
    }
  }

  async function loadCultureNamesAsync(storeId?: string) {
    return getCultureNames(storeId || pageStoreId);
  }

  async function loadUserGroupsAsync() {
    return getUserGroups();
  }

  const isReadOnly = computed(() => {
    return currentValue.value != null && currentValue.value.status === "Archived";
  });

  const statusText = computed(() => {
    const page = currentValue.value;
    if (page == null) {
      return "Draft";
    }
    return page.status || "Draft";
  });

  onMounted(() => {
    initUrlParams();
    if (options?.storeId) {
      pageStoreId = options.storeId;
    } else {
      pageStoreId = storeId.value as string;
    }
  });

  return {
    item: currentValue,
    status,
    isModified,
    loading: useLoading(loadingPage, savingPage, deletingPage, publishingPage, unpublishingPage),
    loadPage,
    savePage,
    deletePage,
    loadCultureNames: loadCultureNamesAsync,
    loadUserGroups: loadUserGroupsAsync,
    isReadOnly,
    statusText,
    publishPage,
    unpublishPage,
    openPageDesigner,
  };
}
