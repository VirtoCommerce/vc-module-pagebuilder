import { createFeatureSelector, createSelector } from '@ngrx/store';

import * as fromRoot from './root.reducer';
import * as fromTheme from '@themes/store';
import * as fromEditor from '@editor/store';

export interface State {
    root: fromRoot.RootState;
}

const selectRootState = (state: State) => state.root;

export const getIsLoading = createSelector(
    fromEditor.getPageLoading,
    fromEditor.getSchemaLoading,
    fromTheme.getPresetsLoading,
    fromTheme.getSchemaLoading,
    (page, blocks, presets, schema) => page || blocks || schema || presets
);

export const getIsTabVisible = createSelector(
    fromEditor.getCurrentSectionItem,
    fromEditor.getAddNewSectionMode,
    fromTheme.getShowPresetsEditor,
    fromTheme.getCurrentThemeSchemaItem,
    (section, isAddMode, presets, theme) => !section && !isAddMode && !presets && !theme
);

export const getActiveTabIndex = createSelector(
    selectRootState,
    state => state.activeTabIndex
);

export const getIsEditMode = createSelector(
    fromEditor.getCurrentSectionItem,
    fromEditor.getAddNewSectionMode,
    fromTheme.getCurrentThemeSchemaItem,
    fromTheme.getShowPresetsEditor,
    (sectionItem, addSection, schemaItem, presetsEditor) =>
        !!sectionItem || !!addSection || !!schemaItem || !!presetsEditor
);

export const getPrimaryFrameId = createSelector(
    selectRootState,
    state => state.primaryFrameId
);

export const getSecondaryFrameId = createSelector(
    selectRootState,
    state => state.secondaryFrameId
);

export const getHeaderIsActive = createSelector(
    getIsEditMode,
    mode => mode
);

export const getHeaderIcon = createSelector(
    getIsEditMode,
    fromEditor.getAddNewSectionMode,
    (isEdit, isAdd) => isAdd ? 'close' : isEdit ? 'back' : 'logo'
);

export const getIsDirty = createSelector(
    fromTheme.getIsDirty,
    fromEditor.getIsDirty,
    (themeDirty, editorDirty) => themeDirty || editorDirty
);

export const getPreviewLoading = createSelector(
    selectRootState,
    state => state.previewLoading
);

export const getPreviewUrl = createSelector(
    selectRootState,
    state => state.previewUrl
);

export const getPreviewError = createSelector(
    selectRootState,
    state => state.previewError
);

export const getPrimaryIsLoaded = createSelector(
    selectRootState,
    state => state.primaryLoaded
);

export const getSecondaryIsLoaded = createSelector(
    selectRootState,
    state => state.secondaryLoaded
);
