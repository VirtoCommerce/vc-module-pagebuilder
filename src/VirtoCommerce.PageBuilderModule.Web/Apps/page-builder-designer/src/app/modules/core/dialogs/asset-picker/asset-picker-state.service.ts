import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { assetLibraryHelpers } from '@core/helpers';
import { AssetLibraryEntry, AssetLibraryService, AssetLibraryUploadCoordinatorService } from '@core/services';

import {
    AssetPickerBreadcrumb,
    AssetPickerDialogData,
    AssetPickerDialogResult,
    AssetPickerGridItem,
} from './asset-picker.models';
import { AssetPickerSelectionState, getAssetPickerEntryKey } from './asset-picker-selection.state';

@Injectable()
export class AssetPickerStateService {

    private readonly data = inject<AssetPickerDialogData>(MAT_DIALOG_DATA);
    private readonly assets = inject(AssetLibraryService);
    private readonly uploadCoordinator = inject(AssetLibraryUploadCoordinatorService);
    private readonly destroyRef = inject(DestroyRef);
    private readonly acceptedTypes = (this.data.accept ?? []).map(x => x.trim()).filter(Boolean);
    private readonly maxFileSize = this.data.maxFileSize;
    private searchTimeout: ReturnType<typeof setTimeout> | null = null;
    private dragDepth = 0;
    private requestId = 0;

    readonly labels = this.assets.getLabels();
    readonly title = this.data.title || this.labels.title;
    readonly multiple = this.data.multiple === true;
    private readonly context = this.data.context ?? null;
    private readonly selection = new AssetPickerSelectionState(this.assets, this.acceptedTypes, this.multiple, this.context);
    readonly rootFolderUrl = this.data.rootFolderUrl;
    readonly currentFolderUrl = signal(this.data.rootFolderUrl);
    readonly entries = signal<AssetLibraryEntry[]>([]);
    readonly selectedAssets = this.selection.selectedAssets;
    readonly selectedCount = this.selection.selectedCount;
    readonly selectButtonText = computed(() => this.multiple && this.selectedCount() > 1
        ? `${this.labels.select} (${this.selectedCount()})`
        : this.labels.select);
    readonly searchValue = signal('');
    readonly loading = signal(false);
    readonly uploading = signal(false);
    readonly dragging = signal(false);
    readonly folderDropTarget = signal<string | null>(null);
    readonly error = signal<string | null>(null);
    readonly acceptAttribute = computed(() => this.acceptedTypes.length ? this.acceptedTypes.join(',') : null);
    readonly breadcrumbs = computed(() => this.buildBreadcrumbs());
    readonly visibleEntries = computed(() => this.entries()
        .filter(entry => entry.type === 'folder' || this.selection.matchesAccept(entry))
        .map(entry => this.toGridItem(entry)));
    readonly visibleAssetsCount = computed(() => this.entries()
        .filter(entry => entry.type === 'blob' && this.selection.matchesAccept(entry))
        .length);

    constructor() {
        this.destroyRef.onDestroy(() => {
            if (this.searchTimeout) {
                clearTimeout(this.searchTimeout);
            }
        });
        this.loadEntries();
    }

    formatAssetsCounter(count: number): string {
        return this.labels.assetsCounter.replace('{count}', count.toString());
    }

    onSearch(value: string) {
        this.searchValue.set(value);
        if (this.searchTimeout) {
            clearTimeout(this.searchTimeout);
        }
        this.searchTimeout = setTimeout(() => this.loadEntries(), 300);
    }

    onDragEnter(event: DragEvent) {
        if (!this.hasDraggedFiles(event)) {
            return;
        }

        this.preventDragDefaults(event);
        this.dragDepth += 1;
        this.dragging.set(true);
    }

    onDragOver(event: DragEvent) {
        if (!this.hasDraggedFiles(event)) {
            return;
        }

        this.preventDragDefaults(event);
    }

    onDragLeave(event: DragEvent) {
        if (!this.hasDraggedFiles(event)) {
            return;
        }

        this.preventDragDefaults(event);
        this.dragDepth = Math.max(this.dragDepth - 1, 0);
        if (!this.dragDepth) {
            this.dragging.set(false);
            this.folderDropTarget.set(null);
        }
    }

    onDrop(event: DragEvent) {
        this.preventDragDefaults(event);
        this.resetDragState();
        this.uploadFiles(Array.from(event.dataTransfer?.files ?? []));
    }

    onFolderDragOver(event: DragEvent, folder: AssetLibraryEntry) {
        if (!this.hasDraggedFiles(event)) {
            return;
        }

        this.preventDragDefaults(event);
        this.folderDropTarget.set(getAssetPickerEntryKey(folder));
    }

    onFolderDragLeave(event: DragEvent, folder: AssetLibraryEntry) {
        if (!this.hasDraggedFiles(event) || this.folderDropTarget() !== getAssetPickerEntryKey(folder)) {
            return;
        }

        this.preventDragDefaults(event);
        this.folderDropTarget.set(null);
    }

    onFolderDrop(event: DragEvent, folder: AssetLibraryEntry) {
        if (folder.type !== 'folder') {
            return;
        }

        this.preventDragDefaults(event);
        this.resetDragState();
        this.uploadFiles(Array.from(event.dataTransfer?.files ?? []), folder.relativeUrl || folder.url || this.currentFolderUrl());
    }

    navigateTo(entry: AssetLibraryEntry) {
        if (entry.type !== 'folder') {
            return;
        }

        this.currentFolderUrl.set(entry.relativeUrl || entry.url || this.currentFolderUrl());
        this.selection.clearSingleSelection();
        this.loadEntries();
    }

    navigateToBreadcrumb(url: string) {
        this.currentFolderUrl.set(url);
        this.selection.clearSingleSelection();
        this.loadEntries();
    }

    selectAsset(entry: AssetLibraryEntry) {
        this.selection.selectAsset(entry);
    }

    getSelectionResult(): AssetPickerDialogResult | null {
        return this.selection.getSelectionResult();
    }

    uploadFiles(files: File[], folderUrl = this.currentFolderUrl()) {
        const acceptedFiles = files.filter(file => this.matchesAcceptFile(file));
        const oversizedFiles = acceptedFiles.filter(file => this.isFileTooLarge(file));
        const uploadFiles = acceptedFiles
            .filter(file => !this.isFileTooLarge(file))
            .slice(0, this.multiple ? undefined : 1);

        if (oversizedFiles.length) {
            this.error.set(this.labels.fileTooLarge.replace('{maxSize}', assetLibraryHelpers.formatAssetSize(this.maxFileSize ?? 0)));
            return;
        }

        if (!uploadFiles.length || this.uploading()) {
            return;
        }

        this.searchValue.set('');
        this.uploading.set(true);
        this.error.set(null);

        this.uploadCoordinator.uploadFiles(folderUrl, uploadFiles, this.context).pipe(
            takeUntilDestroyed(this.destroyRef)
        ).subscribe({
            next: uploaded => {
                const preferredSelectionUrls = uploaded.map(entry => entry.relativeUrl || entry.url).filter((url): url is string => !!url);
                this.uploading.set(false);
                if (uploaded.length) {
                    this.selectedAssets.set(this.multiple ? this.selection.mergeSelectedAssets(uploaded) : uploaded.slice(-1));
                }

                let preferredSelectionUrl: string | string[] | undefined;
                if (folderUrl === this.currentFolderUrl()) {
                    preferredSelectionUrl = this.multiple ? preferredSelectionUrls : preferredSelectionUrls.at(-1);
                }

                this.loadEntries(preferredSelectionUrl);
            },
            error: error => {
                this.uploading.set(false);
                this.error.set(error?.message || this.labels.uploadError);
            }
        });
    }

    private buildBreadcrumbs(): AssetPickerBreadcrumb[] {
        const rootParts = this.rootFolderUrl.split('/').filter(Boolean);
        const currentParts = this.currentFolderUrl().split('/').filter(Boolean);
        const parts = currentParts.slice(rootParts.length);
        const result = [{ label: this.labels.rootBreadcrumb, url: this.rootFolderUrl }];

        parts.reduce((url, part) => {
            const nextUrl = `${url}/${part}`;
            result.push({ label: assetLibraryHelpers.safeDecodeURIComponent(part), url: nextUrl });
            return nextUrl;
        }, this.rootFolderUrl);

        return result;
    }

    private toGridItem(entry: AssetLibraryEntry): AssetPickerGridItem {
        return {
            entry,
            key: getAssetPickerEntryKey(entry),
            previewUrl: this.assets.isImage(entry) ? this.assets.getPreviewUrl(entry, this.context) : null,
            size: entry.type === 'blob' ? assetLibraryHelpers.formatAssetSize(entry.size ?? 0) : null,
            selected: this.selection.isAssetSelected(entry),
            folderDropTarget: entry.type === 'folder' && this.folderDropTarget() === getAssetPickerEntryKey(entry),
        };
    }

    private loadEntries(preferredSelectionUrl?: string | string[]) {
        const requestId = ++this.requestId;
        this.loading.set(true);
        this.error.set(null);
        this.assets.search(this.currentFolderUrl(), this.searchValue()).pipe(
            takeUntilDestroyed(this.destroyRef)
        ).subscribe({
            next: result => {
                if (requestId !== this.requestId) {
                    return;
                }
                this.entries.set(result.results);
                if (preferredSelectionUrl) {
                    const preferredSelectionUrls = Array.isArray(preferredSelectionUrl) ? preferredSelectionUrl : [preferredSelectionUrl];
                    const selected = preferredSelectionUrls
                        .map(url => this.selection.findEntry(result.results, url))
                        .filter((entry): entry is AssetLibraryEntry => !!entry);
                    this.selectedAssets.set(this.multiple ? this.selection.mergeSelectedAssets(selected) : selected.slice(-1));
                } else {
                    this.selection.restoreSelection(result.results);
                }
                this.loading.set(false);
            },
            error: error => {
                if (requestId !== this.requestId) {
                    return;
                }
                this.error.set(error?.message || this.labels.loadError);
                this.entries.set([]);
                this.loading.set(false);
            }
        });
    }

    private matchesAcceptFile(file: File): boolean {
        return assetLibraryHelpers.matchesAcceptFile(file, this.acceptedTypes);
    }

    private isFileTooLarge(file: File): boolean {
        return !!this.maxFileSize && file.size > this.maxFileSize;
    }

    private hasDraggedFiles(event: DragEvent): boolean {
        return Array.from(event.dataTransfer?.types ?? []).includes('Files');
    }

    private preventDragDefaults(event: DragEvent) {
        event.preventDefault();
        event.stopPropagation();
        if (event.dataTransfer) {
            event.dataTransfer.dropEffect = 'copy';
        }
    }

    private resetDragState() {
        this.dragDepth = 0;
        this.dragging.set(false);
        this.folderDropTarget.set(null);
    }

}
