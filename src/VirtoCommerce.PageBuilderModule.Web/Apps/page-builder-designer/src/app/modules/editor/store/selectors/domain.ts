import { createSelector } from "@ngrx/store";

import { selectTemplateKeyParameter } from '@shared/routing';
import {
    selectTemplateDomainState,
    selectTemplateDataState
} from "./common";

export const isSchemasLoaded = createSelector(
    selectTemplateDataState,
    state => !!state.schemas
);

export const selectCurrentTemplateState = createSelector(
    selectTemplateDomainState,
    selectTemplateKeyParameter,
    (state, templateKey) => templateKey ? { ...state.states[templateKey], key: templateKey } : null
);

