import { computed, ref, watch, type ComputedRef, type Ref } from "vue";
import {
  type Breadcrumbs,
  getFileThumbnail,
  isImage as isImageName,
  readableSize,
  useAsync,
  useBreadcrumbs,
  useLoading,
} from "@vc-shell/framework";
import useUrlParams from "../useStoreParams";
import { AssetEntry, createAssetFolder, deleteAssets, searchAssetReferences, searchAssets, uploadAsset } from "../useAssetsLibraryApi";

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

function ensureLeadingSlash(value: string): string {
  return value.startsWith("/") ? value : `/${value}`;
}

function normalizeAssetUrl(value: string | undefined): string | undefined {
  if (!value?.trim()) {
    return undefined;
  }

  let normalized = value.trim();

  try {
    const parsedUrl = new URL(normalized, window.location.origin);
    normalized = parsedUrl.pathname;
  } catch {
    normalized = normalized.split(/[?#]/, 1)[0];
  }

  if (normalized.toLowerCase().startsWith("/assets/")) {
    normalized = normalized.slice("/assets".length);
  }

  try {
    normalized = decodeURIComponent(normalized);
  } catch {
    // Keep the original value if it contains invalid escape sequences.
  }

  return ensureLeadingSlash(normalized);
}

function getAssetKey(entry: AssetEntry | undefined): string | undefined {
  return entry?.relativeUrl || entry?.url;
}

function isImage(entry: AssetEntry | undefined): boolean {
  if (!entry || entry.type !== "blob") {
    return false;
  }

  if (entry.contentType?.startsWith("image/")) {
    return true;
  }

  return isImageName(entry.name) || /\.(avif|bmp|ico|webp)$/i.test(entry.name);
}

function getEntryIcon(entry: AssetEntry): string {
  if (entry.type === "folder") {
    return "material-folder";
  }

  return getFileThumbnail(entry.name);
}

function getReferencesCount(entry: AssetEntry): number {
  return entry.type === "blob" ? entry.referencesCount ?? 0 : 0;
}

export function useAssetsLibrary(t: (key: string) => string): IUseAssetsLibrary {
  const { storeId, initUrlParams } = useUrlParams();
  const entries = ref<AssetEntry[]>([]);
  const totalCount = ref(0);
  const currentFolderUrl = ref("");
  const searchValue = ref<string>();
  const selectedAsset = ref<AssetEntry>();
  const selectedAssetDimensions = ref<string>();
  const assetReferences = ref<Record<string, Pick<AssetEntry, "referencesCount" | "referencePages">>>({});
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
      title: t("PAGE_BUILDER.ASSETS.BREADCRUMBS.ROOT"),
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
        title: decodeURIComponent(segment),
        clickHandler: navigateToFolder,
      });
    });

    return items;
  }

  function syncBreadcrumbs() {
    if (currentBreadcrumbIds.value.length) {
      removeBreadcrumbs(currentBreadcrumbIds.value);
      currentBreadcrumbIds.value = [];
    }

    const items = buildBreadcrumbs();
    items.forEach(pushBreadcrumb);
    currentBreadcrumbIds.value = items.map((item) => item.id);
  }

  watch([currentFolderUrl, rootFolderUrl], syncBreadcrumbs, { immediate: true });

  watch(selectedAsset, (value) => {
    selectedAssetDimensions.value = undefined;

    if (!value || !isImage(value)) {
      return;
    }

    const previewUrl = getPreviewUrl(value);

    if (!previewUrl) {
      return;
    }

    const image = new Image();
    image.onload = () => {
      if (getPreviewUrl(selectedAsset.value) !== previewUrl) {
        return;
      }

      selectedAssetDimensions.value = `${image.naturalWidth} x ${image.naturalHeight}`;
    };
    image.src = previewUrl;
  });

  function formatDate(value?: string): string {
    if (!value) {
      return t("PAGE_BUILDER.ASSETS.DETAILS.NOT_AVAILABLE");
    }

    return new Intl.DateTimeFormat(undefined, {
      dateStyle: "medium",
    }).format(new Date(value));
  }

  function getAssetPublicUrl(entry: AssetEntry): string | undefined {
    if (entry.relativeUrl) {
      return new URL(`/assets${ensureLeadingSlash(entry.relativeUrl)}`, window.location.origin).toString();
    }

    if (!entry.url) {
      return undefined;
    }

    try {
      const parsedUrl = new URL(entry.url, window.location.origin);

      if (parsedUrl.pathname.startsWith("/assets/")) {
        return new URL(`${parsedUrl.pathname}${parsedUrl.search}`, window.location.origin).toString();
      }

      return parsedUrl.toString();
    } catch {
      return entry.url;
    }
  }

  function getAssetPath(entry: AssetEntry): string {
    const publicUrl = getAssetPublicUrl(entry);

    if (!publicUrl) {
      return entry.relativeUrl || entry.url || "";
    }

    try {
      const parsedUrl = new URL(publicUrl);
      return `${parsedUrl.pathname}${parsedUrl.search}`;
    } catch {
      return publicUrl;
    }
  }

  function getPreviewUrl(entry: AssetEntry | undefined): string | undefined {
    if (!entry) {
      return undefined;
    }

    const publicUrl = getAssetPublicUrl(entry);

    if (!publicUrl) {
      return undefined;
    }

    if (!entry.modifiedDate) {
      return publicUrl;
    }

    const separator = publicUrl.includes("?") ? "&" : "?";
    return `${publicUrl}${separator}t=${encodeURIComponent(entry.modifiedDate)}`;
  }

  function getFolderUrl(entry: AssetEntry): string {
    if (entry.type === "folder") {
      return entry.relativeUrl || entry.url || currentFolderUrl.value;
    }

    const relativeUrl = entry.relativeUrl || "";
    const suffix = `/${entry.name}`;

    if (relativeUrl.endsWith(suffix)) {
      return relativeUrl.slice(0, -suffix.length);
    }

    return currentFolderUrl.value;
  }

  const { action: loadEntries, loading: loadingEntries } = useAsync<string | undefined>(async (preferredSelectionUrl) => {
    if (!currentFolderUrl.value) {
      entries.value = [];
      totalCount.value = 0;
      assetReferences.value = {};
      return;
    }

    const result = await searchAssets(currentFolderUrl.value, searchValue.value?.trim());
    assetReferences.value = await loadAssetReferences(result.results);
    entries.value = result.results.map(applyAssetReferences);
    totalCount.value = result.totalCount;

    if (preferredSelectionUrl) {
      selectedAsset.value = entries.value.find((entry) => getAssetKey(entry) === preferredSelectionUrl);
    } else if (selectedAsset.value) {
      const selectedKey = getAssetKey(selectedAsset.value);
      selectedAsset.value = selectedKey
        ? entries.value.find((entry) => getAssetKey(entry) === selectedKey)
        : undefined;
    }
  });

  async function loadAssetReferences(assetEntries: AssetEntry[]) {
    const assetUrls = assetEntries
      .filter((entry) => entry.type === "blob")
      .map((entry) => entry.relativeUrl || entry.url)
      .filter((url): url is string => !!url);

    if (!storeId.value || !assetUrls.length) {
      return {};
    }

    let references: Awaited<ReturnType<typeof searchAssetReferences>> = [];

    try {
      references = await searchAssetReferences(storeId.value, assetUrls, true);
    } catch {
      // Keep the asset library usable if reference lookup is temporarily unavailable.
    }

    return references.reduce<Record<string, Pick<AssetEntry, "referencesCount" | "referencePages">>>((result, reference) => {
      const value = {
        referencesCount: reference.referencesCount,
        referencePages: reference.pages ?? [],
      };

      [reference.assetUrl, reference.normalizedAssetUrl, normalizeAssetUrl(reference.assetUrl)]
        .filter((url): url is string => !!url)
        .forEach((url) => {
          result[url] = value;
        });

      return result;
    }, {});
  }

  function applyAssetReferences(entry: AssetEntry): AssetEntry {
    const references = getAssetReference(entry);
    return references ? { ...entry, ...references } : entry;
  }

  function getAssetReference(entry: AssetEntry | undefined): Pick<AssetEntry, "referencesCount" | "referencePages"> | undefined {
    if (!entry) {
      return undefined;
    }

    return [
      entry.relativeUrl,
      entry.url,
      getAssetPublicUrl(entry),
      normalizeAssetUrl(entry.relativeUrl),
      normalizeAssetUrl(entry.url),
    ]
      .filter((url): url is string => !!url)
      .map((url) => assetReferences.value[url])
      .find(Boolean);
  }

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

    await uploadAsset(getFolderUrl(selectedAsset.value), renamedFile);
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

  async function reload(preferredSelectionUrl?: string) {
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

    selectedAsset.value = entry;
  }

  function clearSelection() {
    selectedAsset.value = undefined;
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
    rootFolderUrl,
    breadcrumbs,
    initialize,
    reload,
    clearSelection,
    navigateToFolder,
    onEntryClick,
    isImage,
    getEntryIcon,
    getReferencesCount,
    getReferencePages: (entry) => getAssetReference(entry)?.referencePages ?? [],
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
