import { createReducer, on } from '@ngrx/store';

import * as actions from '../actions';

import { EditorDataState, initialState } from './state';

export const editorDataReducers = createReducer<EditorDataState>(
    initialState,

    on(actions.useSchemasAction, (state, { schemas }) => ({ ...state, schemas })),
    on(actions.loadTemplateModelSuccess, (state, { template, templateKey }) => ({ ...state, templates: { ...state.templates, [templateKey]: template } })),
    on(actions.reloadTemplateModelSuccess, (state, { template, templateKey }) => ({ ...state, templates: { ...state.templates, [templateKey]: template } })),
    on(actions.reloadTemplateModel, (state, { templateKey }) => {
        const templates = { ...state.templates };
        if (templates[templateKey]) {
            delete templates[templateKey];
        }
        return { ...state, templates };
    }),
    on(actions.updateTemplateAction, (state, { templateKey, template }) => ({
        ...state, templates: { ...state.templates, [templateKey]: template }
    })),
    // on(actions.presetsTileMode, (state) => ({ ...state, mode: 'tile' })),
    // on(actions.applyPresetsFilter, (state, { filter }) => ({ ...state, presetsFilter: filter })),

);
