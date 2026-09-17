import { computed, onScopeDispose, ref, watch, type ComputedRef, type Ref } from "vue";
import { useI18n } from "vue-i18n";
import {
  type Breadcrumbs,
  readableSize,
  useAsync,
  useBreadcrumbs,
  useLoading,
} from "@vc-shell/framework";
import { useUrlParams } from "../../page-builder";
import type { AssetEntry } from "../types";
import { useAssetsLibraryApi } from "./useAssetsLibraryApi";
import { formatAssetDate, getAssetPath, getAssetPublicUrl, getPreviewUrl, safeDecode } from "../utilities/assetUrl";
import { normalizeAssetFileName } from "../utilities/assetUpload";
import { getAssetKey, getEntryIcon, getFolderUrl, getReferencesCount, isImageEntry } from "../utilities/assetEntry";
import { useAssetReferences } from "./useAssetReferences";
import type { DeleteAssetReferences } from "./useAssetReferences";
import { useAssetSelection } from "./useAssetSelection";

interface UploadAssetsPayload {
  files: File[];
  folderUrl?: string;
}

export interface IUseAssetsLibrary {
  entries: Ref<AssetEntry[]>;
  loading: ComputedRef<boolean>;
  totalCount: Ref<number>;
  currentFolderUrl: Ref<string>;
  searchValue: Ref<string | undefined>;
  selectedAsset: Ref<AssetEntry | undefined>;
  selectedAssetDimensions: Ref<string | undefined>;
  storeId: Ref<string | null>;
  storeContextStatus: ReturnType<typeof useUrlParams>["storeContextStatus"];
  rootFolderUrl: ComputedRef<string>;
  breadcrumbs: ComputedRef<Breadcrumbs[]>;
  initialize: () => Promise<void>;
  reload: (preferredSelectionUrl?: string) => Promise<void>;
  clearSelection: () => void;
  navigateToFolder: (folderUrl: string) => Promise<void>;
  onEntryClick: (entry: AssetEntry) => Promise<void>;
  isImage: (entry: AssetEntry | undefined) => boolean;
  getEntryIcon: (entry: AssetEntry) => string;
  getReferencesCount: (entry: AssetEntry) => number;
  getReferencePages: (entry: AssetEntry | undefined) => NonNullable<AssetEntry["referencePages"]>;
  getDeleteReferences: (entry: AssetEntry) => Promise<DeleteAssetReferences>;
  findAssetByName: (folderUrl: string, fileName: string) => Promise<AssetEntry | undefined>;
  formatFileSize: (size?: number) => string;
  formatDate: (value?: string) => string;
  getAssetPath: (entry: AssetEntry) => string;
  getAssetPublicUrl: (entry: AssetEntry) => string | undefined;
  getPreviewUrl: (entry: AssetEntry | undefined) => string | undefined;
  createFolder: (name: string) => Promise<void>;
  uploadFiles: (files: FileList | File[], folderUrl?: string) => Promise<void>;
  replaceSelectedAsset: (file: File) => Promise<void>;
  deleteEntry: (entry: AssetEntry) => Promise<void>;
}

export function useAssetsLibrary(): IUseAssetsLibrary {
  const { searchAssets, createAssetFolder, deleteAssets, uploadAsset } = useAssetsLibraryApi();
  const { t } = useI18n({ useScope: "global" });
  const { storeId, storeContextStatus, initUrlParams, validateStoreContext } = useUrlParams();
  const entries = ref<AssetEntry[]>([]);
  const totalCount = ref(0);
  const currentFolderUrl = ref("");
  const searchValue = ref<string>();
  const {
    selectedAsset,
    selectedAssetDimensions,
    clearSelection,
    selectAsset,
    refreshSelection,
  } = useAssetSelection();
  const {
    resetAssetReferences,
    loadAssetReferences,
    loadAssetReferencePages,
    applyAssetReferences,
    getReferencePages,
    getDeleteReferences,
  } = useAssetReferences(storeId);
  const currentBreadcrumbIds = ref<string[]>([]);
  const { breadcrumbs, push: pushBreadcrumb, remove: removeBreadcrumbs } = useBreadcrumbs();

  const rootFolderUrl = computed(() => (storeId.value ? `/stores/${storeId.value}/Page Builder` : ""));

  function buildBreadcrumbs(): Breadcrumbs[] {
    const items: Breadcrumbs[] = [];

    if (!currentFolderUrl.value || !rootFolderUrl.value) {
      return items;
    }

    const baseSegments = rootFolderUrl.value.split("/").filter(Boolean);
    const currentSegments = currentFolderUrl.value.split("/").filter(Boolean);

    items.push({
      id: rootFolderUrl.value,
      title: t("ASSET_LIBRARY.BREADCRUMBS.ROOT"),
      clickHandler: navigateToFolder,
    });

    if (currentSegments.length <= baseSegments.length) {
      return items;
    }

    let accumulated = rootFolderUrl.value;

    currentSegments.slice(baseSegments.length).forEach((segment) => {
      accumulated = `${accumulated}/${segment}`;
      items.push({
        id: accumulated,
        title: safeDecode(segment),
        clickHandler: navigateToFolder,
      });
    });

    return items;
  }

  function clearTrackedBreadcrumbs() {
    if (currentBreadcrumbIds.value.length) {
      removeBreadcrumbs(currentBreadcrumbIds.value);
      currentBreadcrumbIds.value = [];
    }
  }

  function syncBreadcrumbs() {
    clearTrackedBreadcrumbs();
    const items = buildBreadcrumbs();
    items.forEach(pushBreadcrumb);
    currentBreadcrumbIds.value = items.map((item) => item.id);
  }

  watch([currentFolderUrl, rootFolderUrl], syncBreadcrumbs, { immediate: true });
  onScopeDispose(clearTrackedBreadcrumbs);

  function formatDate(value?: string): string {
    return formatAssetDate(value) ?? t("ASSET_LIBRARY.DETAILS.NOT_AVAILABLE");
  }

  const { action: loadEntries, loading: loadingEntries } = useAsync<string | undefined>(async (preferredSelectionUrl) => {
    if (!currentFolderUrl.value) {
      entries.value = [];
      totalCount.value = 0;
      resetAssetReferences();
      return;
    }

    const result = await searchAssets(currentFolderUrl.value, searchValue.value?.trim());
    await loadAssetReferences(result.results);
    entries.value = result.results.map(applyAssetReferences);
    totalCount.value = result.totalCount;
    refreshSelection(entries.value, preferredSelectionUrl);

    if (selectedAsset.value?.type === "blob") {
      await loadAssetReferencePages(selectedAsset.value);
    }
  });

  const { action: createFolderAction, loading: loadingCreateFolder } = useAsync<string>(async (name) => {
    if (!name || !currentFolderUrl.value) {
      return;
    }

    await createAssetFolder({
      name,
      parentUrl: currentFolderUrl.value,
    });

    await reload();
  });

  const { action: uploadFilesAction, loading: loadingUpload } = useAsync<UploadAssetsPayload>(async (payload) => {
    const files = payload?.files ?? [];
    const folderUrl = payload?.folderUrl;
    const targetFolderUrl = folderUrl || currentFolderUrl.value;

    if (!files?.length || !targetFolderUrl) {
      return;
    }

    for (const file of files) {
      await uploadAsset(targetFolderUrl, file);
    }

    await reload();
  });

  const { action: replaceSelectedAssetAction, loading: loadingReplace } = useAsync<File>(async (replacement) => {
    if (!replacement || !selectedAsset.value) {
      return;
    }

    const renamedFile = new File([replacement], selectedAsset.value.name, {
      type: replacement.type,
      lastModified: replacement.lastModified,
    });

    await uploadAsset(getFolderUrl(selectedAsset.value, currentFolderUrl.value), renamedFile);
    await reload(getAssetKey(selectedAsset.value));
  });

  const { action: deleteEntryAction, loading: loadingDelete } = useAsync<AssetEntry>(async (entry) => {
    if (!entry) {
      return;
    }

    const target = entry.relativeUrl || entry.url;

    if (!target) {
      return;
    }

    await deleteAssets([target]);

    if (getAssetKey(selectedAsset.value) === getAssetKey(entry)) {
      selectedAsset.value = undefined;
    }

    await reload();
  });

  function resetContent() {
    currentFolderUrl.value = "";
    selectedAsset.value = undefined;
    entries.value = [];
    totalCount.value = 0;
    resetAssetReferences();
  }

  async function reload(preferredSelectionUrl?: string) {
    if (!(await validateStoreContext())) {
      resetContent();
      return;
    }

    if (!currentFolderUrl.value) {
      currentFolderUrl.value = rootFolderUrl.value;
    }

    await loadEntries(preferredSelectionUrl);
  }

  async function navigateToFolder(folderUrl: string) {
    currentFolderUrl.value = folderUrl;
    selectedAsset.value = undefined;
    await reload();
  }

  async function onEntryClick(entry: AssetEntry) {
    if (entry.type === "folder") {
      await navigateToFolder(entry.relativeUrl || entry.url || currentFolderUrl.value);
      return;
    }

    selectAsset(entry);
    await loadAssetReferencePages(entry);
  }

  async function findAssetByName(folderUrl: string, fileName: string): Promise<AssetEntry | undefined> {
    if (!folderUrl || !fileName) {
      return undefined;
    }

    const result = await searchAssets(folderUrl, fileName);
    return result.results.find(
      (entry) => entry.type === "blob" && normalizeAssetFileName(entry.name) === normalizeAssetFileName(fileName),
    );
  }

  async function initialize() {
    initUrlParams();
    currentFolderUrl.value = rootFolderUrl.value;

    if (currentFolderUrl.value) {
      await reload();
    }
  }

  return {
    entries,
    loading: useLoading(loadingEntries, loadingCreateFolder, loadingUpload, loadingReplace, loadingDelete),
    totalCount,
    currentFolderUrl,
    searchValue,
    selectedAsset,
    selectedAssetDimensions,
    storeId: computed(() => storeId.value ?? null),
    storeContextStatus,
    rootFolderUrl,
    breadcrumbs,
    initialize,
    reload,
    clearSelection,
    navigateToFolder,
    onEntryClick,
    isImage: isImageEntry,
    getEntryIcon,
    getReferencesCount,
    getReferencePages,
    getDeleteReferences,
    findAssetByName,
    formatFileSize: readableSize,
    formatDate,
    getAssetPath,
    getAssetPublicUrl,
    getPreviewUrl,
    createFolder: createFolderAction,
    uploadFiles: async (files, folderUrl) => uploadFilesAction({ files: Array.from(files), folderUrl }),
    replaceSelectedAsset: replaceSelectedAssetAction,
    deleteEntry: deleteEntryAction,
  };
}
