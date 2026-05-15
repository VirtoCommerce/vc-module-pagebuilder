import { AssetLibraryEntry } from '@core/services';

export interface AssetPickerDialogData {
    rootFolderUrl: string;
    accept?: string[];
    title?: string;
    multiple?: boolean;
    maxFileSize?: number;
}

export interface AssetPickerDialogItem {
    entry: AssetLibraryEntry;
    url: string;
    previewUrl: string | null;
}

export type AssetPickerDialogResult = AssetPickerDialogItem | AssetPickerDialogItem[];

export interface AssetPickerBreadcrumb {
    label: string;
    url: string;
}

export interface AssetPickerGridItem {
    entry: AssetLibraryEntry;
    key: string;
    previewUrl: string | null;
    size: string | null;
    selected: boolean;
    folderDropTarget: boolean;
}

export interface AssetPickerEntryDragEvent {
    entry: AssetLibraryEntry;
    event: DragEvent;
}
