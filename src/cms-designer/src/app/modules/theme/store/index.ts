import { createFeatureSelector, createSelector } from '@ngrx/store';
import * as fromRoot from 'src/app/store';
import * as fromTheme from './theme.reducer';

export interface State extends fromRoot.State {
    theme: fromTheme.ThemeState;
}

const getThemeFeatureState = createFeatureSelector<fromTheme.ThemeState>('theme');

export const getDraftUploaded = createSelector(
    getThemeFeatureState,
    state => state.draftUploaded
);

export const getIsDirty = createSelector(
    getThemeFeatureState,
    state => state.dirty
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

export const getEditableTheme = createSelector(
    getThemeFeatureState,
    state => state.editableTheme
);

export const getCurrentThemeValuesRequested = createSelector(
    getThemeFeatureState,
    state => state.currentThemeValuesRequested
);

export const getEffectiveValues = createSelector(
    getThemeFeatureState,
    getPresets,
    getEditableTheme,
    (state, presets, editableTheme) => {
        if (presets && editableTheme) {
            const defaultValues = typeof presets.current === 'string' ? presets.presets[presets.current] : presets.current;
            const result = {};
            Object.keys(defaultValues).forEach(key => {
                if (defaultValues[key] !== editableTheme[key]) {
                    result[key] = editableTheme[key];
                }
            });
            Object.keys(editableTheme).forEach(key => {
                if (typeof defaultValues[key] === 'undefined') {
                    result[key] = editableTheme[key];
                }
            });
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
