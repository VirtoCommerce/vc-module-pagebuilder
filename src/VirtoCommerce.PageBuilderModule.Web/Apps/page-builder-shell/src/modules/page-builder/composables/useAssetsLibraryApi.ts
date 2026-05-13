import { useApiClient } from "@vc-shell/framework";
import { AssetsClient, AssetEntry, AssetReference, AssetSearchResult, CreateFolderPayload } from "../../../api_client/virtocommerce.assets";

const { getApiClient } = useApiClient(AssetsClient);

export type { AssetEntry, AssetSearchResult };

export async function searchAssets(folderUrl: string, keyword?: string): Promise<AssetSearchResult> {
  const client = await getApiClient();
  return client.search(folderUrl, keyword);
}

export async function createAssetFolder(payload: CreateFolderPayload): Promise<void> {
  const client = await getApiClient();
  await client.createFolder(payload);
}

export async function uploadAsset(folderUrl: string, file: File): Promise<AssetEntry | undefined> {
  const client = await getApiClient();
  const payload = await client.upload(folderUrl, file);
  return payload[0];
}

export async function deleteAssets(urls: string[]): Promise<void> {
  const client = await getApiClient();
  await client.delete(urls);
}

export async function searchAssetReferences(storeId: string, assetUrls: string[], includePages = true): Promise<AssetReference[]> {
  const client = await getApiClient();
  const result = await client.searchReferences({
    storeId,
    assetUrls,
    includePages,
  });

  return result.results;
}
