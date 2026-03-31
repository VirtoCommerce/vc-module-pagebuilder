import { createSelector } from '@ngrx/store'
import { BuilderState } from '../state';

export const selectThemeEditorFeature = (state: BuilderState) => state.themeEditor;

export const selectThemeUIState = createSelector(
    selectThemeEditorFeature,
    state => state?.ui
);

export const selectThemeDataState = createSelector(
    selectThemeEditorFeature,
    state => state?.data
);

export const selectThemeDomainState = createSelector(
    selectThemeEditorFeature,
    state => state?.domain
);

export const selectPresetsFilter = createSelector(
    selectThemeUIState,
    state => state.presetsFilter
);
