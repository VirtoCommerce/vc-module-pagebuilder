import { createReducer, on, Action } from '@ngrx/store';

import { PresetsModel } from '@themes/models';
import { BlockSchema, ValueType } from '@shared/models';

import * as Actions from './theme.actions';

export interface PresetsState {
    schemaLoading: boolean;
    schemaNotLoaded: boolean;
    presetsLoading: boolean;
    presetsNotLoaded: boolean;
    draftUploaded: boolean;
    uploadDraftFail: boolean;

    presetUnderPreview: string;
    effectiveValuesSkipped: boolean;
    showPresetsEditor: boolean;
    presetChanged: boolean;
    selectedSchemaItem: BlockSchema; // this section corresponds to section from schema
    editablePreset: { [key: string]: ValueType }; // the preset under editing
    initialValues: { [key: string]: ValueType }; // initial values of current preset
    presets: PresetsModel; // the whole presets file which used as transport for preview
    basePresets: PresetsModel; // base settings
    schema: BlockSchema[]; // the settings schema
    dirty: boolean;
}

export const initialState: PresetsState = {
    schemaLoading: false,
    schemaNotLoaded: false,
    presetsLoading: false,
    presetsNotLoaded: false,
    draftUploaded: false,
    uploadDraftFail: false,

    presetUnderPreview: null,
    effectiveValuesSkipped: false,
    showPresetsEditor: false,
    presetChanged: false,
    selectedSchemaItem: null,
    editablePreset: null,
    initialValues: {},
    presets: null,
    basePresets: null,
    schema: null,
    dirty: false
};

const themesReducer = createReducer(
    initialState,
    on(Actions.loadSchema, state => ({ ...state, schemaLoading: true })),
    on(Actions.loadSchemaSuccess, (state, { schema }) => ({ ...state, schema, schemaLoading: false, schemaNotLoaded: false })),
    on(Actions.loadSchemaFail, state => ({ ...state, schemaLoading: false, schemaNotLoaded: true, schema: null })),
    on(Actions.saveThemeSuccess, (state, { values }) => ({ ...state, initialValues: values, dirty: false })),
    on(Actions.loadPresets, state => ({ ...state, presetsLoading: true })),
    on(Actions.loadPresetsSuccess, (state, themes) => {
        const currentPreset = <any>themes.presets.current;
        return {
            ...state,
            editablePreset: { ...currentPreset },
            initialValues: { ...currentPreset },
            presets: themes.presets,
            basePresets: themes.basePresets,
            presetsNotLoaded: false,
            presetsLoading: false
        };
    }),
    on(Actions.loadPresetsFail, state => ({ ...state, presetsLoading: false, presetsNotLoaded: true })),
    on(Actions.selectSchemaItem, (state, { item }) => ({ ...state, selectedSchemaItem: item })),
    on(Actions.showPresetsPane, state => ({ ...state, showPresetsEditor: true })),
    on(Actions.closeEditors, Actions.cancelPreset, state => ({
        ...state,
        showPresetsEditor: false,
        selectedSchemaItem: null,
        presetUnderPreview: null
    })),
    on(Actions.cancelPresetComplete, state => ({ ...state, presetChanged: false })),
    on(Actions.previewPreset, (state, { preset }) => ({ ...state, presetUnderPreview: preset, presetChanged: true })),
    on(Actions.applyPreset, (state, { preset }) => {
        const newPreset = { current: preset, ...state.presets.presets[preset] };
        return {
            ...state,
            editablePreset: newPreset,
            showPresetsEditor: false,
            presetChanged: false,
            dirty: true
        };
    }),
    on(Actions.updateTheme, (state, { values }) => {
        return {
            ...state,
            editablePreset: { ...state.editablePreset, ...values },
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
    //         editablePreset: { ...newPresets.current },
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
    //     newPresets.presets[preset] = { ...state.editablePreset };
    //     newPresets.current = { ...state.editablePreset };
    //     return {
    //         ...state,
    //         presets: newPresets,
    //         dirty: true
    //     };
    // }),
    on(Actions.updateDraftSuccess, state => ({ ...state, draftUploaded: true }))
);

export function reducer(state = initialState, action: Action): PresetsState {
    return themesReducer(state, action);
}
