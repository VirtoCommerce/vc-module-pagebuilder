import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { NgClass, NgStyle } from '@angular/common';
import { MatDialogActions, MatDialogContent, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { concatMap, from, toArray } from 'rxjs';

import { IconComponent } from '@core/components/icon/icon.component';
import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';
import { AssetLibraryEntry, AssetLibraryService } from '@core/services';

export interface AssetPickerDialogData {
    rootFolderUrl: string;
    accept?: string[];
    title?: string;
}

export interface AssetPickerDialogResult {
    entry: AssetLibraryEntry;
    url: string;
    previewUrl: string | null;
}

@Component({
    selector: 'app-asset-picker',
    templateUrl: './asset-picker.component.html',
    styleUrls: ['./asset-picker.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgClass, NgStyle, MatDialogContent, MatDialogActions, IconComponent, IconButtonComponent]
})
export class AssetPickerComponent {

    private readonly dialogRef = inject(MatDialogRef<AssetPickerComponent, AssetPickerDialogResult | null>);
    private readonly data = inject<AssetPickerDialogData>(MAT_DIALOG_DATA);
    private readonly assets = inject(AssetLibraryService);
    private readonly destroyRef = inject(DestroyRef);
    private readonly acceptedTypes = (this.data.accept ?? []).map(x => x.trim()).filter(Boolean);
    private searchTimeout: ReturnType<typeof setTimeout> | null = null;
    private requestId = 0;

    readonly title = this.data.title || 'Choose from asset library';
    readonly rootFolderUrl = this.data.rootFolderUrl;
    readonly currentFolderUrl = signal(this.data.rootFolderUrl);
    readonly entries = signal<AssetLibraryEntry[]>([]);
    readonly selectedAsset = signal<AssetLibraryEntry | null>(null);
    readonly searchValue = signal('');
    readonly loading = signal(false);
    readonly uploading = signal(false);
    readonly error = signal<string | null>(null);

    constructor() {
        this.destroyRef.onDestroy(() => {
            if (this.searchTimeout) {
                clearTimeout(this.searchTimeout);
            }
        });
        this.loadEntries();
    }

    get visibleEntries(): AssetLibraryEntry[] {
        return this.entries().filter(entry => entry.type === 'folder' || this.matchesAccept(entry));
    }

    get acceptAttribute(): string | null {
        return this.acceptedTypes.length ? this.acceptedTypes.join(',') : null;
    }

    get breadcrumbs(): { label: string; url: string }[] {
        const rootParts = this.rootFolderUrl.split('/').filter(Boolean);
        const currentParts = this.currentFolderUrl().split('/').filter(Boolean);
        const parts = currentParts.slice(rootParts.length);
        const result = [{ label: 'Page Builder', url: this.rootFolderUrl }];

        parts.reduce((url, part) => {
            const nextUrl = `${url}/${part}`;
            result.push({ label: decodeURIComponent(part), url: nextUrl });
            return nextUrl;
        }, this.rootFolderUrl);

        return result;
    }

    getEntryKey(entry: AssetLibraryEntry): string {
        return entry.relativeUrl || entry.url || entry.name;
    }

    getPreviewStyle(entry: AssetLibraryEntry): string | null {
        if (!this.assets.isImage(entry)) {
            return null;
        }

        const previewUrl = this.assets.getPreviewUrl(entry);
        return previewUrl ? `url("${this.escapeCssUrl(previewUrl)}")` : null;
    }

    getSize(entry: AssetLibraryEntry): string {
        const size = entry.size ?? 0;
        if (size < 1024) {
            return `${size} B`;
        }
        if (size < 1024 * 1024) {
            return `${Math.round(size / 1024)} KB`;
        }
        return `${(size / 1024 / 1024).toFixed(1)} MB`;
    }

    onSearch(value: string) {
        this.searchValue.set(value);
        if (this.searchTimeout) {
            clearTimeout(this.searchTimeout);
        }
        this.searchTimeout = setTimeout(() => this.loadEntries(), 300);
    }

    onUpload(event: Event) {
        const input = event.target as HTMLInputElement;
        const files = Array.from(input.files ?? []);
        input.value = '';

        if (!files.length) {
            return;
        }

        this.searchValue.set('');
        this.uploading.set(true);
        this.error.set(null);

        from(files).pipe(
            concatMap(file => this.assets.upload(this.currentFolderUrl(), file)),
            toArray(),
            takeUntilDestroyed(this.destroyRef)
        ).subscribe({
            next: uploaded => {
                const uploadedEntries = uploaded.filter((entry): entry is AssetLibraryEntry => !!entry);
                const preferredSelectionUrl = uploadedEntries[uploadedEntries.length - 1]?.relativeUrl;
                this.uploading.set(false);
                if (uploadedEntries.length) {
                    this.selectedAsset.set(uploadedEntries[uploadedEntries.length - 1] ?? null);
                }
                this.loadEntries(preferredSelectionUrl);
            },
            error: error => {
                this.uploading.set(false);
                this.error.set(error?.message || 'Unable to upload asset.');
            }
        });
    }

    navigateTo(entry: AssetLibraryEntry) {
        if (entry.type !== 'folder') {
            return;
        }

        this.currentFolderUrl.set(entry.relativeUrl || entry.url || this.currentFolderUrl());
        this.selectedAsset.set(null);
        this.loadEntries();
    }

    navigateToBreadcrumb(url: string) {
        this.currentFolderUrl.set(url);
        this.selectedAsset.set(null);
        this.loadEntries();
    }

    selectAsset(entry: AssetLibraryEntry) {
        if (entry.type !== 'blob' || !this.matchesAccept(entry)) {
            return;
        }

        this.selectedAsset.set(entry);
    }

    confirm() {
        const entry = this.selectedAsset();
        if (!entry) {
            return;
        }

        const url = this.assets.getPublicUrl(entry);
        if (!url) {
            return;
        }

        this.dialogRef.close({
            entry,
            url,
            previewUrl: this.assets.getPreviewUrl(entry)
        });
    }

    decline() {
        this.dialogRef.close(null);
    }

    private loadEntries(preferredSelectionUrl?: string) {
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
                    this.selectedAsset.set(result.results.find(entry => entry.relativeUrl === preferredSelectionUrl) ?? this.selectedAsset());
                } else if (this.selectedAsset() && !result.results.includes(this.selectedAsset()!)) {
                    this.selectedAsset.set(null);
                }
                this.loading.set(false);
            },
            error: error => {
                if (requestId !== this.requestId) {
                    return;
                }
                this.error.set(error?.message || 'Unable to load assets.');
                this.entries.set([]);
                this.selectedAsset.set(null);
                this.loading.set(false);
            }
        });
    }

    private matchesAccept(entry: AssetLibraryEntry): boolean {
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

    private escapeCssUrl(value: string): string {
        return value
            .replace(/\\/g, '\\\\')
            .replace(/"/g, '\\"')
            .replace(/\r?\n/g, '%0A');
    }
}
