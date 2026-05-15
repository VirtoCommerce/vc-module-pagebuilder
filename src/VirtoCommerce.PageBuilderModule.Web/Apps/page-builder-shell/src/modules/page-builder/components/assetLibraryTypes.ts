import type { AssetEntry } from "../composables/useAssetsLibraryApi";

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

export interface AssetLibraryDetailsViewModel extends AssetLibraryEntryViewModel {
  dimensions?: string;
  referencePages: NonNullable<AssetEntry["referencePages"]>;
}
