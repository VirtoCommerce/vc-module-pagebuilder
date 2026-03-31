import { computed, ref, reactive, Ref, ComputedRef, onMounted } from "vue";
import { useAsync, useLoading, useApiClient, useModificationTracker } from "@vc-shell/framework";
import {
  FilePublishStatus,
  PageBuilderPageClient,
  GroupedPageBuilderPage,
} from "../../../../api_client/virtocommerce.pagebuildermodule";

import useUserGroups, { IUserGroupsResult } from "./../useUserGroups";
import useOrganizations, { IOrganizationsResult } from "./../useOrganizations";
import useUrlParams from "../useStoreParams";

const { getApiClient } = useApiClient(PageBuilderPageClient);

export interface IUsePageBuilderDetails {
  item: Ref<GroupedPageBuilderPage>;
  status: Ref<FilePublishStatus>;
  isModified: Readonly<Ref<boolean>>;
  loading: ComputedRef<boolean>;
  loadGroup: () => Promise<void>;
  saveGroup: () => Promise<GroupedPageBuilderPage>;
  deleteGroup: () => Promise<void>;
  loadUserGroups: () => Promise<IUserGroupsResult>;
  loadOrganizations: (keyword?: string, skip?: number, objectIds?: string[]) => Promise<IOrganizationsResult>;
  isReadOnly: ComputedRef<boolean>;
  statusText: ComputedRef<string>;
  publishGroup: () => Promise<void>;
  unpublishGroup: () => Promise<void>;
  openDraftDesigner: () => void;
}

export interface UsePageBuilderDetailsOptions {
  id?: string;
  storeId?: string;
}

export function usePageBuilderDetails(options?: UsePageBuilderDetailsOptions): IUsePageBuilderDetails {
  const { getUserGroups } = useUserGroups();
  const { getOrganizations } = useOrganizations();
  const { storeId, initUrlParams } = useUrlParams();

  const item = ref<GroupedPageBuilderPage>(new GroupedPageBuilderPage());
  const isNew = ref(!options?.id);
  const status = ref<FilePublishStatus>(new FilePublishStatus());

  let groupStoreId: string | undefined;

  const { currentValue, isModified, resetModificationState } = useModificationTracker(item);

  const { action: loadGroup, loading: loadingGroup } = useAsync(async () => {
    if (options?.id) {
      const apiClient = await getApiClient();
      const result = await apiClient.getGroup(options.id);
      status.value = await apiClient.publishStatus(options.id);
      currentValue.value = reactive(result);
    } else {
      currentValue.value = reactive(new GroupedPageBuilderPage());
    }
    resetModificationState();
  });

  const { action: saveGroup, loading: savingGroup } = useAsync(async () => {
    const apiClient = await getApiClient();
    const group = currentValue.value;
    let result: GroupedPageBuilderPage;

    if (isNew.value) {
      group.storeId = groupStoreId;
      result = await apiClient.createGroup(group);
    } else {
      result = await apiClient.updateGroup(group);
    }

    currentValue.value = reactive(result);
    resetModificationState();
    return result;
  });

  const { action: deleteGroup, loading: deletingGroup } = useAsync(async () => {
    if (currentValue.value.id) {
      const apiClient = await getApiClient();
      await apiClient.archiveGroups([currentValue.value.id]);
    }
  });

  const { action: publishGroup, loading: publishingGroup } = useAsync(async () => {
    const groupId = currentValue.value?.id;
    if (!groupId) {
      throw new Error("Can't publish group.");
    }
    const apiClient = await getApiClient();
    await apiClient.publishGroup(groupId, true);

    if (currentValue.value) {
      await loadGroup();
    }
  });

  const { action: unpublishGroup, loading: unpublishingGroup } = useAsync(async () => {
    // check if the page has changes
    if (status.value?.hasChanges) {
      throw new Error("PAGE_BUILDER.PAGES.ALERTS.UNPUBLISH_WITH_DRAFT");
    }

    const groupId = currentValue.value?.id;
    if (!groupId) {
      throw new Error("Can't unpublish group.");
    }
    const apiClient = await getApiClient();
    await apiClient.publishGroup(groupId, false);

    if (currentValue.value) {
      await loadGroup();
    }
  });

  function openDraftDesigner() {
    // Get platform URL from env
    const platformUrl: string = (
      (import.meta.env.DEV && import.meta.env.APP_PLATFORM_URL) ||
      window.location.origin
    ).replace(/\/$/, "");
    const designerUrl = `${platformUrl}/Modules/$(VirtoCommerce.PageBuilderModule)/Content/page-builder-designer/index.html`;
    const groupId = currentValue.value?.id;
    const pageStoreId = currentValue.value?.storeId;

    if (groupId && pageStoreId) {
      const url = `${designerUrl}?storeId=${pageStoreId}#/pages?type=pages&groupId=${groupId}`;
      window.open(url, "_blank");
    } else {
      throw new Error("Can't open page.");
    }
  }

  async function loadUserGroupsAsync() {
    return getUserGroups();
  }

  async function loadOrganizationsAsync(keyword?: string, skip?: number, objectIds?: string[]) {
    return getOrganizations(keyword, skip, objectIds);
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
      groupStoreId = options.storeId;
    } else {
      groupStoreId = storeId.value as string;
    }
  });

  return {
    item: currentValue,
    status,
    isModified,
    loading: useLoading(loadingGroup, savingGroup, deletingGroup, publishingGroup, unpublishingGroup),
    loadGroup,
    saveGroup,
    deleteGroup,
    loadUserGroups: loadUserGroupsAsync,
    loadOrganizations: loadOrganizationsAsync,
    isReadOnly,
    statusText,
    publishGroup,
    unpublishGroup,
    openDraftDesigner,
  };
}
