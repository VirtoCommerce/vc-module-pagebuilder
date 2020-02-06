export interface EnvironmentSettings {
    storeBaseUrl: string;
    storePreviewPath: string;
    contentCssPath: string;
    baseUrl: string;
    tokenUrl: string;
    previewTimeout: number;
    assetsPath: string;
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
