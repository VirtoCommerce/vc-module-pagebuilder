import { createReducer, on, Action } from '@ngrx/store';

import * as actions from './actions';

import { SharedState, initialState } from './state';

export * from './state';

export const sharedReducers = createReducer<SharedState>(
    initialState,

    on(actions.loadTemplateEntries, state => ({ ...state, templatesEntriesLoading: true, templatesEntriesLoaded: false })),
    on(actions.useTemplateEntries, (state, { templatesEntries }) => ({ ...state, templatesEntriesLoading: false, templatesEntriesLoaded: true, templatesEntries })),
    on(actions.loadTemplateEntriesFails, state => ({ ...state, templatesEntriesLoading: false, templatesEntriesLoaded: false })),
    on(actions.initShared, state => ({ ...state, appInitialized: true })),

    on(actions.filterTemplates, (state, { filter }) => ({ ...state, templatesFilter: filter })),
    on(actions.displayRootTemplates, (state) => ({ ...state, templateSelected: null, templatesFilter: null })),
    on(actions.loadChildrenTemplates, (state, { templateKey }) => ({
        ...state,
        templateSelected: templateKey,
        childrenTemplatesState: {
            ...state.childrenTemplatesState,
            [templateKey]: {
                ...state.childrenTemplatesState[templateKey],
                isLoading: true
            }
        }
    })),
    on(actions.switchToChildrenTemplates, (state) => ({
        ...state,
        templatesFilter: null
    })),
    on(actions.loadChildrenTemplatesSuccess, (state, { parentTemplate, childrenEntries }) => ({
        ...state,
        childrenTemplatesState: {
            ...state.childrenTemplatesState,
            [parentTemplate]: {
                ...state.childrenTemplatesState[parentTemplate],
                isLoading: false,
                templates: childrenEntries,
                error: null
            }
        }
    })),
    on(actions.loadChildrenTemplatesFails, (state, { error, parentTemplate }) => ({
        ...state,
        childrenTemplatesState: {
            ...state.childrenTemplatesState,
            [parentTemplate]: {
                ...state.childrenTemplatesState[parentTemplate],
                isLoading: false,
                error
            }
        }
    })),
    on(actions.setRootDirtyState, (state, { templateKey, dirty }) => ({
        ...state,
        entriesStates: {
            ...state.entriesStates,
            [templateKey]: {
                ...state.entriesStates?.[templateKey],
                isDirty: dirty
            }
        }
    })),
    on(actions.setDirtyState, (state, { templateKey, parentKey, dirty }) => ({
        ...state,
        childrenTemplatesState: {
            ...state.childrenTemplatesState,
            [parentKey!]: {
                ...state.childrenTemplatesState[parentKey!],
                states: {
                    ...state.childrenTemplatesState[parentKey!]?.states || {},
                    [templateKey]: {
                        ...state.childrenTemplatesState[parentKey!]?.states?.[templateKey],
                        isDirty: dirty
                    }
                }
            }
        }
    })),

);

