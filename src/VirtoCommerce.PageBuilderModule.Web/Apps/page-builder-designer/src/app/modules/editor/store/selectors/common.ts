import { createSelector } from '@ngrx/store'
import { BuilderState } from '../state';

export const selectTemplateEditorFeature = (state: BuilderState) => state.templateEditor;

export const selectTemplateUIState = createSelector(
    selectTemplateEditorFeature,
    state => state?.ui
);

export const selectTemplateDataState = createSelector(
    selectTemplateEditorFeature,
    state => state?.data
);

export const selectTemplateDomainState = createSelector(
    selectTemplateEditorFeature,
    state => state?.domain
);

export const selectCurrentSectionsFilter = createSelector(
    selectTemplateUIState,
    state => state.currentSectionsFilter
);
