import { createReducer, on } from '@ngrx/store';

import * as actions from '../actions';

import { EditorDomainState, initialState } from './state';
import { SectionStatesList } from '@editor/models';
import { TemplateModel } from '@models/document';

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
    on(actions.loadTemplateModelSuccess, (state, { templateKey, template }) => ({
            ...state,
            states: {
                ...state.states,
                [templateKey]: {
                    ...state.states[templateKey],
                    isLoading: false,
                    error: undefined,
                    sections: retainExistingItemStates(state.states[templateKey]?.sections || {}, template)
                }
            }
        })
    ),
    on(actions.updateTemplateAction, (state, { templateKey, template }) => ({
        ...state,
        states: {
            ...state.states,
            [templateKey]: {
                ...state.states[templateKey],
                sections: retainExistingItemStates(state.states[templateKey]?.sections || {}, template)
            }
        }
    })),
    on(actions.discardSharedComponentChanges, (state, { templateKey }) => ({
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

function retainExistingItemStates(states: SectionStatesList, template: TemplateModel): SectionStatesList {
    return Object.fromEntries((template.content || []).filter(section => states[section.id]).map(section => [
        section.id,
        {
            ...states[section.id],
            blocks: Object.fromEntries((section.blocks || [])
                .filter(block => states[section.id].blocks?.[block.id])
                .map(block => [block.id, states[section.id].blocks[block.id]]))
        }
    ]));
}

function withoutKey<T>(source: Record<string, T>, key: string): Record<string, T> {
    const result = { ...source };
    delete result[key];
    return result;
}
