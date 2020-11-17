export interface EnvironmentSettings {
    storeBaseUrl: string;
    storePreviewPath: string;
    defaultThemeName: string;
    themeName: string;
    contentCssPath: string;
    baseUrl: string;
    tokenUrl: string;
    previewTimeout: number;
    useGlobalAssets: boolean;
    assetsPath: string;

    moduleLocation: string;
    version: string;

    storeId: string;
    path: string;
    contentType: string;
    filename?: string;
    uploadPath?: string;
    platformUrl?: string;
}

export interface PlatformSetting {
    groupName: string;
    name: string;
    value: string;
    valueType: string;
    defaultValue: string;
    isArray: boolean;
    title: string;
    description: string;
}

export interface StoreSettings {
    url: string;
    secureUrl: string;
}

export interface ModuleSettings {
    id: string;
    version: string;
}
