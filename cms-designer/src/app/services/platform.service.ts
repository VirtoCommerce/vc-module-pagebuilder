import { ModuleSettings } from './../models/environment.settings';
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, combineLatest } from 'rxjs';
import { tap, map } from 'rxjs/operators';
import { ApiUrlsService } from './api-url.service';
import { PresetsModel } from '@themes/models';
import { BlockValuesModel, BlocksSchema, BlockSchema, ValueType } from '@shared/models';
import { PlatformSetting, StoreSettings } from '@app/models';

import { AppSettings } from './app.settings';
import { environment } from 'src/environments/environment';

enum ContentType {
    themes = 'themes'
}

@Injectable({
    providedIn: 'root'
})
export class PlatformService {

    constructor(private http: HttpClient, private urls: ApiUrlsService) { }

    downloadSettingsData(): Observable<PresetsModel> {
        return this.downloadModel<PresetsModel>(ContentType.themes, `/${AppSettings.defaultThemeName}/config/settings_data.json`);
    }

    downloadSettingsSchema(): Observable<BlockSchema[]> {
        return this.downloadModel<BlockSchema[]>(ContentType.themes, `/${AppSettings.defaultThemeName}/config/settings_schema.json`);
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

    donwloadBlocksSchema(): Observable<BlocksSchema> {
        return this.downloadModel<BlocksSchema>(ContentType.themes, `/${AppSettings.defaultThemeName}/config/blocks_schema.json`);
    }

    initSettings(): Promise<any> {
        const parameters = {};
        parameters['StorePreviewPath'] = 'storePreviewPath';
        parameters['TokenUrl'] = 'tokenUrl';
        parameters['AssetsPath'] = 'assetsPath';
        return combineLatest([this.moduleSettings(), this.storeSettings(), this.moduleVersion()]).pipe(
            tap(([moduleSettings, storeSettings, version]) => {
                moduleSettings.forEach(x => {
                    const key = x.name.replace('VirtoCommerce.PageBuilderModule.General.', '');
                    AppSettings[parameters[key]] = x.value || x.defaultValue;
                });
                // AppSettings.storeBaseUrl = storeSettings.secureUrl || storeSettings.url;
                AppSettings.themeName = this.getThemeName(storeSettings);
                environment.version = version;
            })
        ).toPromise();
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
