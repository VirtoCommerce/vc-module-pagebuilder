import { EnvironmentSettings } from '@app/models';

export let AppSettings: EnvironmentSettings = {
    storeBaseUrl: null,
    defaultThemeName: 'default',
    themeName: 'default',
    storePreviewPath: null,
    contentCssPath: null,
    baseUrl: null,
    tokenUrl: '/connect/token',
    useGlobalAssets: false,
    previewTimeout: 120000
};
