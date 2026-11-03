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

        if (!storeId) {
            return null;
        }

        return this.appConfig.getValue('assetLibraryRootFolderUrl', this.getAssetContext(context, { storeId })) || null;
    }

    getPublicUrl(entry: AssetLibraryEntry, context: AssetLibraryContext | null = null): string | null {
        const entryUrl = this.getEntryUrl(entry);
        return this.getPublicAssetUrl(entryUrl, context);
    }

    getPreviewUrl(entry: AssetLibraryEntry, context: AssetLibraryContext | null = null): string | null {
        const assetUrl = this.getEntryUrl(entry);
        if (!assetUrl) {
            return null;
        }

        const publicAssetUrl = this.getPublicAssetUrl(assetUrl, context);
        if (!publicAssetUrl) {
            return null;
        }

        return this.getConfiguredAssetUrl('assetPreviewUrlTemplate', assetUrl, context, {
            publicAssetUrl,
            modifiedDate: entry.modifiedDate ?? ''
        });
    }

    getPublicAssetUrl(value: unknown, context: AssetLibraryContext | null = null): string | null {
        if (typeof value !== 'string') {
            return null;
        }

        const assetUrl = value.trim();
        if (!assetUrl) {
            return null;
        }

        return this.getConfiguredAssetUrl('assetsUrlTemplate', assetUrl, context);
    }

    getEntryUrl(entry: AssetLibraryEntry): string | null {
        return entry.url?.trim() || entry.relativeUrl || null;
    }

    getStoreId(context: AssetLibraryContext | null = null): string | null {
        const appContext = this.appConfig.getContext() as AssetLibraryContext;
        const storeId = context?.storeId
            ?? context?.location?.params?.storeId
            ?? context?.template?.storeId
            ?? context?.model?.storeId
            ?? appContext?.location?.params?.storeId;

        return Array.isArray(storeId) ? storeId[0] ?? null : storeId ?? null;
    }

    isImage(entry: AssetLibraryEntry): boolean {
        return entry.contentType?.startsWith('image/') === true
            || assetLibraryHelpers.isImageFileName(entry.name);
    }

    private getConfiguredAssetUrl(
        property: 'assetsUrlTemplate' | 'assetPreviewUrlTemplate',
        assetUrl: string,
        context: AssetLibraryContext | null,
        values: Record<string, string> = {}
    ): string | null {
        return this.appConfig.getValue(property, this.getAssetContext(context, {
            assetUrl,
            ...values
        })) || null;
    }

    private getAssetContext(context: AssetLibraryContext | null, values: Record<string, string>): any {
        return {
            ...(context ?? {}),
            ...values
        };
    }
}
