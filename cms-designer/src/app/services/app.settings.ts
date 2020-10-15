import { Injectable } from '@angular/core';
import { EnvironmentSettings } from '@app/models';

@Injectable({
    providedIn: 'root'
})
export class AppSettings implements EnvironmentSettings {
    storeBaseUrl = null;
    defaultThemeName = 'default';
    themeName = 'default';
    storePreviewPath = null;
    contentCssPath = null;
    baseUrl = null;
    tokenUrl = '/connect/token';
    useGlobalAssets = false;
    previewTimeout = 120000;

    moduleLocation = null;
    version = null;

    storeId = null;
    path = null;
    contentType = null;
    filename = null;
    uploadPath = null;
    platformUrl = null;
};
