import { SettingsModel, SettingsDataModel, SettingsSchemaModel } from '@theme/models';

export interface ThemeDataState {
    settings: SettingsModel | null;
    sourceSettings: SettingsDataModel | null;
    settingsSchema: SettingsSchemaModel | null
}

export const initialState: ThemeDataState = {
    settings: null,
    sourceSettings: null,
    settingsSchema: null
}
