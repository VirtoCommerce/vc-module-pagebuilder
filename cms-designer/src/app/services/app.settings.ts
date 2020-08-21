import { EnvironmentSettings } from '@app/models';

export let AppSettings: EnvironmentSettings = {
    storeBaseUrl: null,
    defaultThemeName: 'default',
    themeName: 'default',
    storePreviewPath: null,
    contentCssPath: null,
    baseUrl: null,
    tokenUrl: '/token',
    useGlobalAssets: false,
    previewTimeout: 120000
};
