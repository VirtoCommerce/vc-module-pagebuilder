import { computed, signal } from '@angular/core';

import { assetLibraryHelpers } from '@core/helpers';
import { AssetLibraryEntry, AssetLibraryService } from '@core/services';

import { AssetPickerDialogItem, AssetPickerDialogResult } from './asset-picker.models';

export function getAssetPickerEntryKey(entry: AssetLibraryEntry | null | undefined): string {
    return entry?.relativeUrl || entry?.url || entry?.name || '';
}

export class AssetPickerSelectionState {
    readonly selectedAssets = signal<AssetLibraryEntry[]>([]);
    readonly selectedCount = computed(() => this.selectedAssets().length);

    constructor(
        private readonly assets: AssetLibraryService,
        private readonly acceptedTypes: string[],
        private readonly multiple: boolean
    ) {
    }

    selectAsset(entry: AssetLibraryEntry) {
        if (entry.type !== 'blob' || !this.matchesAccept(entry)) {
            return;
        }

        if (!this.multiple) {
            this.selectedAssets.set([entry]);
            return;
        }

        if (this.isAssetSelected(entry)) {
            this.selectedAssets.update(items => items.filter(item => getAssetPickerEntryKey(item) !== getAssetPickerEntryKey(entry)));
        } else {
            this.selectedAssets.update(items => [...items, entry]);
        }
    }

    clearSingleSelection() {
        if (!this.multiple) {
            this.selectedAssets.set([]);
        }
    }

    restoreSelection(entries: AssetLibraryEntry[]) {
        if (!this.multiple) {
            const selected = this.selectedAssets()
                .map(entry => this.findEntry(entries, getAssetPickerEntryKey(entry)))
                .find((entry): entry is AssetLibraryEntry => !!entry);

            this.selectedAssets.set(selected ? [selected] : []);
            return;
        }

        this.selectedAssets.set(this.mergeSelectedAssets([], entries));
    }

    getSelectionResult(): AssetPickerDialogResult | null {
        const result = this.selectedAssets()
            .map(entry => this.toDialogResult(entry))
            .filter((item): item is AssetPickerDialogItem => !!item);

        if (!result.length) {
            return null;
        }

        return this.multiple ? result : result[0];
    }

    mergeSelectedAssets(assets: AssetLibraryEntry[], refreshedEntries: AssetLibraryEntry[] = []): AssetLibraryEntry[] {
        const selectedByKey = new Map<string, AssetLibraryEntry>();

        this.selectedAssets().forEach(entry => {
            const key = getAssetPickerEntryKey(entry);
            if (key) {
                selectedByKey.set(key, entry);
            }
        });

        refreshedEntries.forEach(entry => {
            const key = getAssetPickerEntryKey(entry);
            if (key && selectedByKey.has(key)) {
                selectedByKey.set(key, entry);
            }
        });

        assets.forEach(entry => {
            const key = getAssetPickerEntryKey(entry);
            if (key) {
                selectedByKey.set(key, entry);
            }
        });

        return [...selectedByKey.values()];
    }

    findEntry(entries: AssetLibraryEntry[], key: string): AssetLibraryEntry | undefined {
        return entries.find(entry => getAssetPickerEntryKey(entry) === key);
    }

    isAssetSelected(entry: AssetLibraryEntry): boolean {
        const key = getAssetPickerEntryKey(entry);
        return !!key && this.selectedAssets().some(item => getAssetPickerEntryKey(item) === key);
    }

    matchesAccept(entry: AssetLibraryEntry): boolean {
        if (!this.acceptedTypes.length) {
            return true;
        }

        const contentType = entry.contentType?.toLowerCase() || '';
        const fileName = entry.name.toLowerCase();

        return this.acceptedTypes.some(type => {
            const accept = type.toLowerCase();
            if (accept.endsWith('/*')) {
                return contentType.startsWith(accept.slice(0, -1))
                    || (accept === 'image/*' && this.assets.isImage(entry));
            }
            if (accept.startsWith('.')) {
                return fileName.endsWith(accept);
            }
            return contentType === accept;
        });
    }

    private toDialogResult(entry: AssetLibraryEntry): AssetPickerDialogItem | null {
        const url = this.assets.getPublicUrl(entry);
        if (!url) {
            return null;
        }

        return {
            entry,
            url,
            previewUrl: this.assets.getPreviewUrl(entry)
        };
    }
}
