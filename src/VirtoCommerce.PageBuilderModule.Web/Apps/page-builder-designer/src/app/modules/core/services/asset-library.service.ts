import { inject, Injectable } from '@angular/core';
import { map, Observable, throwError } from 'rxjs';

import { AssetLibraryApiService } from './asset-library-api.service';
import {
    AssetLibraryContext,
    AssetLibraryEntry,
    AssetLibraryLabels,
    AssetLibraryReferencesSearchResult,
    AssetLibrarySearchResult,
} from './asset-library.models';
import { AssetUrlService } from './asset-url.service';

export type {
    AssetLibraryContext,
    AssetLibraryEntry,
    AssetLibraryLabels,
    AssetLibraryReference,
    AssetLibraryReferencePage,
    AssetLibraryReferencesSearchResult,
    AssetLibrarySearchResult,
} from './asset-library.models';

const fallbackLabels: AssetLibraryLabels = {
    title: 'Choose from asset library',
    rootBreadcrumb: 'Page Builder',
    choose: 'Choose from Asset Library',
    chooseOrDrop: 'Choose from Asset Library or drag and drop here',
    fileLabel: 'File',
    imageLabel: 'Image',
    upload: 'Upload',
    uploading: 'Uploading...',
    searchPlaceholder: 'Search assets...',
    assetsCounter: '{count} assets',
    loading: 'Loading assets...',
    empty: 'No assets found.',
    cancel: 'Cancel',
    select: 'Select',
    storeRequired: 'Store context is required to open Asset Library.',
    uploadError: 'Unable to upload asset.',
    loadError: 'Unable to load assets.',
    fileTooLarge: 'File is too large. Maximum size is {maxSize}.',
    overwriteTitle: 'Replace existing asset?',
    overwriteUnused: '{name} is not used on any Page Builder pages. Replacing it will update the existing file.',
    overwriteUsedOne: '{name} is used on 1 Page Builder page. Replacing it will change the asset on that page.',
    overwriteUsedMany: '{name} is used on {count} Page Builder pages. Replacing it will change the asset on all of them.',
    overwriteBatchDuplicate: 'Another file in this upload is already named {name}. Keeping the same name will upload only the last one.',
    affectedPages: 'Affected Page Builder pages',
    uploadAs: 'Upload as',
    replace: 'Replace',
    fileNameRequired: 'File name is required.',
    fileNameInvalid: 'File name must not contain path separators.',
    fileNameCollision: '{name} already exists in this folder. Enter a different file name.'
};

@Injectable({
    providedIn: 'root'
})
export class AssetLibraryService {

    private readonly api = inject(AssetLibraryApiService);
    private readonly urls = inject(AssetUrlService);

    getRootFolderUrl(context: AssetLibraryContext | null = null): string | null {
        return this.urls.getRootFolderUrl(context);
    }

    getLabels(): AssetLibraryLabels {
        return { ...fallbackLabels };
    }

    search(folderUrl: string, keyword?: string): Observable<AssetLibrarySearchResult> {
        return this.api.search(folderUrl, keyword);
    }

    upload(folderUrl: string, file: File): Observable<AssetLibraryEntry | null> {
        return this.api.upload(folderUrl, file);
    }

    searchReferences(context: AssetLibraryContext | null, entry: AssetLibraryEntry): Observable<AssetLibraryReferencesSearchResult> {
        const storeId = this.urls.getStoreId(context);
        const assetUrl = entry.relativeUrl || entry.url;

        if (!storeId || !assetUrl) {
            return throwError(() => new Error('Store context and asset URL are required to check asset references.'));
        }

        return this.api.searchReferences(storeId, [assetUrl]);
    }

    findByName(folderUrl: string, fileName: string): Observable<AssetLibraryEntry | null> {
        const normalized = normalizeFileName(fileName);
        return this.search(folderUrl, fileName).pipe(
            map(result => result.results.find(item => item.type === 'blob' && normalizeFileName(item.name) === normalized) ?? null)
        );
    }

    getStoreId(context: AssetLibraryContext | null = null): string | null {
        return this.urls.getStoreId(context);
    }

    getPublicUrl(entry: AssetLibraryEntry, context: AssetLibraryContext | null = null): string | null {
        return this.urls.getPublicUrl(entry, context);
    }

    getPreviewUrl(entry: AssetLibraryEntry, context: AssetLibraryContext | null = null): string | null {
        return this.urls.getPreviewUrl(entry, context);
    }

    isImage(entry: AssetLibraryEntry): boolean {
        return this.urls.isImage(entry);
    }
}

function normalizeFileName(value: string): string {
    try {
        return decodeURIComponent(value).trim().normalize('NFC');
    } catch {
        return value.trim().normalize('NFC');
    }
}
