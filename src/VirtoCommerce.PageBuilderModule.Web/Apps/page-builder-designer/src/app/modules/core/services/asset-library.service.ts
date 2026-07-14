import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { AssetLibraryApiService } from './asset-library-api.service';
import {
    AssetLibraryContext,
    AssetLibraryEntry,
    AssetLibraryLabels,
    AssetLibrarySearchResult,
} from './asset-library.models';
import { AssetUrlService } from './asset-url.service';

export type {
    AssetLibraryContext,
    AssetLibraryEntry,
    AssetLibraryLabels,
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
    fileTooLarge: 'File is too large. Maximum size is {maxSize}.'
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
