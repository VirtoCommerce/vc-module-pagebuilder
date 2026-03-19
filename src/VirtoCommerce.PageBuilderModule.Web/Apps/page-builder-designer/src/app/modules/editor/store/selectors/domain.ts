import { createSelector } from "@ngrx/store";

import { selectTemplateKeyParameter } from '@shared/routing';
import {
    selectTemplateEditorFeature,
    selectTemplateDomainState,
    selectTemplateDataState
} from "./common";
import * as fromData from "./data";
import * as fromShared from "@shared/store/selectors";

import { SectionStatesList, SectionState } from '@editor/models';

export const isSchemasLoaded = createSelector(
    selectTemplateDataState,
    state => !!state.schemas
);

export const selectCurrentTemplateState = createSelector(
    selectTemplateDomainState,
    selectTemplateKeyParameter,
    (state, templateKey) => templateKey ? { ...state.states[templateKey], key: templateKey } : null
);

// export const selectChangedTemplates = createSelector(
//     fromShared.selectTemplatesEntriesWithState,
//     fromShared.selectChangedTemplates
// );
