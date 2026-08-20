import { createReducer, on } from '@ngrx/store';

import * as actions from '../actions';

import { PageHistoryState } from '@editor/models';

import { EditorDomainState, initialState } from './state';

export const editorDomainReducers = createReducer<EditorDomainState>(
    initialState,

    on(actions.loadTemplateSchemas, (state) => ({ ...state, schemaLoading: true })),
    on(actions.loadTemplateSchemasSuccess, (state) => ({ ...state, schemaLoading: false })),
    on(actions.loadTemplateModel, (state, { templateKey }) => ({
            ...state,
            states: {
                ...state.states,
                [templateKey]: {
                    ...state.states[templateKey],
                    isLoading: true,
                    sections: state.states[templateKey]?.sections || {}
                }
            }
        })
    ),
    on(actions.loadTemplateModelSuccess, (state, { templateKey }) => ({
            ...state,
            states: {
                ...state.states,
                [templateKey]: {
                    ...state.states[templateKey],
                    isLoading: false,
                    error: undefined,
                    sections: state.states[templateKey]?.sections || {}
                }
            }
        })
    ),
    on(actions.loadTemplateModelFails, (state, { error, templateKey }) => ({
            ...state,
            states: {
                ...state.states,
                [templateKey]: {
                    ...state.states[templateKey],
                    isLoading: false,
                    error: error.message
                }
            }
        })
    ),
    on(actions.getTemplatePublishStatusSuccess, (state, { templateKey, hasChanges, published, pending }) => ({
        ...state,
        states: {
            ...state.states,
            [templateKey]: {
                ...state.states[templateKey],
                hasChanges,
                published,
                pending: !!pending
            }
        }
    })),
    on(actions.loadPageHistory, (state, { templateKey }) => withHistory(state, templateKey, { isLoading: true, error: undefined })),
    on(actions.loadPageHistorySuccess, (state, { templateKey, history }) => withHistory(state, templateKey, { ...history, isLoading: false, error: undefined })),
    on(actions.loadPageHistoryFails, (state, { templateKey, error }) => withHistory(state, templateKey, { isLoading: false, error: error?.message })),
    // the row that was clicked shows it is working; the list itself is reloaded once the commit lands
    on(actions.restoreVersion, (state, { templateKey, sha }) => withHistory(state, templateKey, { restoring: sha })),
    on(actions.restoreVersionSuccess, (state, { templateKey }) => withHistory(state, templateKey, { restoring: undefined })),
    on(actions.restoreVersionFails, (state, { templateKey, error }) => withHistory(state, templateKey, { restoring: undefined, error: error?.message })),
    on(actions.sectionStateChangedAction, (state, { templateKey, sectionId, state: seactionState }) => ({
        ...state,
        states: {
            ...state.states,
            [templateKey]: {
                ...state.states[templateKey],
                sections: {
                    ...state.states[templateKey]?.sections,
                    [sectionId]: {
                        ...state.states[templateKey]?.sections[sectionId],
                        ...seactionState,
                        blocks: {
                            ...state.states[templateKey]?.sections[sectionId]?.blocks,
                            ...seactionState.blocks
                        }
                    }
                }
            }
        }
    })),
);

/**
 * Patches the open page's history without disturbing the rest of its state. Versions live next to publish
 * status because they answer the same question from the other side: what is live, and what is not yet.
 */
function withHistory(state: EditorDomainState, templateKey: string, patch: Partial<PageHistoryState>): EditorDomainState {
    const current = state.states[templateKey]?.history;
    return {
        ...state,
        states: {
            ...state.states,
            [templateKey]: {
                ...state.states[templateKey],
                sections: state.states[templateKey]?.sections || {},
                history: {
                    versions: [],
                    truncated: false,
                    otherDraftCount: 0,
                    isLoading: false,
                    ...current,
                    ...patch,
                },
            },
        },
    };
}
