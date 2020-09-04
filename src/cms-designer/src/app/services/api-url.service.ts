import { Injectable } from '@angular/core';
import { CookieService } from 'ngx-cookie-service';

import { PageBuilderContext } from '@shared/models';
import { AppSettings } from './app.settings';
import { WindowRef } from './window-ref';
import { environment } from 'src/environments/environment';
import { generateUniqueString } from './utils';

@Injectable({
    providedIn: 'root'
})
export class ApiUrlsService {

    private readonly SESSION_ID = 'sessionId';
    private _params: PageBuilderContext = null;

    constructor(private cookies: CookieService, private windowRef: WindowRef) { }

    generateSettingsUrl(): string {
        const url = this.combine(this.params.platformUrl, '/api/platform/settings/modules/VirtoCommerce.PageBuilderModule');
        return url;
    }

    generateModulesUrl(): string {
        const url = this.combine(this.params.platformUrl, '/api/platform/modules');
        return url;
    }

    generateStoreSettingsUrl(): string {
        const url = this.combine(this.params.platformUrl, '/api/stores/', this.params.storeId);
        return url;
    }

    generateDownloadUrl(contentType: string, filepath: string): string {
        const path = encodeURIComponent(filepath || this.params.path);
        const url = this.combine(this.params.platformUrl, '/api/content/', contentType || this.params.contentType, this.params.storeId)
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
            : `api/content/Pages/${this.params.storeId}`; // url to pages storage, upload via content module endpoint
        const url = this.combine(this.params.platformUrl, assetEndpoint) +
                `?folderUrl=/assets/${encodeURIComponent(this.params.contentType)}&name=${name}`;
        return url;
    }

    generateUploadUrl(contentType: string = null, pathToUpload: string = null): string {
        const path = encodeURIComponent(pathToUpload || this.params.uploadPath);
        const url = this.combine(this.params.platformUrl, '/api/content/', contentType || this.params.contentType, this.params.storeId)
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
        return this.combine('/assets/', this.params.contentType, filename);
    }

    getStoreUrl(layout: string): string {
        // preview_mode used for key in preview theme
        // layout for choose base layout
        // ep is endpoint for platform
        const query = `?preview_mode=${this.getCurrentSessionId()}${!!layout ? '&layout=' + layout : ''}&ep=${this.params.platformUrl}`;
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
        return givenFilename || this.params.filename;
    }

    getStoresEndPoint(): string {
        // /admin/api/stores/{Electronics}
        const url = this.combine(this.params.platformUrl, '/api/stores/', this.params.storeId);
        return url;
    }

    getTokenUrl(): string {
        const url = this.combine(this.params.platformUrl, AppSettings.tokenUrl);
        return url;
    }

    get params(): PageBuilderContext {
        if (!this._params) {
            const win = this.windowRef.nativeWindow;
            const urlParams = new URLSearchParams(win.location.search);

            this._params = {
                storeId: urlParams.get('storeId'),
                path: urlParams.get('path'),
                contentType: urlParams.get('contentType'),
                platformUrl: urlParams.get('platform') || this.getPlatformUrl()
            };
            const index = this._params.path.lastIndexOf('/');
            this._params.filename = index !== -1 ? this._params.path.substr(index + 1) : this._params.path;
            this._params.uploadPath = index === -1 ? '' : this._params.path.substr(0, index);
            if (!this._params.platformUrl) {
                this._params.platformUrl = this.windowRef.nativeWindow.location.origin;
            }
        }
        return this._params;
    }

    generateFullPlatformUrl(relativeUrl: string): string {
        const url = this.combine(this.params.platformUrl, relativeUrl);
        return url;
    }

    private generatePrefixAndSetCookie(): string {
        const result = generateUniqueString(10);
        this.cookies.set(this.SESSION_ID, result);
        return result;
    }

    private getPlatformUrl(): string {
        const url = this.windowRef.nativeWindow.location.href;
        const result = url.substr(0, url.indexOf(environment.moduleLocation));
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
