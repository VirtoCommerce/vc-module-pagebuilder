import { ref, type Ref } from "vue";
import type { AssetEntry, AssetReference, AssetReferenceDetails } from "../types";
import { useAssetsLibraryApi } from "./useAssetsLibraryApi";
import { getAssetKey, getReferencesCount as getEntryReferencesCount } from "../utilities/assetEntry";
import { createAssetReferenceDetails } from "../utilities/assetReferences";

type AssetReferenceState = Pick<
  AssetReferenceDetails,
  "referencesCount" | "pageReferencesCount" | "sharedComponentReferencesCount"
> &
  Partial<Pick<AssetReferenceDetails, "referencePages" | "referenceSharedComponents">>;
export type DeleteAssetReferences = AssetReferenceDetails;

export function useAssetReferences(storeId: Ref<string | null | undefined>) {
  const { searchAssetReferences, searchFolderReferences } = useAssetsLibraryApi();
  const assetReferences = ref<Record<string, AssetReferenceState>>({});
  const unavailableReferenceUrls = ref<Set<string>>(new Set());

  async function loadAssetReferences(
    assetEntries: AssetEntry[],
    isCurrent: () => boolean = () => true,
  ): Promise<boolean> {
    const currentStoreId = storeId.value;
    const assetUrls = assetEntries
      .filter((entry) => entry.type === "blob")
      .map(getAssetKey)
      .filter((url): url is string => !!url);

    if (!currentStoreId || !assetUrls.length) {
      if (isCurrent()) {
        assetReferences.value = {};
        unavailableReferenceUrls.value = new Set();
        return true;
      }

      return false;
    }

    let references: Awaited<ReturnType<typeof searchAssetReferences>>;

    try {
      references = await searchAssetReferences(currentStoreId, assetUrls, false);
    } catch {
      if (!isCurrent()) {
        return false;
      }

      unavailableReferenceUrls.value = new Set([...unavailableReferenceUrls.value, ...assetUrls]);
      return true;
    }

    if (!isCurrent()) {
      return false;
    }

    assetReferences.value = references.reduce<Record<string, AssetReferenceState>>((result, reference) => {
      if (reference.assetUrl) {
        result[reference.assetUrl] = toReferenceState(reference, false);
      }

      return result;
    }, {});
    const unavailableUrls = new Set(unavailableReferenceUrls.value);
    assetUrls.forEach((url) => unavailableUrls.delete(url));
    unavailableReferenceUrls.value = unavailableUrls;
    return true;
  }

  async function loadAssetReferenceDetails(
    entry: AssetEntry | undefined,
    isCurrent: () => boolean = () => true,
  ): Promise<boolean> {
    const currentStoreId = storeId.value;
    const assetUrl = getAssetKey(entry);

    if (!currentStoreId || !assetUrl || entry?.type !== "blob") {
      return false;
    }

    const references = await searchAssetReferences(currentStoreId, [assetUrl], true);
    if (!isCurrent()) {
      return false;
    }

    const reference = references[0];
    const referenceState = reference ? toReferenceState(reference, true) : createAssetReferenceDetails([]);

    assetReferences.value = {
      ...assetReferences.value,
      ...getAssetUrls(entry).reduce<Record<string, AssetReferenceState>>(
        (result, url) => {
          result[url] = referenceState;
          return result;
        },
        reference?.assetUrl ? { [reference.assetUrl]: referenceState } : {},
      ),
    };
    const unavailableUrls = new Set(unavailableReferenceUrls.value);
    getAssetUrls(entry).forEach((url) => unavailableUrls.delete(url));
    unavailableReferenceUrls.value = unavailableUrls;
    return true;
  }

  function applyAssetReferences(entry: AssetEntry): AssetEntry {
    const references = getAssetReference(entry);
    return references ? { ...entry, ...references } : entry;
  }

  function getAssetReference(entry: AssetEntry | undefined): AssetReferenceState | undefined {
    if (!entry) {
      return undefined;
    }

    return getAssetUrls(entry)
      .map((url) => assetReferences.value[url])
      .find(Boolean);
  }

  function areReferencesAvailable(entry: AssetEntry | undefined): boolean {
    return !getAssetUrls(entry).some((url) => unavailableReferenceUrls.value.has(url));
  }

  async function getDeleteReferences(entry: AssetEntry): Promise<DeleteAssetReferences> {
    const folderUrl = getAssetKey(entry);

    if (!folderUrl || !storeId.value) {
      return emptyDeleteReferences();
    }

    if (entry.type === "blob") {
      return getAssetReferences(
        [folderUrl],
        {
          referencesCount: getEntryReferencesCount(entry),
          pageReferencesCount: entry.pageReferencesCount ?? 0,
          sharedComponentReferencesCount: entry.sharedComponentReferencesCount ?? 0,
          referencePages: entry.referencePages ?? [],
          referenceSharedComponents: entry.referenceSharedComponents ?? [],
        },
        true,
      );
    }

    const reference = await searchFolderReferences(storeId.value, folderUrl, true);

    if (!reference) {
      return emptyDeleteReferences();
    }

    return toDeleteReferences([reference], true);
  }

  async function getAssetReferences(
    assetUrls: string[],
    fallback = emptyDeleteReferences(),
    includePages = false,
  ): Promise<DeleteAssetReferences> {
    if (!storeId.value || !assetUrls.length) {
      return fallback;
    }

    const references = await searchAssetReferences(storeId.value, assetUrls, includePages);
    return toDeleteReferences(references, includePages);
  }

  function toDeleteReferences(references: AssetReference[], includeDetails: boolean): DeleteAssetReferences {
    return includeDetails
      ? createAssetReferenceDetails(references)
      : {
          referencesCount: references.reduce((count, reference) => count + (reference.referencesCount ?? 0), 0),
          pageReferencesCount: references.reduce((count, reference) => count + (reference.pageReferencesCount ?? 0), 0),
          sharedComponentReferencesCount: references.reduce(
            (count, reference) => count + (reference.sharedComponentReferencesCount ?? 0),
            0,
          ),
          referencePages: [],
          referenceSharedComponents: [],
        };
  }

  function toReferenceState(reference: AssetReference, includeDetails: boolean): AssetReferenceState {
    if (includeDetails) {
      return createAssetReferenceDetails([reference]);
    }

    return {
      referencesCount: reference.referencesCount ?? 0,
      pageReferencesCount: reference.pageReferencesCount ?? 0,
      sharedComponentReferencesCount: reference.sharedComponentReferencesCount ?? 0,
    };
  }

  function getAssetUrls(entry: AssetEntry | undefined): string[] {
    return [entry?.relativeUrl, entry?.url].filter((url): url is string => !!url);
  }

  function emptyDeleteReferences(): DeleteAssetReferences {
    return createAssetReferenceDetails([]);
  }

  function getReferenceDetails(entry: AssetEntry | undefined): AssetReferenceDetails {
    const reference = getAssetReference(entry);

    return {
      referencesCount: reference?.referencesCount ?? entry?.referencesCount ?? 0,
      pageReferencesCount: reference?.pageReferencesCount ?? entry?.pageReferencesCount ?? 0,
      sharedComponentReferencesCount:
        reference?.sharedComponentReferencesCount ?? entry?.sharedComponentReferencesCount ?? 0,
      referencePages: reference?.referencePages ?? entry?.referencePages ?? [],
      referenceSharedComponents: reference?.referenceSharedComponents ?? entry?.referenceSharedComponents ?? [],
    };
  }

  return {
    resetAssetReferences: () => {
      assetReferences.value = {};
      unavailableReferenceUrls.value = new Set();
    },
    loadAssetReferences,
    loadAssetReferenceDetails,
    applyAssetReferences,
    getReferenceDetails,
    getReferencesCount: (entry: AssetEntry) => getReferenceDetails(entry).referencesCount,
    areReferencesAvailable,
    getDeleteReferences,
  };
}
