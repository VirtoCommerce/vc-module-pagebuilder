import { createReducer, on } from '@ngrx/store';

import * as actions from '../actions';
import * as sharedActions from '@shared/store/actions';

import { EditorUIState, initialState } from './state';

export const editorUIReducers = createReducer<EditorUIState>(
    initialState,

    on(actions.toggleGroupAction, (state, { groupId }) => ({
        ...state,
        addSectionPaneStates: {
            ...state.addSectionPaneStates,
            [groupId]: {
                ...state.addSectionPaneStates[groupId],
                opened: !(state.addSectionPaneStates[groupId]?.opened)
            }
        }
    })),
    on(actions.previewItemAction, (state, { item }) => ({
        ...state,
        previewItemType: item.type
    })),
    on(actions.applySectionsFilter, (state, { filter }) => ({
        ...state,
        currentSectionsFilter: filter
    })),
    on(actions.resetGroupsState, state => ({
        ...state,
        currentSectionsFilter: null,
        previewItemType: null
    })),
    on(actions.startDragSection, (state, { sectionId }) => ({
        ...state,
        dragSectionIds: [...state.dragSectionIds, sectionId]
    })),
    on(actions.releaseDragSection, (state, { sectionId: _sectionId }) => ({
        ...state,
        dragSectionIds: []
    })),

    on(actions.loadTemplateModel, state => ({ ...state, isTemplateLoading: true })),
    on(actions.loadTemplateModelSuccess, state => ({ ...state, isTemplateLoading: false })),
    on(actions.loadTemplateModelFails, state => ({ ...state, isTemplateLoading: false })),
    on(actions.saveTemplates, state => ({ ...state, isTemplateLoading: true })),
    on(actions.saveTemplateSuccess, state => ({ ...state, isTemplateLoading: false })),
    on(actions.saveTemplateFails, state => ({ ...state, isTemplateLoading: false })),
    on(actions.loadTemplateSchemas, state => ({ ...state, isSchemasLoading: true })),
    on(actions.loadTemplateSchemasSuccess, state => ({ ...state, isSchemasLoading: false })),
    on(actions.loadTemplateSchemasFails, state => ({ ...state, isSchemasLoading: false })),
    on(sharedActions.previewSectionHovered, (state, { sectionId }) => ({ ...state, hoveredSectionId: sectionId })),
);
