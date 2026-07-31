import { createReducer, on } from '@ngrx/store';

import * as actions from '../actions';

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
    on(actions.discardLinkedComponentChanges, (state, { templateKey }) => ({
        ...state,
        states: withoutKey(state.states, templateKey),
    })),
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
    on(actions.getTemplatePublishStatusSuccess, (state, { templateKey, hasChanges, published }) => ({
        ...state,
        states: {
            ...state.states,
            [templateKey]: {
                ...state.states[templateKey],
                hasChanges,
                published
            }
        }
    })),
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

function withoutKey<T>(source: Record<string, T>, key: string): Record<string, T> {
    const result = { ...source };
    delete result[key];
    return result;
}
