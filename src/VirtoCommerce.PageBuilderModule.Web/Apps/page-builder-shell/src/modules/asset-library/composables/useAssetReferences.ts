import { ref, type Ref } from "vue";
import type { AssetEntry, AssetReference } from "../types";
import { useAssetsLibraryApi } from "./useAssetsLibraryApi";
import { getAssetKey, getReferencesCount } from "../utilities/assetEntry";

type AssetReferenceState = Pick<AssetEntry, "referencesCount" | "referencePages">;
export type DeleteAssetReferences = Required<Pick<AssetEntry, "referencesCount" | "referencePages">> & {
  usageKnown: boolean;
};

export function useAssetReferences(storeId: Ref<string | null | undefined>) {
  const { searchAssetReferences, searchFolderReferences } = useAssetsLibraryApi();
  const assetReferences = ref<Record<string, AssetReferenceState>>({});

  async function loadAssetReferences(assetEntries: AssetEntry[]) {
    const assetUrls = assetEntries
      .filter((entry) => entry.type === "blob")
      .map(getAssetKey)
      .filter((url): url is string => !!url);

    if (!storeId.value || !assetUrls.length) {
      assetReferences.value = {};
      return;
    }

    let references: Awaited<ReturnType<typeof searchAssetReferences>> = [];

    try {
      references = await searchAssetReferences(storeId.value, assetUrls, false);
    } catch {
      // Keep the asset library usable if reference lookup is temporarily unavailable.
    }

    assetReferences.value = references.reduce<Record<string, AssetReferenceState>>((result, reference) => {
      if (reference.assetUrl) {
        result[reference.assetUrl] = toReferenceState(reference, false);
      }

      return result;
    }, {});
  }

  async function loadAssetReferencePages(entry: AssetEntry | undefined): Promise<void> {
    const assetUrl = getAssetKey(entry);

    if (!storeId.value || !assetUrl || entry?.type !== "blob") {
      return;
    }

    const references = await searchAssetReferences(storeId.value, [assetUrl], true);
    const reference = references[0];
    const referenceState = reference
      ? toReferenceState(reference, true)
      : { referencesCount: 0, referencePages: [] };

    assetReferences.value = {
      ...assetReferences.value,
      ...getAssetUrls(entry).reduce<Record<string, AssetReferenceState>>((result, url) => {
        result[url] = referenceState;
        return result;
      }, reference?.assetUrl ? { [reference.assetUrl]: referenceState } : {}),
    };
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

  async function getDeleteReferences(entry: AssetEntry): Promise<DeleteAssetReferences> {
    const folderUrl = getAssetKey(entry);

    if (!folderUrl || !storeId.value) {
      return emptyDeleteReferences(false);
    }

    if (entry.type === "blob") {
      return getAssetReferences([folderUrl], {
        referencesCount: getReferencesCount(entry),
        referencePages: entry.referencePages ?? [],
        usageKnown: true,
      }, true);
    }

    const reference = await searchFolderReferences(storeId.value, folderUrl, true);

    if (!reference) {
      return emptyDeleteReferences();
    }

    return toDeleteReferences([reference], true);
  }

  async function getAssetReferences(assetUrls: string[], fallback = emptyDeleteReferences(), includePages = false): Promise<DeleteAssetReferences> {
    if (!storeId.value || !assetUrls.length) {
      return fallback;
    }

    const references = await searchAssetReferences(storeId.value, assetUrls, includePages);
    return toDeleteReferences(references, includePages);
  }

  function toDeleteReferences(references: AssetReference[], includePages: boolean): DeleteAssetReferences {
    const referencePages = includePages
      ? getDistinctReferencePages(references.flatMap((reference) => reference.pages ?? []))
      : [];

    return {
      referencesCount: referencePages.length || references.reduce((count, reference) => count + (reference.referencesCount ?? 0), 0),
      referencePages,
      usageKnown: true,
    };
  }

  function toReferenceState(reference: AssetReference, includePages: boolean): AssetReferenceState {
    return {
      referencesCount: reference.referencesCount,
      referencePages: includePages ? reference.pages ?? [] : undefined,
    };
  }

  function getAssetUrls(entry: AssetEntry | undefined): string[] {
    return [
      entry?.relativeUrl,
      entry?.url,
    ].filter((url): url is string => !!url);
  }

  function emptyDeleteReferences(usageKnown = true): DeleteAssetReferences {
    return {
      referencesCount: 0,
      referencePages: [],
      usageKnown,
    };
  }

  function getDistinctReferencePages(pages: NonNullable<AssetReference["pages"]>): NonNullable<AssetReference["pages"]> {
    const result: NonNullable<AssetReference["pages"]> = [];
    const seen = new Set<string>();

    for (const page of pages) {
      const key = page.id || page.permalink || `${page.name ?? ""}:${page.cultureName ?? ""}`;

      if (key && !seen.has(key)) {
        seen.add(key);
        result.push(page);
      }
    }

    return result;
  }

  return {
    resetAssetReferences: () => {
      assetReferences.value = {};
    },
    loadAssetReferences,
    loadAssetReferencePages,
    applyAssetReferences,
    getReferencePages: (entry: AssetEntry | undefined) => getAssetReference(entry)?.referencePages ?? [],
    getDeleteReferences,
  };
}
