import { createFeatureSelector, createSelector } from '@ngrx/store';
import * as fromRoot from 'src/app/store';
import * as fromTheme from './theme.reducer';
import { AppSettings } from '@app/services';

export interface State extends fromRoot.State {
    theme: fromTheme.PresetsState;
}

const getThemeFeatureState = createFeatureSelector<fromTheme.PresetsState>('theme');

export const getDraftUploaded = createSelector(
    getThemeFeatureState,
    state => state.draftUploaded
);

export const getIsDirty = createSelector(
    getThemeFeatureState,
    state => !state.schemaNotLoaded && state.dirty
);

export const getIsEffectiveValuesSkipped = createSelector(
    getThemeFeatureState,
    state => state.effectiveValuesSkipped
);

export const getPresetChanged = createSelector(
    getThemeFeatureState,
    state => state.presetChanged
);

export const getPresetUnderPreview = createSelector(
    getThemeFeatureState,
    state => state.presetUnderPreview
);

export const getCurrentThemeSchemaItem = createSelector(
    getThemeFeatureState,
    state => state.selectedSchemaItem
);

export const getShowPresetsEditor = createSelector(
    getThemeFeatureState,
    state => state.showPresetsEditor
);

export const getSchemaLoading = createSelector(
    getThemeFeatureState,
    state => state.schemaLoading
);

export const getPresetsLoading = createSelector(
    getThemeFeatureState,
    state => state.presetsLoading
);

export const getPresets = createSelector(
    getThemeFeatureState,
    state => state.presets
);

export const getSchema = createSelector(
    getThemeFeatureState,
    state => state.schema
);

export const getEditablePreset = createSelector(
    getThemeFeatureState,
    getCurrentThemeSchemaItem,
    (state, schema) => {
        const result = { ...state.editablePreset };
        if (schema) {
            schema.settings.forEach(x => {
                if (!result[x.id] && !!x.default) {
                    result[x.id] = x.default;
                }
            });
        }
        return result;
    }
);

export const getBasePresets = createSelector(
    getThemeFeatureState,
    state => state.basePresets
);

// export const getCurrentThemeValuesRequested = createSelector(
//     getThemeFeatureState,
//     state => state.currentThemeValuesRequested
// );

// export const getValuesToSave = createSelector(
//     getThemeFeatureState,
//     (state, presets, editablePreset, schema) => {

// // error!;

//         let result: any = {}
//         const actualPreset = state.presetUnderPreview ? presets.presets[state.presetUnderPreview] : editablePreset;
//         if (AppSettings.defaultThemeName === AppSettings.themeName) {
//             result = {
//                 ...presets,
//                 current: { ...actualPreset },
//             };
//         }
//         else if (state.presetUnderPreview) {
//             result = { current: state.presetUnderPreview };
//         }
//         else if (presets && editablePreset) {
//             const presetName = !!editablePreset.current ? editablePreset.current : presets.current;
//             const defaultValues = typeof presetName === 'string' ? presets.presets[presetName] : presets.current;
//             Object.keys(defaultValues).forEach(key => {
//                 if (defaultValues[key] !== editablePreset[key]) {
//                     result[key] = editablePreset[key];
//                 }
//             });
//             Object.keys(editablePreset).forEach(key => {
//                 if (typeof defaultValues[key] === 'undefined') {
//                     result[key] = editablePreset[key];
//                 }
//             });
//             result.current = editablePreset.current;
//         }

//         if (schema) {
//             schema.forEach(s => s.settings.forEach(x => {
//                 if (typeof result.current[x.id] !== 'undefined' && result.current[x.id] === x.default) {
//                     delete result.current[x.id];
//                 }
//             }));
//         }

//         return result;
//     }
// );

export const getPresetsNotLoaded = createSelector(
    getThemeFeatureState,
    state => state.presetsNotLoaded
);

export const getSchemaNotLoaded = createSelector(
    getThemeFeatureState,
    state => state.schemaNotLoaded
);
