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
import { downloadPageContent, PageExportData } from "../usePageContentApi";
import { usePageDesigner } from "../usePageDesigner";
import { serializeImportedPageContent } from "./importContent";
import { loadPage, savePage } from "../../../designer/api";

const { getApiClient } = useApiClient(PageBuilderPageClient);

/** What search engines and shares show for the page: its content's `settings.seoInfo`, which the storefront reads */
export interface PageSeo {
  pageTitle?: string;
  metaDescription?: string;
  metaKeywords?: string;
}

export interface IUsePageBuilderDetails {
  item: Ref<GroupedPageBuilderPage>;
  seo: Ref<PageSeo>;
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
  openDraftDesigner: (onClose?: () => void) => void;
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
  const openPageDesigner = usePageDesigner();

  const item = ref<GroupedPageBuilderPage>({} as GroupedPageBuilderPage);
  const isNew = ref(!options?.id);
  const status = ref<FilePublishStatus>({} as FilePublishStatus);
  const seo = ref<PageSeo>({});
  /** the loaded SEO, to save it only when it changed ("" and unset alike) */
  const seoKey = (x: PageSeo) => JSON.stringify([x.pageTitle || "", x.metaDescription || "", x.metaKeywords || ""]);
  let savedSeo = seoKey({});

  let groupStoreId: string | undefined;

  const { action: loadGroup, loading: loadingGroup } = useAsync(async () => {
    if (options?.id) {
      const apiClient = await getApiClient();
      // the SEO lives in the page's content (draft, else published); a page without content has none yet
      const [result, page] = await Promise.all([apiClient.getGroup(options.id), loadPage(options.id).catch(() => undefined)]);
      status.value = await apiClient.publishStatus(options.id);
      seo.value = { ...(page?.doc.settings.seoInfo as PageSeo | undefined) };
      savedSeo = seoKey(seo.value);
      item.value = reactive(result);
    } else {
      // New pages are visible to everyone (incl. anonymous) by default; import keeps its own value below.
      const page = { visibility: true } as GroupedPageBuilderPage;
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
      const content = options?.importData?.content;
      // Use the existing atomic creation API: a new import has no document version to edit yet.
      result =
        content !== undefined
          ? await apiClient.createGroupPage({
              ...group,
              content: serializeImportedPageContent(content),
            })
          : await apiClient.createGroup(group);

      item.value = reactive(result);
      isNew.value = false;
    } else {
      result = await apiClient.updateGroup(group);
      item.value = reactive(result);
      await saveSeo(result.id!);
    }

    return result;
  });

  /**
   * A changed SEO goes into the page's draft, as the designer saves it (live with Publish). Read again first: the
   * designer may have saved since this blade loaded.
   */
  async function saveSeo(groupId: string) {
    if (seoKey(seo.value) === savedSeo) return;
    const { doc, eTag } = await loadPage(groupId);
    await savePage(groupId, { ...doc, settings: { ...doc.settings, seoInfo: { ...seo.value } } }, eTag ?? "");
    savedSeo = seoKey(seo.value);
    status.value = await (await getApiClient()).publishStatus(groupId);
  }

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

  /** onClose: when the designer blade closes (Admin Shell only; the standalone app opens a new tab). */
  function openDraftDesigner(onClose?: () => void) {
    openPageDesigner(
      {
        groupId: item.value?.id,
        storeId: item.value?.storeId,
        cultureName: item.value?.cultureName,
        status: item.value?.status,
      },
      onClose,
    );
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
    seo,
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
