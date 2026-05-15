import { inject, Injectable } from '@angular/core';

import { assetLibraryHelpers } from '@core/helpers';
import { AppConfig } from '@integration/services';

import { AssetLibraryContext, AssetLibraryEntry } from './asset-library.models';

@Injectable({
    providedIn: 'root'
})
export class AssetUrlService {

    private readonly appConfig = inject(AppConfig);

    getRootFolderUrl(context: AssetLibraryContext | null = null): string | null {
        const storeId = this.getStoreId(context);

        return storeId ? `/stores/${storeId}/Page Builder` : null;
    }

    getPublicUrl(entry: AssetLibraryEntry): string | null {
        const entryUrl = this.getEntryUrl(entry);
        return entryUrl ? assetLibraryHelpers.toPublicAssetUrl(entryUrl) : null;
    }

    getPreviewUrl(entry: AssetLibraryEntry): string | null {
        const entryUrl = this.getEntryUrl(entry);
        if (!entryUrl) {
            return null;
        }

        return assetLibraryHelpers.toAssetPreviewUrl(entryUrl, entry.modifiedDate);
    }

    getEntryUrl(entry: AssetLibraryEntry): string | null {
        return entry.url?.trim() || entry.relativeUrl || null;
    }

    getStoreId(context: AssetLibraryContext | null = null): string | null {
        const appContext = this.appConfig.getContext() as AssetLibraryContext;
        const storeId = context?.location?.params?.storeId
            ?? context?.template?.storeId
            ?? context?.model?.storeId
            ?? appContext?.location?.params?.storeId;

        return Array.isArray(storeId) ? storeId[0] ?? null : storeId ?? null;
    }

    isImage(entry: AssetLibraryEntry): boolean {
        return entry.contentType?.startsWith('image/') === true
            || assetLibraryHelpers.isImageFileName(entry.name);
    }
}
