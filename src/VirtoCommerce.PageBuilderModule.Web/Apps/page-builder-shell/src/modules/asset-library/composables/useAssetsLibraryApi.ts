import { useApiClient } from "@vc-shell/framework";
import {
  AssetsClient,
  type BlobEntry,
  type BlobInfo,
  type FileParameter,
} from "../../../api_client/virtocommerce.assets";
import { PageBuilderAssetsClient } from "../../../api_client/virtocommerce.pagebuildermodule";
import { PageBuilderAssetSearchClient } from "../../../api_client/pagebuilderAssetSearch";

import type { AssetEntry, AssetReference, AssetSearchResult, CreateFolderPayload } from "../types";
import type { AssetSearchOptions } from "../utilities/assetEntriesLoader";

export function useAssetsLibraryApi() {
  const { getApiClient: getAssetsClient } = useApiClient(AssetsClient);
  const { getApiClient: getPageBuilderAssetsClient } = useApiClient(PageBuilderAssetsClient);
  const { getApiClient: getSearchClient } = useApiClient(PageBuilderAssetSearchClient);

  async function searchAssets(
    folderUrl: string,
    keyword?: string,
    options?: AssetSearchOptions,
  ): Promise<AssetSearchResult> {
    const client = await getSearchClient();
    const result = await client.search({ folderUrl, keyword, skip: 0, take: 20, ...options });

    return {
      totalCount: result.totalCount ?? result.results?.length ?? 0,
      results: (result.results ?? []).map(mapBlobEntry),
    };
  }

  async function createAssetFolder(payload: CreateFolderPayload): Promise<void> {
    const client = await getAssetsClient();
    await client.createBlobFolder({ name: payload.name, parentUrl: payload.parentUrl });
  }

  async function uploadAsset(folderUrl: string, file: File): Promise<AssetEntry | undefined> {
    const client = await getAssetsClient();
    const payload = await client.uploadAsset(folderUrl, undefined, undefined, toFileParameter(file));
    return payload[0] ? mapBlobEntry(payload[0]) : undefined;
  }

  async function deleteAssets(urls: string[]): Promise<void> {
    const client = await getAssetsClient();
    await client.deleteBlobs(urls);
  }

  async function searchAssetReferences(
    storeId: string,
    assetUrls: string[],
    includePages = true,
  ): Promise<AssetReference[]> {
    const client = await getPageBuilderAssetsClient();
    const result = await client.searchReferences({
      storeId,
      assetUrls,
      includePages,
    });

    return result.results ?? [];
  }

  async function searchFolderReferences(
    storeId: string,
    folderUrl: string,
    includePages = true,
  ): Promise<AssetReference | undefined> {
    const client = await getPageBuilderAssetsClient();
    const result = await client.searchReferences({
      storeId,
      folderUrl,
      includePages,
    });

    return result.results?.[0];
  }

  return { searchAssets, createAssetFolder, uploadAsset, deleteAssets, searchAssetReferences, searchFolderReferences };
}

function mapBlobEntry(entry: BlobEntry): AssetEntry {
  const blobInfo = entry as BlobInfo;

  return {
    type: entry.type === "folder" ? "folder" : "blob",
    name: entry.name ?? "",
    url: entry.url,
    relativeUrl: entry.relativeUrl,
    contentType: blobInfo.contentType,
    size: blobInfo.size,
    modifiedDate: formatDate(entry.modifiedDate),
    createdDate: formatDate(entry.createdDate),
    referencesCount: entry.type === "blob" ? 0 : undefined,
  };
}

function formatDate(value?: Date): string | undefined {
  return value?.toISOString();
}

function toFileParameter(file: File): FileParameter {
  return {
    data: file,
    fileName: file.name,
  };
}
