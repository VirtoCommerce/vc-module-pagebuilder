import { Injectable } from '@angular/core';
import { CookieService } from 'ngx-cookie-service';

import { PageDescriptor } from '@shared/models';
import { AppSettings } from './app.settings';
import { WindowRef } from './window-ref';
import { environment } from 'src/environments/environment';

@Injectable({
    providedIn: 'root'
})
export class ApiUrlsService {

    private readonly SESSION_ID = 'sessionId';
    private _params: PageDescriptor = null;

    constructor(private cookies: CookieService, private windowRef: WindowRef) { }

    generateSettingsUrl() {
        const url = this.combine(this.params.platformUrl, '/api/platform/settings/modules/VirtoCommerce.PageBuilderModule');
        return url;
    }

    generateModulesUrl() {
        const url = this.combine(this.params.platformUrl, '/api/platform/modules');
        return url;
    }

    generateStoreSettingsUrl() {
        const url = this.combine(this.params.platformUrl, '/api/stores/', this.params.storeId);
        return url;
    }

    generateDownloadUrl(contentType: string, filepath: string): string {
        const path = encodeURIComponent(filepath || this.params.path);
        const url = this.combine(this.params.platformUrl, '/api/content/', contentType || this.params.contentType, this.params.storeId)
            + `?relativeUrl=${path}`;
        return url;
    }

    generateUploadAssetUrl(name: string): string {
        const assetEndpoint = `api/content/${this.params.contentType}/${this.params.storeId}`;
        const url = this.combine(this.params.platformUrl, assetEndpoint) +
                `?folderUrl=${encodeURIComponent(AppSettings.assetsPath)}&name=${name}`;
        return url;
    }

    generateUploadUrl(contentType: string = null, pathToUpload: string = null): string {
        const path = encodeURIComponent(pathToUpload || this.params.uploadPath);
        const url = this.combine(this.params.platformUrl, '/api/content/', contentType || this.params.contentType, this.params.storeId)
            + `?folderUrl=${path}`;
        return url;
    }

    getAssetsUrl(relativeUrl: string): string {
        const url = this.combine(AppSettings.storeBaseUrl, relativeUrl);
        return url;
    }

    getAssetsRelativeUrl(filename: string): string {
        if (AppSettings.assetsPath.startsWith('/assets') || AppSettings.assetsPath.startsWith('assets')) {
            return this.combine('/', AppSettings.assetsPath, filename);
        }
        return this.combine('/assets/', AppSettings.assetsPath, filename);
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
        const url = `${this.params.platformUrl}${AppSettings.tokenUrl}`;
        return url;
    }

    private generatePrefixAndSetCookie(): string {
        const characters = '0123456789ABCDEFGHIJKLMNOPQRSTUVabcdefghijklmnopqrstuv_-';
        const randomChar = () => characters[Math.floor(Math.random() * characters.length)];
        const result = Array.from({ length: 10 }, randomChar).join('');
        this.cookies.set(this.SESSION_ID, result);
        return result;
    }

    private get params(): PageDescriptor {
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

    private getPlatformUrl(): string {
        const url = this.windowRef.nativeWindow.location.href;
        const result = url.substr(0, url.indexOf(environment.moduleLocation));
        return result;
    }

    private combine(...parts: string[]): string {
        const result = parts.reduce((acc, part, index) => {
            if (part === null || part === '') {
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
