import type { AssetEntry, AssetLibraryDetailsViewModel, AssetLibraryEntryViewModel } from "../types";

interface EntryViewModelContext {
  selectedEntryKey: string;
  draggedFolderUrl?: string;
  notAvailableText: string;
  getEntryDropFolderUrl: (entry: AssetEntry) => string | undefined;
  isImage: (entry: AssetEntry | undefined) => boolean;
  getEntryIcon: (entry: AssetEntry) => string;
  getReferencesCount: (entry: AssetEntry) => number;
  formatFileSize: (size?: number) => string;
  formatDate: (value?: string) => string;
  getPreviewUrl: (entry: AssetEntry | undefined) => string | undefined;
}

interface DetailsViewModelContext extends EntryViewModelContext {
  dimensions?: string;
  getReferencePages: (entry: AssetEntry | undefined) => NonNullable<AssetEntry["referencePages"]>;
}

export function getAssetEntryKey(entry: AssetEntry | undefined): string {
  return entry?.relativeUrl || entry?.url || entry?.name || "";
}

export function createAssetLibraryEntryViewModel(
  entry: AssetEntry,
  context: EntryViewModelContext,
): AssetLibraryEntryViewModel {
  const key = getAssetEntryKey(entry);
  const dropFolderUrl = context.getEntryDropFolderUrl(entry);
  const isBlob = entry.type === "blob";

  return {
    id: key,
    key,
    entry,
    name: entry.name,
    type: entry.type,
    contentType: entry.contentType,
    isFolder: entry.type === "folder",
    isBlob,
    isImage: context.isImage(entry),
    selected: context.selectedEntryKey === key,
    dropTarget: !!dropFolderUrl && context.draggedFolderUrl === dropFolderUrl,
    dropFolderUrl,
    icon: context.getEntryIcon(entry),
    previewUrl: context.getPreviewUrl(entry),
    referencesCount: context.getReferencesCount(entry),
    formattedSize: isBlob ? context.formatFileSize(entry.size) : context.notAvailableText,
    formattedDate: context.formatDate(entry.modifiedDate || entry.createdDate),
  };
}

export function createAssetLibraryDetailsViewModel(
  entry: AssetEntry,
  context: DetailsViewModelContext,
): AssetLibraryDetailsViewModel {
  return {
    ...createAssetLibraryEntryViewModel(entry, context),
    dimensions: context.dimensions,
    referencePages: context.getReferencePages(entry),
  };
}
