import { createReducer, on } from '@ngrx/store';
import * as actions from '../actions';
import { ThemeUIState, initialState } from './state';

export const themeUIReducers = createReducer<ThemeUIState>(
    initialState,

    on(actions.presetsListMode, (state) => ({ ...state, mode: 'list' })),
    on(actions.presetsTileMode, (state) => ({ ...state, mode: 'tile' })),

    on(actions.loadSettingsData, state => ({ ...state, settingsLoading: true })),
    on(actions.loadSettingsDataSuccess, state => ({ ...state, settingsLoading: false })),
    on(actions.loadSettingsDataFail, state => ({ ...state, settingsLoading: false })),

    on(actions.loadSettingsSchema, state => ({ ...state, schemaLoading: true })),
    on(actions.loadSettingsSchemaSuccess, state => ({ ...state, schemaLoading: false })),
    on(actions.loadSettingsSchemaFail, state => ({ ...state, schemaLoading: false })),

    on(actions.saveSettings, state => ({ ...state, schemaLoading: true })),
    on(actions.saveSettingsSuccess, state => ({ ...state, schemaLoading: false })),
    on(actions.saveSettingsFail, state => ({ ...state, schemaLoading: false })),


    on(actions.applyPresetsFilter, (state, { filter }) => ({ ...state, presetsFilter: filter }))
);
