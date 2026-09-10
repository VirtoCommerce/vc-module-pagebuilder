import type { AssetEntry, AssetSearchResult } from "../types";
import { createLatestRequestTracker } from "../../../utilities/latestRequest";

export interface AssetEntriesLoadRequest {
  folderUrl: string;
  keyword?: string;
  preferredSelectionUrl?: string;
}

interface AssetEntriesLoaderOptions {
  search: (folderUrl: string, keyword?: string) => Promise<AssetSearchResult>;
  loadReferences: (entries: AssetEntry[], isCurrent: () => boolean) => Promise<boolean>;
  apply: (
    result: AssetSearchResult,
    preferredSelectionUrl: string | undefined,
    isCurrent: () => boolean,
  ) => Promise<void> | void;
  clear: () => void;
  onLoadingChange: (loading: boolean) => void;
}

export interface AssetEntriesLoader {
  load: (request: AssetEntriesLoadRequest) => Promise<void>;
  invalidate: () => void;
  dispose: () => void;
}

export function createAssetEntriesLoader(options: AssetEntriesLoaderOptions): AssetEntriesLoader {
  const requests = createLatestRequestTracker(options.onLoadingChange);

  async function load(loadRequest: AssetEntriesLoadRequest): Promise<void> {
    const request = requests.begin();

    try {
      if (!loadRequest.folderUrl) {
        if (request.isCurrent()) {
          options.clear();
        }
        return;
      }

      const result = await options.search(loadRequest.folderUrl, loadRequest.keyword);
      if (!request.isCurrent()) {
        return;
      }

      const referencesLoaded = await options.loadReferences(result.results, request.isCurrent);
      if (!referencesLoaded || !request.isCurrent()) {
        return;
      }

      await options.apply(result, loadRequest.preferredSelectionUrl, request.isCurrent);
    } catch (error) {
      if (request.isCurrent()) {
        throw error;
      }
    } finally {
      request.complete();
    }
  }

  return {
    load,
    invalidate: requests.invalidate,
    dispose: requests.dispose,
  };
}
