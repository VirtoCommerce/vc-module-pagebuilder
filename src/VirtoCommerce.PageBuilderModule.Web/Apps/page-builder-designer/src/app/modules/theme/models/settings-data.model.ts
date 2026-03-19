import { SettingsModel } from './settings.model';
import { PresetsModel } from './presets.model';

export type SettingsDataModel = {
    current: string | SettingsModel,
    presets: PresetsModel
};
