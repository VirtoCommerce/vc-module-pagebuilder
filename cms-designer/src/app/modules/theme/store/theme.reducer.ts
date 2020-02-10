import { createReducer, on, Action } from '@ngrx/store';

import { PresetsModel } from '@themes/models';
import { BlockSchema, ValueType } from '@shared/models';

import * as Actions from './theme.actions';

export interface ThemeState {
    schemaLoading: boolean;
    schemaNotLoaded: boolean;
    presetsLoading: boolean;
    presetsNotLoaded: boolean;
    draftUploaded: boolean;
    uploadDraftFail: boolean;

    showPresetsEditor: boolean;
    selectedSchemaItem: BlockSchema; // this section corresponds to section from schema
    editableTheme: { [key: string]: ValueType }; // the current theme
    presets: PresetsModel; // the whole presets file which used as transport for preview
    initialPresets: string; // initial file with presets and theme as string
    schema: BlockSchema[]; // the settings schema
    dirty: boolean;
}

export const initialState: ThemeState = {
    schemaLoading: false,
    schemaNotLoaded: false,
    presetsLoading: false,
    presetsNotLoaded: false,
    draftUploaded: false,
    uploadDraftFail: false,

    showPresetsEditor: false,
    selectedSchemaItem: null,
    editableTheme: null,
    presets: null,
    initialPresets: null,
    schema: null,
    dirty: false
};

const themesReducer = createReducer(
    initialState,
    on(Actions.loadSchema, state => ({ ...state, schemaLoading: true })),
    on(Actions.loadSchemaSuccess, (state, { schema }) => ({ ...state, schema, schemaLoading: false, schemaNotLoaded: false })),
    on(Actions.loadSchemaFail, (state) => ({ ...state, schemaLoading: false, schemaNotLoaded: true, schema: null })),
    on(Actions.saveTheme, state => ({ ...state, presets: { ...state.presets, current: { ...state.editableTheme } } })),
    on(Actions.saveThemeSuccess, state => ({ ...state, initialPresets: JSON.stringify(state.presets), dirty: false })),
    on(Actions.loadThemes, state => ({ ...state, presetsLoading: true })),
    on(Actions.loadThemesSuccess, (state, { presets }) => {
        const newPresets = { ...presets };
        if (typeof presets.current === 'string') {
            newPresets.current = { ...presets.presets[presets.current] };
        }
        return {
            ...state,
            editableTheme: { ...<any>newPresets.current },
            initialPresets: JSON.stringify(presets),
            presets: newPresets,
            presetsLoading: false,
            presetsNotLoaded: false
        };
    }),
    on(Actions.loadThemesFail, state => ({ ...state, presetsLoading: false, presetsNotLoaded: true })),
    on(Actions.selectSchemaItem, (state, { item }) => ({ ...state, selectedSchemaItem: item })),
    on(Actions.showPresetsPane, state => ({ ...state, showPresetsEditor: true })),
    on(Actions.closeEditors, Actions.cancelPreset, state => ({
        ...state,
        presets: {
            ...state.presets,
            current: { ...state.editableTheme }
        },
        showPresetsEditor: false,
        selectedSchemaItem: null
    })),
    on(Actions.applyPreset, (state, { preset }) => {
        const newTheme = { ...state.presets.presets[preset] };
        return {
            ...state,
            editableTheme: newTheme,
            presets: {
                ...state.presets,
                current: newTheme
            },
            showPresetsEditor: false,
            dirty: true
        };
    }),
    on(Actions.updateTheme, (state, { values }) => {
        const currentTheme = { ...state.editableTheme, ...values };
        const newPresets = { ...state.presets };
        newPresets.current = { ...currentTheme };
        return {
            ...state,
            editableTheme: currentTheme,
            presets: newPresets,
            dirty: true
        };
    }),
    on(Actions.clearThemeChanges, state => {
        const newPresets = JSON.parse(state.initialPresets);
        if (typeof newPresets.current === 'string') {
            newPresets.current = { ...newPresets.presets[newPresets.current] };
        }
        return {
            ...state,
            presets: newPresets,
            editableTheme: { ...newPresets.current },
            dirty: false,
            draftUploaded: false
        };
    }),
    on(Actions.removePreset, (state, { preset }) => {
        const newPresets = { ...state.presets };
        delete newPresets.presets[preset];
        return {
            ...state,
            presets: newPresets,
            dirty: true
        };
    }),
    on(Actions.createPreset, (state, { preset }) => {
        const newPresets = { ...state.presets };
        newPresets.presets[preset] = { ...state.editableTheme };
        newPresets.current = { ...state.editableTheme };
        return {
            ...state,
            presets: newPresets,
            dirty: true
        };
    }),
    on(Actions.selectPreset, (state, { preset }) => {
        const newPresets = { ...state.presets };
        newPresets.current = { ...newPresets.presets[preset] };
        return {
            ...state,
            presets: newPresets
        };
    }),
    on(Actions.updateDraftSuccess, state => ({ ...state, draftUploaded: true }))
);

export function reducer(state = initialState, action: Action): ThemeState {
    return themesReducer(state, action);
}
