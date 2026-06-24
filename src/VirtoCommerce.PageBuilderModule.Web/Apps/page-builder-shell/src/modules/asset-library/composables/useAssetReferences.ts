import { ref, type Ref } from "vue";
import type { AssetEntry, AssetReference } from "../types";
import { useAssetsLibraryApi } from "./useAssetsLibraryApi";
import { getAssetKey, getReferencesCount } from "../utilities/assetEntry";

type AssetReferenceState = Pick<AssetEntry, "referencesCount" | "referencePages">;

export function useAssetReferences(storeId: Ref<string | null | undefined>) {
  const { searchAssetReferences, searchAssets } = useAssetsLibraryApi();
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

  async function getDeleteReferencesCount(entry: AssetEntry): Promise<number> {
    const folderUrl = getAssetKey(entry);

    if (!folderUrl || !storeId.value) {
      return 0;
    }

    if (entry.type === "blob") {
      return getAssetReferencesCount([folderUrl], getReferencesCount(entry));
    }

    const assetUrls = await collectFolderAssetUrls(folderUrl);

    if (!assetUrls.length) {
      return 0;
    }

    return getAssetReferencesCount(assetUrls);
  }

  async function getAssetReferencesCount(assetUrls: string[], fallback = 0): Promise<number> {
    if (!storeId.value || !assetUrls.length) {
      return fallback;
    }

    const references = await searchAssetReferences(storeId.value, assetUrls, false);
    return references.reduce((count, reference) => count + (reference.referencesCount ?? 0), 0);
  }

  async function collectFolderAssetUrls(folderUrl: string, visited = new Set<string>()): Promise<string[]> {
    if (visited.has(folderUrl)) {
      return [];
    }

    visited.add(folderUrl);

    const result = await searchAssets(folderUrl);
    const folders: string[] = [];
    const blobs: string[] = [];

    for (const entry of result.results) {
      const entryUrl = getAssetKey(entry);

      if (!entryUrl) {
        continue;
      }

      if (entry.type === "folder") {
        folders.push(entryUrl);
      } else {
        blobs.push(entryUrl);
      }
    }

    // Parallel recursion over subfolders (was sequential await-in-loop).
    const nested = await Promise.all(folders.map((url) => collectFolderAssetUrls(url, visited)));
    return [...blobs, ...nested.flat()];
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

  return {
    resetAssetReferences: () => {
      assetReferences.value = {};
    },
    loadAssetReferences,
    loadAssetReferencePages,
    applyAssetReferences,
    getReferencePages: (entry: AssetEntry | undefined) => getAssetReference(entry)?.referencePages ?? [],
    getDeleteReferencesCount,
  };
}
