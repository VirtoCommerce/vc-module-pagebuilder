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

        return this.appConfig.getValue('assetLibraryRootFolderUrl', this.getAssetContext(context, { storeId }))
            || `/stores/${storeId}/Page Builder`;
    }

    getPublicUrl(entry: AssetLibraryEntry, context: AssetLibraryContext | null = null): string | null {
        const entryUrl = this.getEntryUrl(entry);
        return this.getPublicAssetUrl(entryUrl, context);
    }

    getPreviewUrl(entry: AssetLibraryEntry, context: AssetLibraryContext | null = null): string | null {
        const publicUrl = this.getPublicUrl(entry, context);
        if (!publicUrl) {
            return null;
        }

        return this.addPreviewTimestamp(publicUrl, entry.modifiedDate);
    }

    getPublicAssetUrl(value: unknown, context: AssetLibraryContext | null = null): string | null {
        if (typeof value !== 'string') {
            return null;
        }

        const assetUrl = value.trim();
        if (!assetUrl) {
            return null;
        }

        if (this.isAbsoluteAssetUrl(assetUrl)) {
            return assetUrl;
        }

        const normalizedAssetUrl = this.ensureLeadingSlash(assetUrl);
        const publicAssetUrl = assetLibraryHelpers.toPublicAssetUrl(assetUrl);

        return this.appConfig.getValue('assetsUrlTemplate', this.getAssetContext(context, {
            assetName: assetUrl,
            assetUrl: normalizedAssetUrl,
            publicAssetUrl
        })) || publicAssetUrl;
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

    private getAssetContext(context: AssetLibraryContext | null, values: Record<string, string>): any {
        return {
            ...(context ?? {}),
            ...values
        };
    }

    private isAbsoluteAssetUrl(value: string): boolean {
        return /^(?:[a-z][a-z\d+\-.]*:)?\/\//i.test(value) || value.startsWith('data:');
    }

    private ensureLeadingSlash(value: string): string {
        return value.startsWith('/') ? value : `/${value}`;
    }

    private addPreviewTimestamp(url: string, modifiedDate?: string): string {
        if (!modifiedDate) {
            return url;
        }

        const separator = url.includes('?') ? '&' : '?';
        return `${url}${separator}t=${encodeURIComponent(modifiedDate)}`;
    }
}
