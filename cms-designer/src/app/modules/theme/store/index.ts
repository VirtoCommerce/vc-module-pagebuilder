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
    state => state.dirty
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
    state => state.editablePreset
);

export const getCurrentThemeValuesRequested = createSelector(
    getThemeFeatureState,
    state => state.currentThemeValuesRequested
);

export const getValuesToSave = createSelector(
    getThemeFeatureState,
    getPresets,
    getEditablePreset,
    (state, presets, editablePreset) => {
        const actualPreset = state.presetUnderPreview ? presets.presets[state.presetUnderPreview] : editablePreset;
        if (AppSettings.defaultThemeName === AppSettings.themeName) {
            return {
                ...presets,
                current: actualPreset,
            };
        }
        if (state.presetUnderPreview) {
            return { current: state.presetUnderPreview };
        }
        if (presets && editablePreset) {
            const presetName = !!editablePreset.current ? editablePreset.current : presets.current;
            const defaultValues = typeof presetName === 'string' ? presets.presets[presetName] : presets.current;
            const result: any = {};
            Object.keys(defaultValues).forEach(key => {
                if (defaultValues[key] !== editablePreset[key]) {
                    result[key] = editablePreset[key];
                }
            });
            Object.keys(editablePreset).forEach(key => {
                if (typeof defaultValues[key] === 'undefined') {
                    result[key] = editablePreset[key];
                }
            });
            result.current = editablePreset.current;
            return result;
        }
        return {};
    }
);

export const getPresetsNotLoaded = createSelector(
    getThemeFeatureState,
    state => state.presetsNotLoaded
);

export const getSchemaNotLoaded = createSelector(
    getThemeFeatureState,
    state => state.schemaNotLoaded
);
