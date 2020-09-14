import { Injectable } from '@angular/core';
import { CookieService } from 'ngx-cookie-service';

import { AppSettings } from './app.settings';
import { WindowRef } from './window-ref';
import { generateUniqueString } from './utils';

@Injectable({
    providedIn: 'root'
})
export class ApiUrlsService {

    private readonly SESSION_ID = 'sessionId';

    constructor(private cookies: CookieService, private windowRef: WindowRef) { }

    generateSettingsUrl(): string {
        const url = this.combine(AppSettings.platformUrl, '/api/platform/settings/modules/VirtoCommerce.PageBuilderModule');
        return url;
    }

    generateModulesUrl(): string {
        const url = this.combine(AppSettings.platformUrl, '/api/platform/modules');
        return url;
    }

    generateStoreSettingsUrl(): string {
        const url = this.combine(AppSettings.platformUrl, '/api/stores/', AppSettings.storeId);
        return url;
    }

    generateDownloadUrl(contentType: string, filepath: string): string {
        const path = encodeURIComponent(filepath || AppSettings.path);
        const url = this.combine(AppSettings.platformUrl, '/api/content/', contentType || AppSettings.contentType, AppSettings.storeId)
            + `?relativeUrl=${path}`;
        return url;
    }

    generateUniqueSafeFileName(name: string): string {
        const parts = name.split('.');
        const extension = parts.pop();
        const uniqueName = `${parts.join('.')}_${generateUniqueString(10)}.${extension}`;
        const safeName = encodeURIComponent(uniqueName);
        return safeName;
    }

    generateUploadAssetUrl(name: string): string {
        const assetEndpoint = AppSettings.useGlobalAssets
            ? '/api/platform/assets' // url for cdn, upload via platform endpoint
            : `api/content/Pages/${AppSettings.storeId}`; // url to pages storage, upload via content module endpoint
        const url = this.combine(AppSettings.platformUrl, assetEndpoint) +
            `?folderUrl=/assets/${encodeURIComponent(AppSettings.contentType)}&name=${name}`;
        return url;
    }

    generateUploadUrl(contentType: string = null, pathToUpload: string = null): string {
        const path = encodeURIComponent(pathToUpload || AppSettings.uploadPath);
        const url = this.combine(AppSettings.platformUrl, '/api/content/', contentType || AppSettings.contentType, AppSettings.storeId)
            + `?folderUrl=${path}`;
        return url;
    }

    getAssetsUrl(absoluteOrRelativeUrl: string): string {
        if (!absoluteOrRelativeUrl) {
            return null;
        }
        const url = ['http://', 'https://', '//'].find(x => absoluteOrRelativeUrl.startsWith(x))
            ? absoluteOrRelativeUrl
            : this.combine(AppSettings.storeBaseUrl, absoluteOrRelativeUrl);
        return url;
    }

    getAssetsRelativeUrl(filename: string): string {
        return this.combine('/assets/', AppSettings.contentType, filename);
    }

    getStoreUrl(layout: string): string {
        // preview_mode used for key in preview theme
        // layout for choose base layout
        // ep is endpoint for platform
        const query = `?preview_mode=${this.getCurrentSessionId()}${!!layout ? '&layout=' + layout : ''}&ep=${AppSettings.platformUrl}`;
        const url = this.combine(AppSettings.storeBaseUrl, AppSettings.storePreviewPath) + query;
        return url;
    }

    getCurrentSessionId(): string {
        const result = this.cookies.check(this.SESSION_ID)
            ? this.cookies.get(this.SESSION_ID)
            : this.generatePrefixAndSetCookie();
        return result;
    }

    chooseFilename(givenFilename: string): string {
        return givenFilename || AppSettings.filename;
    }

    getStoresEndPoint(): string {
        // /admin/api/stores/{Electronics}
        const url = this.combine(AppSettings.platformUrl, '/api/stores/', AppSettings.storeId);
        return url;
    }

    getTokenUrl(): string {
        const url = this.combine(AppSettings.platformUrl, AppSettings.tokenUrl);
        return url;
    }

    getLocalConfigUrl(): string {
        return 'data/settings.json';
    }

    generateFullPlatformUrl(relativeUrl: string): string {
        const url = this.combine(AppSettings.platformUrl, relativeUrl);
        return url;
    }

    private generatePrefixAndSetCookie(): string {
        const result = generateUniqueString(10);
        this.cookies.set(this.SESSION_ID, result);
        return result;
    }



    private combine(...parts: string[]): string {
        const result = parts.reduce((acc, part, index) => {
            if (!part) {
                return acc;
            }
            if (index === 0) {
                return part;
            }
            if (acc.endsWith('/') && part.startsWith('/')) {
                return acc + part.substr(1);
            }
            if (!acc.endsWith('/') && !part.startsWith('/')) {
                return acc + '/' + part;
            }
            return acc + part;
        }, '');
        return result;
    }
}
