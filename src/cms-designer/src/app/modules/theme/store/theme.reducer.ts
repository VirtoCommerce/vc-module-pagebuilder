import { createReducer, on, Action } from '@ngrx/store';

import { PresetsModel } from '@themes/models';
import { BlockSchema, ValueType } from '@shared/models';

import * as Actions from './theme.actions';

export interface ThemeState {
    schemaLoading: boolean;
    schemaNotLoaded: boolean;
    presetsLoading: boolean;
    presetsNotLoaded: boolean;
    currentThemeValuesRequested: boolean;
    draftUploaded: boolean;
    uploadDraftFail: boolean;

    showPresetsEditor: boolean;
    selectedSchemaItem: BlockSchema; // this section corresponds to section from schema
    editableTheme: { [key: string]: ValueType }; // the current theme
    initialValues: { [key: string]: ValueType }; // the effective theme values
    presets: PresetsModel; // the whole presets file which used as transport for preview
    schema: BlockSchema[]; // the settings schema
    dirty: boolean;
}

export const initialState: ThemeState = {
    schemaLoading: false,
    schemaNotLoaded: false,
    presetsLoading: false,
    presetsNotLoaded: false,
    currentThemeValuesRequested: false,
    draftUploaded: false,
    uploadDraftFail: false,

    showPresetsEditor: false,
    selectedSchemaItem: null,
    editableTheme: null,
    initialValues: {},
    presets: null,
    schema: null,
    dirty: false
};

const themesReducer = createReducer(
    initialState,
    on(Actions.loadSchema, state => ({ ...state, schemaLoading: true })),
    on(Actions.loadSchemaSuccess, (state, { schema }) => ({ ...state, schema, schemaLoading: false, schemaNotLoaded: false })),
    on(Actions.loadSchemaFail, state => ({ ...state, schemaLoading: false, schemaNotLoaded: true, schema: null })),
    on(Actions.saveThemeSuccess, (state, { values }) => ({ ...state, initialValues: values, dirty: false })),
    on(Actions.loadDefaultThemes, state => ({ ...state, presetsLoading: true })),
    on(Actions.loadDefaultThemesSuccess, (state, { presets }) => {
        return {
            ...state,
            editableTheme: typeof presets.current === 'string' ? { ...presets.presets[presets.current] } : { ...presets.current },
            presets: presets
        };
    }),
    on(Actions.loadEffectiveThemeValuesRequested, state => ({ ...state, currentThemeValuesRequested: true })),
    on(Actions.loadEffectiveThemeValuesSuccess, (state, { values }) => {
        const currentValues = { ...state.editableTheme, ...values };
        return {
            ...state,
            editableTheme: currentValues,
            initialValues: values,
            presetsLoading: false,
            presetsNotLoaded: false
        };
    }),
    on(Actions.loadDefaultThemesFail, state => ({ ...state, presetsLoading: false, presetsNotLoaded: true })),
    on(Actions.selectSchemaItem, (state, { item }) => ({ ...state, selectedSchemaItem: item })),
    on(Actions.showPresetsPane, state => ({ ...state, showPresetsEditor: true })),
    on(Actions.closeEditors, Actions.cancelPreset, state => ({
        ...state,
        showPresetsEditor: false,
        selectedSchemaItem: null
    })),
    // on(Actions.applyPreset, (state, { preset }) => {
    //     const newTheme = { ...state.presets.presets[preset] };
    //     return {
    //         ...state,
    //         editableTheme: newTheme,
    //         presets: {
    //             ...state.presets,
    //             current: newTheme
    //         },
    //         showPresetsEditor: false,
    //         dirty: true
    //     };
    // }),
    on(Actions.updateTheme, (state, { values }) => {
        return {
            ...state,
            editableTheme: { ...state.editableTheme, ...values },
            dirty: true
        };
    }),
    // on(Actions.clearThemeChanges, state => {
    //     const newPresets = JSON.parse(state.initialPresets);
    //     if (typeof newPresets.current === 'string') {
    //         newPresets.current = { ...newPresets.presets[newPresets.current] };
    //     }
    //     return {
    //         ...state,
    //         presets: newPresets,
    //         editableTheme: { ...newPresets.current },
    //         dirty: false,
    //         draftUploaded: false
    //     };
    // }),
    // on(Actions.removePreset, (state, { preset }) => {
    //     const newPresets = { ...state.presets };
    //     delete newPresets.presets[preset];
    //     return {
    //         ...state,
    //         presets: newPresets,
    //         dirty: true
    //     };
    // }),
    // on(Actions.createPreset, (state, { preset }) => {
    //     const newPresets = { ...state.presets };
    //     newPresets.presets[preset] = { ...state.editableTheme };
    //     newPresets.current = { ...state.editableTheme };
    //     return {
    //         ...state,
    //         presets: newPresets,
    //         dirty: true
    //     };
    // }),
    on(Actions.selectPreset, (state, { preset }) => ({ ...state, editableTheme: { ...state.presets[preset] } })),
    on(Actions.updateDraftSuccess, state => ({ ...state, draftUploaded: true }))
);

export function reducer(state = initialState, action: Action): ThemeState {
    return themesReducer(state, action);
}
