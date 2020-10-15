import { WindowRef } from './window-ref';
import { ModuleSettings } from './../models/environment.settings';
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, combineLatest, of } from 'rxjs';
import { tap, map, catchError } from 'rxjs/operators';
import { ApiUrlsService } from './api-url.service';
import { PresetsModel } from '@themes/models';
import { BlockValuesModel, BlocksSchema, BlockSchema, ValueType } from '@shared/models';
import { PlatformSetting, StoreSettings, EnvironmentSettings } from '@app/models';

import { AppSettings } from './app.settings';

enum ContentType {
    themes = 'themes'
}

@Injectable({
    providedIn: 'root'
})
export class PlatformService {

    constructor(private http: HttpClient, private urls: ApiUrlsService, private windowRef: WindowRef, private appSettings: AppSettings) { }

    downloadSettingsData(): Observable<PresetsModel> {
        return this.tryDownloadFromTheme('/config/settings_data.json');
    }

    downloadSettingsSchema(): Observable<BlockSchema[]> {
        return this.tryDownloadFromTheme('/config/settings_schema.json');
    }

    uploadPreset(model: { [key: string]: ValueType }): Observable<any> {
        return this.uploadModel<{ [key: string]: ValueType }>(model, ContentType.themes, `/${this.appSettings.themeName}/config`, 'settings_data.json');
    }

    uploadDraftPreset(model: { [key: string]: ValueType }): Observable<any> {
        return this.uploadModel<{ [key: string]: ValueType }>(model, ContentType.themes,
            `/${this.appSettings.themeName}/config/drafts`, this.generateDraftPresetName());
    }

    downloadPage(): Observable<BlockValuesModel[]> {
        return this.downloadModel<BlockValuesModel[]>();
    }

    uploadPage(model: BlockValuesModel[]): Observable<any> {
        return this.uploadModel<BlockValuesModel[]>(model);
    }

    downloadBlocksSchema(): Observable<BlocksSchema> {
        return this.tryDownloadFromTheme('/config/blocks_schema.json');
    }

    loadData<T>(relativeUrl: string, params: any = null, method: string = 'get'): Observable<T> {
        const url = this.urls.generateFullPlatformUrl(relativeUrl);
        return this.http[method.toLowerCase()](url, params);
    }

    initSettings(): Promise<any> {
        const win = this.windowRef.nativeWindow;
        const urlParams = new URLSearchParams(win.location.search);
        this.appSettings.storeId = urlParams.get('storeId');
        this.appSettings.path = urlParams.get('path');
        this.appSettings.contentType = urlParams.get('contentType');
        this.appSettings.platformUrl = urlParams.get('platform') || this.getPlatformUrl();
        if (!!this.appSettings.path) {
            const index = this.appSettings.path.lastIndexOf('/');
            this.appSettings.filename = index !== -1 ? this.appSettings.path.substr(index + 1) : this.appSettings.path;
            this.appSettings.uploadPath = index === -1 ? '' : this.appSettings.path.substr(0, index);
        }

        return combineLatest([this.loadModuleConfig(), this.moduleSettings(), this.storeSettings(), this.moduleVersion()]).pipe(
            tap(([appSettings, moduleSettings, storeSettings, version]) => {
                Object.assign(this.appSettings, appSettings);
                moduleSettings.forEach(x => {
                    const key = x.name.replace('VirtoCommerce.PageBuilderModule.General.', '');
                    let value: any = x.value || x.defaultValue;
                    switch (x.valueType) {
                        case 'Boolean':
                            value = value ? value.toString().toLowerCase() == 'true' : false;
                            break;
                        case 'Integer':
                            value = value ? parseInt(value.toString()) : 0;
                            break;
                    }
                    this.appSettings[`${key[0].toLowerCase()}${key.substring(1)}`] = value;
                });

                if (!this.appSettings.platformUrl) {
                    this.appSettings.platformUrl = this.windowRef.nativeWindow.location.origin;
                }

                if (!this.appSettings.storeBaseUrl) {
                    this.appSettings.storeBaseUrl = storeSettings.secureUrl || storeSettings.url;
                }
                this.appSettings.themeName = this.getThemeName(storeSettings);
                this.appSettings.version = version;
            })
        ).toPromise();
    }

    private loadModuleConfig(): Observable<Partial<EnvironmentSettings>> {
        // const url = this.urls.getLocalConfigUrl();
        // return this.http.get<EnvironmentSettings>(url);
        return of({
            defaultThemeName: "default",
            previewTimeout: 12000,
            useGlobalAssets: true,
            storeBaseUrl: null,
            contentCssPath: "/themes/assets/style.min.css",
            baseUrl: "/",
            moduleLocation: "/Modules/$(VirtoCommerce.PageBuilderModule)/Content/builder/"
        });
    }

    private getPlatformUrl(): string {
        const url = this.windowRef.nativeWindow.location.href;
        const result = url.substr(0, url.indexOf(this.appSettings.moduleLocation));
        return result;
    }

    private tryDownloadFromTheme<T>(path: string): Observable<T> {
        if (this.appSettings.themeName === this.appSettings.defaultThemeName) {
            return this.downloadModel<T>(ContentType.themes, `/${this.appSettings.themeName}${path}`);
        }
        return this.downloadModel<T>(ContentType.themes, `/${this.appSettings.themeName}${path}`).pipe(
            catchError(() => {
                return this.downloadModel<T>(ContentType.themes, `/${this.appSettings.defaultThemeName}${path}`)
            })
        );
    }

    private getThemeName(storeSettings: any): string {
        let result = this.appSettings.defaultThemeName;

        if (!!storeSettings && !!storeSettings.dynamicProperties) {
            const properties: Array<any> = storeSettings.dynamicProperties;
            const property = properties.find(x => x.name === 'DefaultThemeName');
            if (!!property && property.values.length > 0 && !!property.values[0].value) {
                result = property.values[0].value;
            }
        }

        return result;
    }

    private moduleVersion(): Observable<string> {
        const url = this.urls.generateModulesUrl();
        return this.http.get<ModuleSettings[]>(url).pipe(
            map(x => x.find(m => m.id === 'VirtoCommerce.PageBuilderModule')),
            map(x => x.version)
        );
    }

    private moduleSettings(): Observable<PlatformSetting[]> {
        const url = this.urls.generateSettingsUrl();
        return this.http.get<PlatformSetting[]>(url);
    }

    private storeSettings(): Observable<StoreSettings> {
        const url = this.urls.generateStoreSettingsUrl();
        return this.http.get<StoreSettings>(url);
    }

    private downloadModel<T>(contentType: string = null, filepath: string = null): Observable<T> {
        const url = this.urls.generateDownloadUrl(contentType, filepath);
        return this.download<T>(url);
    }

    private download<T>(url: string): Observable<T> {
        return this.http.get<T>(url);
    }

    private uploadModel<T>(model: T, contentType: string = null, pathToUpload: string = null, filename: string = null): Observable<any> {
        const url = this.urls.generateUploadUrl(contentType, pathToUpload);
        const form = new FormData();
        form.append(this.urls.chooseFilename(filename), JSON.stringify(model, null, 4));
        return this.http.post(url, form);
    }

    private generateDraftPresetName(): string {
        const prefix = this.urls.getCurrentSessionId();
        return `${prefix}_settings_data.json`;
    }

}
