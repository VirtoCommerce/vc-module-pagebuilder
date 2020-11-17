import { Injectable } from '@angular/core';
import { CookieService } from 'ngx-cookie-service';

import { AppSettings } from './app.settings';
import { generateUniqueString } from './utils';

@Injectable({
    providedIn: 'root'
})
export class ApiUrlsService {

    private readonly SESSION_ID = 'sessionId';

    constructor(private cookies: CookieService, private appSettings: AppSettings) { }

    generateSettingsUrl(): string {
        const url = this.combine(this.appSettings.platformUrl, '/api/platform/settings/modules/VirtoCommerce.PageBuilderModule');
        return url;
    }

    generateModulesUrl(): string {
        const url = this.combine(this.appSettings.platformUrl, '/api/platform/modules');
        return url;
    }

    generateStoreSettingsUrl(): string {
        const url = this.combine(this.appSettings.platformUrl, '/api/stores/', this.appSettings.storeId);
        return url;
    }

    getStoreUrlEndpoint(storeId: string): string {
        const url = this.combine(this.appSettings.platformUrl, 'api/stores/url/', storeId);
        return url;
    }

    generateDownloadUrl(contentType: string, filepath: string): string {
        const path = encodeURIComponent(filepath || this.appSettings.path);
        const url = this.combine(this.appSettings.platformUrl, '/api/content/', contentType || this.appSettings.contentType, this.appSettings.storeId)
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
        const assetEndpoint = this.appSettings.useGlobalAssets
            ? '/api/platform/assets' // url for cdn, upload via platform endpoint
            : `api/content/Pages/${this.appSettings.storeId}`; // url to pages storage, upload via content module endpoint
        const url = this.combine(this.appSettings.platformUrl, assetEndpoint) +
            `?folderUrl=${encodeURIComponent(this.appSettings.assetsPath)}&name=${name}`;
            // `?folderUrl=/assets/${encodeURIComponent(this.appSettings.contentType)}&name=${name}`;
        return url;
    }

    generateUploadUrl(contentType: string = null, pathToUpload: string = null): string {
        const path = encodeURIComponent(pathToUpload || this.appSettings.uploadPath);
        const url = this.combine(this.appSettings.platformUrl, '/api/content/', contentType || this.appSettings.contentType, this.appSettings.storeId)
            + `?folderUrl=${path}`;
        return url;
    }

    getAssetsUrl(absoluteOrRelativeUrl: string): string {
        if (!absoluteOrRelativeUrl) {
            return null;
        }
        const url = ['http://', 'https://', '//'].find(x => absoluteOrRelativeUrl.startsWith(x))
            ? absoluteOrRelativeUrl
            : this.combine(this.appSettings.storeBaseUrl, absoluteOrRelativeUrl);
        return url;
    }

    getAssetsRelativeUrl(filename: string): string {
        return this.combine('/assets/', this.appSettings.contentType, filename);
    }

    getStoreUrl(layout: string): string {
        // preview_mode used for key in preview theme
        // layout for choose base layout
        // ep is endpoint for platform
        const query = `?preview_mode=${this.getCurrentSessionId()}${!!layout ? '&layout=' + layout : ''}&ep=${this.appSettings.platformUrl}`;
        const url = this.combine(this.appSettings.storeBaseUrl, this.appSettings.storePreviewPath) + query;
        return url;
    }

    getCurrentSessionId(): string {
        const result = this.cookies.check(this.SESSION_ID)
            ? this.cookies.get(this.SESSION_ID)
            : this.generatePrefixAndSetCookie();
        return result;
    }

    chooseFilename(givenFilename: string): string {
        return givenFilename || this.appSettings.filename;
    }

    getStoresEndPoint(): string {
        // /admin/api/stores/{Electronics}
        const url = this.combine(this.appSettings.platformUrl, '/api/stores/', this.appSettings.storeId);
        return url;
    }

    getTokenUrl(): string {
        const url = this.combine(this.appSettings.platformUrl, this.appSettings.tokenUrl);
        return url;
    }

    getLocalConfigUrl(): string {
        return 'data/settings.html';
    }

    generateFullPlatformUrl(relativeUrl: string): string {
        const url = this.combine(this.appSettings.platformUrl, relativeUrl);
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
