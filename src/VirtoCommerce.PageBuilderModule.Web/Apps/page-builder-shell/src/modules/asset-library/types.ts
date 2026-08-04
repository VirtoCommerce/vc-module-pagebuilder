import type {
  PageBuilderAssetReference,
  PageBuilderAssetReferenceSharedComponent,
  PageBuilderAssetReferencePage,
} from "../../api_client/virtocommerce.pagebuildermodule";

export type AssetReferencePage = PageBuilderAssetReferencePage;
export type AssetReferenceSharedComponent = PageBuilderAssetReferenceSharedComponent;
export type AssetReference = PageBuilderAssetReference;

export interface AssetReferenceDetails {
  referencesCount: number;
  pageReferencesCount: number;
  sharedComponentReferencesCount: number;
  referencePages: AssetReferencePage[];
  referenceSharedComponents: AssetReferenceSharedComponent[];
}

export interface AssetEntry {
  type: "folder" | "blob";
  name: string;
  url?: string;
  relativeUrl?: string;
  contentType?: string;
  size?: number;
  referencesCount?: number;
  pageReferencesCount?: number;
  sharedComponentReferencesCount?: number;
  referencePages?: AssetReferencePage[];
  referenceSharedComponents?: AssetReferenceSharedComponent[];
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

export type AssetLibraryViewMode = "grid" | "table";

export interface AssetLibraryEntryViewModel {
  id: string;
  key: string;
  entry: AssetEntry;
  name: string;
  type: AssetEntry["type"];
  contentType?: string;
  isFolder: boolean;
  isBlob: boolean;
  isImage: boolean;
  selected: boolean;
  dropTarget: boolean;
  dropFolderUrl?: string;
  icon: string;
  previewUrl?: string;
  referencesCount: number;
  formattedSize: string;
  formattedDate: string;
}

export interface AssetLibraryDetailsViewModel extends AssetLibraryEntryViewModel, AssetReferenceDetails {
  dimensions?: string;
}
