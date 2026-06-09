import { computed, ref, reactive, Ref, ComputedRef, onMounted } from "vue";
import { useAsync, useLoading, useApiClient } from "@vc-shell/framework";
import {
  FilePublishStatus,
  PageBuilderPageClient,
  GroupedPageBuilderPage,
} from "../../../../api_client/virtocommerce.pagebuildermodule";

import useUserGroups, { IUserGroupsResult } from "./../useUserGroups";
import useOrganizations, { IOrganizationsResult } from "./../useOrganizations";
import useUrlParams from "../useStoreParams";
import { downloadPageContent, uploadPageContent, PageExportData } from "../usePageContentApi";

const { getApiClient } = useApiClient(PageBuilderPageClient);

export interface IUsePageBuilderDetails {
  item: Ref<GroupedPageBuilderPage>;
  status: Ref<FilePublishStatus>;
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
  downloadContent: () => Promise<void>;
  clonePage: () => Promise<GroupedPageBuilderPage>;
}

export interface UsePageBuilderDetailsOptions {
  id?: string;
  storeId?: string;
  importData?: PageExportData;
}

function incrementCopyName(name: string): string {
  const match = /^(.*)\(copy(?:\s+(\d+))?\)$/.exec(name);
  if (match) {
    const base = match[1];
    const num = match[2] ? Number.parseInt(match[2], 10) + 1 : 2;
    return `${base}(copy ${num})`;
  }
  return `${name} (copy)`;
}

function incrementCopyPermalink(permalink: string): string {
  const match = /^(.*)-copy(?:-(\d+))?$/.exec(permalink);
  if (match) {
    const base = match[1];
    const num = match[2] ? Number.parseInt(match[2], 10) + 1 : 2;
    return `${base}-copy-${num}`;
  }
  return `${permalink}-copy`;
}

export function usePageBuilderDetails(options?: UsePageBuilderDetailsOptions): IUsePageBuilderDetails {
  const { getUserGroups } = useUserGroups();
  const { getOrganizations } = useOrganizations();
  const { storeId, initUrlParams } = useUrlParams();

  const item = ref<GroupedPageBuilderPage>({} as GroupedPageBuilderPage);
  const isNew = ref(!options?.id);
  const status = ref<FilePublishStatus>({} as FilePublishStatus);

  let groupStoreId: string | undefined;
  let pendingContentUpload = !!options?.importData?.content;

  const { action: loadGroup, loading: loadingGroup } = useAsync(async () => {
    if (options?.id) {
      const apiClient = await getApiClient();
      const result = await apiClient.getGroup(options.id);
      status.value = await apiClient.publishStatus(options.id);
      item.value = reactive(result);
    } else {
      const page = {} as GroupedPageBuilderPage;
      if (options?.importData) {
        const data = options.importData;
        page.name = data.name;
        page.permalink = data.permalink;
        page.cultureName = data.cultureName;
        page.visibility = data.visibility;
        page.userGroups = data.userGroups;
        page.organizationId = data.organizationId;
        page.startDate = data.startDate;
        page.endDate = data.endDate;
      }
      item.value = reactive(page);
    }
  });

  const { action: saveGroup, loading: savingGroup } = useAsync(async () => {
    const apiClient = await getApiClient();
    const group = item.value;
    let result: GroupedPageBuilderPage;

    if (isNew.value) {
      group.storeId = groupStoreId;
      result = await apiClient.createGroup(group);

      // Update state before upload so a failed upload won't cause duplicate createGroup on retry
      item.value = reactive(result);
      isNew.value = false;
    } else {
      result = await apiClient.updateGroup(group);
      item.value = reactive(result);
    }

    if (pendingContentUpload && result.id && options?.importData?.content) {
      await uploadPageContent(result.id, options.importData.content);
      pendingContentUpload = false;
    }

    return result;
  });

  const { action: deleteGroup, loading: deletingGroup } = useAsync(async () => {
    if (item.value.id) {
      const apiClient = await getApiClient();
      await apiClient.archiveGroups([item.value.id]);
    }
  });

  const { action: publishGroup, loading: publishingGroup } = useAsync(async () => {
    const groupId = item.value?.id;
    if (!groupId) {
      throw new Error("Can't publish group.");
    }
    const apiClient = await getApiClient();
    await apiClient.publishGroup(groupId, true);

    if (item.value) {
      await loadGroup();
    }
  });

  const { action: unpublishGroup, loading: unpublishingGroup } = useAsync(async () => {
    // check if the page has changes
    if (status.value?.hasChanges) {
      throw new Error("PAGE_BUILDER.PAGES.ALERTS.UNPUBLISH_WITH_DRAFT");
    }

    const groupId = item.value?.id;
    if (!groupId) {
      throw new Error("Can't unpublish group.");
    }
    const apiClient = await getApiClient();
    await apiClient.publishGroup(groupId, false);

    if (item.value) {
      await loadGroup();
    }
  });

  const { action: downloadContent, loading: downloadingContent } = useAsync(async () => {
    const groupId = item.value?.id;
    if (!groupId) {
      throw new Error("Can't download content.");
    }
    await downloadPageContent(groupId, item.value);
  });

  const { action: clonePage, loading: cloningPage } = useAsync(async () => {
    const source = item.value;
    if (!source?.id) {
      throw new Error("Can't clone page.");
    }

    const clone: GroupedPageBuilderPage = {
      name: incrementCopyName(source.name || ""),
      permalink: incrementCopyPermalink(source.permalink || ""),
      cultureName: source.cultureName,
      storeId: source.storeId,
      visibility: source.visibility,
      userGroups: source.userGroups,
      organizationId: source.organizationId,
    };

    const apiClient = await getApiClient();
    const created = await apiClient.createGroup(clone);

    if (created.id && source.id) {
      await apiClient.copyPageContent(created.id, source.id);
    }

    return created;
  });

  function openDraftDesigner() {
    // Get platform URL from env
    const platformUrl: string = (
      (import.meta.env.DEV && import.meta.env.APP_PLATFORM_URL) ||
      globalThis.location.origin
    ).replace(/\/$/, "");
    const designerUrl = `${platformUrl}/Modules/$(VirtoCommerce.PageBuilderModule)/Content/page-builder-designer/index.html`;
    const groupId = item.value?.id;
    const pageStoreId = item.value?.storeId;

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
    return item.value?.status === "Archived";
  });

  const statusText = computed(() => {
    const page = item.value;
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
    item,
    status,
    loading: useLoading(
      loadingGroup,
      savingGroup,
      deletingGroup,
      publishingGroup,
      unpublishingGroup,
      downloadingContent,
      cloningPage,
    ),
    ),
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
    downloadContent,
    clonePage,
  };
}
