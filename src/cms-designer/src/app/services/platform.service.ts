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

    constructor(private http: HttpClient, private urls: ApiUrlsService, private windowRef: WindowRef) { }

    downloadSettingsData(): Observable<PresetsModel> {
        return this.tryDownloadFromTheme('/config/settings_data.json');
    }

    downloadSettingsSchema(): Observable<BlockSchema[]> {
        return this.tryDownloadFromTheme('/config/settings_schema.json');
    }

    uploadPreset(model: { [key: string]: ValueType }): Observable<any> {
        return this.uploadModel<{ [key: string]: ValueType }>(model, ContentType.themes, `/${AppSettings.themeName}/config`, 'settings_data.json');
    }

    uploadDraftPreset(model: { [key: string]: ValueType }): Observable<any> {
        return this.uploadModel<{ [key: string]: ValueType }>(model, ContentType.themes,
            `/${AppSettings.themeName}/config/drafts`, this.generateDraftPresetName());
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
        return combineLatest([this.loadModuleConfig(), this.moduleSettings(), this.storeSettings(), this.moduleVersion()]).pipe(
            tap(([appSettings, moduleSettings, storeSettings, version]) => {

                const win = this.windowRef.nativeWindow;
                const urlParams = new URLSearchParams(win.location.search);
        
                Object.assign(AppSettings, appSettings);

                AppSettings.storeId = urlParams.get('storeId');
                AppSettings.path = urlParams.get('path'),
                AppSettings.contentType = urlParams.get('contentType'),
                AppSettings.platformUrl = urlParams.get('platform') || this.getPlatformUrl()
                const index = AppSettings.path.lastIndexOf('/');
                AppSettings.filename = index !== -1 ? AppSettings.path.substr(index + 1) : AppSettings.path;
                AppSettings.uploadPath = index === -1 ? '' : AppSettings.path.substr(0, index);
                if (!AppSettings.platformUrl) {
                    AppSettings.platformUrl = this.windowRef.nativeWindow.location.origin;
                }

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
                    AppSettings[`${key[0].toLowerCase()}${key.substring(1)}`] = value;
                });
                if (!AppSettings.storeBaseUrl) {
                    AppSettings.storeBaseUrl = storeSettings.secureUrl || storeSettings.url;
                }
                AppSettings.themeName = this.getThemeName(storeSettings);
                AppSettings.version = version;
            })
        ).toPromise();
    }

    private loadModuleConfig(): Observable<EnvironmentSettings> {
        const url = this.urls.getLocalConfigUrl();
        return this.http.get<EnvironmentSettings>(url);
    }

    private getPlatformUrl(): string {
        const url = this.windowRef.nativeWindow.location.href;
        const result = url.substr(0, url.indexOf(AppSettings.moduleLocation));
        return result;
    }

    private tryDownloadFromTheme<T>(path: string): Observable<T> {
        if (AppSettings.themeName === AppSettings.defaultThemeName) {
            return this.downloadModel<T>(ContentType.themes, `/${AppSettings.themeName}${path}`);
        }
        return this.downloadModel<T>(ContentType.themes, `/${AppSettings.themeName}${path}`).pipe(
            catchError(() => {
                return this.downloadModel<T>(ContentType.themes, `/${AppSettings.defaultThemeName}${path}`)
            })
        );
    }

    private getThemeName(storeSettings: any): string {
        let result = AppSettings.defaultThemeName;

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
