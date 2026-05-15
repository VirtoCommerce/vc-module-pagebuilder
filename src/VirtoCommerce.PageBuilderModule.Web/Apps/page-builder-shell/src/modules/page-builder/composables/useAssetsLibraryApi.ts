import { useApiClient } from "@vc-shell/framework";
import {
  ApiException,
  AssetsClient as GeneratedAssetsClient,
  BlobEntrySearchResult,
  BlobFolder,
  BlobInfo,
  type BlobEntry,
  type FileParameter,
} from "../../../api_client/virtocommerce.assets";
import {
  type PageBuilderAssetReference,
  type PageBuilderAssetReferencePage,
  PageBuilderAssetReferencesSearchCriteria,
  PageBuilderAssetsClient,
} from "../../../api_client/virtocommerce.pagebuildermodule";

export type AssetReferencePage = PageBuilderAssetReferencePage;
export type AssetReference = PageBuilderAssetReference;

export interface AssetEntry {
  type: "folder" | "blob";
  name: string;
  url?: string;
  relativeUrl?: string;
  contentType?: string;
  size?: number;
  referencesCount?: number;
  referencePages?: AssetReferencePage[];
  modifiedDate?: string;
  createdDate?: string;
}

export interface AssetSearchResult {
  totalCount: number;
  results: AssetEntry[];
}

export interface CreateFolderPayload {
  name: string;
  parentUrl: string;
}

class AssetLibraryAssetsClient extends GeneratedAssetsClient {
  protected override processSearchAssetItems(response: Response): Promise<BlobEntrySearchResult> {
    const status = response.status;
    const headers: Record<string, string> = {};
    response.headers?.forEach((value, key) => {
      headers[key] = value;
    });

    if (status === 200) {
      return response.text().then(responseText => {
        const data = responseText === "" ? null : JSON.parse(responseText, this.jsonParseReviver);
        return new BlobEntrySearchResult({
          totalCount: data?.totalCount,
          results: Array.isArray(data?.results) ? data.results.map((entry: any) => createBlobEntry(entry)) : [],
        });
      });
    }

    if (status !== 200 && status !== 204) {
      return response.text().then(responseText => {
        throw new ApiException("An unexpected server error occurred.", status, responseText, headers, null);
      });
    }

    return Promise.resolve(new BlobEntrySearchResult());
  }
}

const { getApiClient: getAssetsClient } = useApiClient(AssetLibraryAssetsClient);
const { getApiClient: getPageBuilderAssetsClient } = useApiClient(PageBuilderAssetsClient);

export async function searchAssets(folderUrl: string, keyword?: string): Promise<AssetSearchResult> {
  const client = await getAssetsClient();
  const result = await client.searchAssetItems(folderUrl, keyword);

  return {
    totalCount: result.totalCount ?? result.results?.length ?? 0,
    results: (result.results ?? []).map(mapBlobEntry),
  };
}

export async function createAssetFolder(payload: CreateFolderPayload): Promise<void> {
  const client = await getAssetsClient();
  await client.createBlobFolder(new BlobFolder(payload));
}

export async function uploadAsset(folderUrl: string, file: File): Promise<AssetEntry | undefined> {
  const client = await getAssetsClient();
  const payload = await client.uploadAsset(folderUrl, undefined, undefined, toFileParameter(file));
  return payload[0] ? mapBlobEntry(payload[0]) : undefined;
}

export async function deleteAssets(urls: string[]): Promise<void> {
  const client = await getAssetsClient();
  await client.deleteBlobs(urls);
}

export async function searchAssetReferences(storeId: string, assetUrls: string[], includePages = true): Promise<AssetReference[]> {
  const client = await getPageBuilderAssetsClient();
  const result = await client.searchReferences(
    new PageBuilderAssetReferencesSearchCriteria({
      storeId,
      assetUrls,
      includePages,
    }),
  );

  return result.results ?? [];
}

function createBlobEntry(entry: any): BlobEntry {
  return entry?.type === "blob"
    ? BlobInfo.fromJS(entry)
    : BlobFolder.fromJS(entry);
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
