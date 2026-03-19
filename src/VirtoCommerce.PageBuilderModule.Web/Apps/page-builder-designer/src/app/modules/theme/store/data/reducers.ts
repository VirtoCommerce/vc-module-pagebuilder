import { cloneDeep } from 'lodash-es';
import { createReducer, on } from '@ngrx/store';

import * as actions from '../actions';

import { ThemeDataState, initialState } from './state';

export const themeDataReducers = createReducer<ThemeDataState>(
    initialState,

    on(actions.loadSettingsDataSuccess, (state: ThemeDataState, { settingsData }) => {
        const settings = typeof settingsData?.current === 'string' ? settingsData.presets[settingsData.current] : settingsData?.current || null;
        return {
            ...state,
            settings: settings ? cloneDeep(settings) : settings,
            sourceSettings: settingsData,
        };
    }),
    on(actions.useSettingsSchema, (state, { schema }) => ({ ...state, settingsSchema: schema })),
    on(actions.applyPreset, (state, { preset }) => ({ ...state, settings: { ...state.sourceSettings!.presets[preset] } })),

    on(actions.updateSettings, (state, { model }) => ({ ...state, settings: { ...state.settings, ...model } })),
    on(actions.revertChanges, state => {
        const settingsData = state.sourceSettings;
        const settings = typeof settingsData?.current === 'string' ? settingsData.presets[settingsData.current] : settingsData?.current || null;
        return {
            ...state,
            settings: settings ? cloneDeep(settings) : settings
        };
    }),
    on(actions.applyChanges, state => ({
        ...state,
        sourceSettings: {
            ...state.sourceSettings!,
            current: state.settings!
        }
    })),
);
