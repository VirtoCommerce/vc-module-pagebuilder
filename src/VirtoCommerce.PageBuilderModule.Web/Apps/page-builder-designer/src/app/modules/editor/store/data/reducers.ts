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
    on(actions.cacheLinkedComponent, (state, { component, content }) => ({
        ...state,
        linkedComponents: { ...state.linkedComponents, [component.id]: component },
        linkedComponentContents: content
            ? { ...state.linkedComponentContents, [component.id]: content }
            : state.linkedComponentContents,
        linkedComponentErrors: content
            ? withoutKey(state.linkedComponentErrors, component.id)
            : state.linkedComponentErrors,
    })),
    on(actions.cacheLinkedComponentContent, (state, { componentId, content }) => ({
        ...state,
        linkedComponentContents: { ...state.linkedComponentContents, [componentId]: content },
        linkedComponentErrors: withoutKey(state.linkedComponentErrors, componentId),
    })),
    on(actions.loadLinkedComponentDetails, (state, { componentId }) => ({
        ...state,
        linkedComponentDetails: { componentId, loading: true, error: null },
    })),
    on(actions.loadLinkedComponentDetailsSuccess, (state, { component }) => ({
        ...state,
        linkedComponents: { ...state.linkedComponents, [component.id]: component },
        linkedComponentDetails: state.linkedComponentDetails.componentId === component.id
            ? { componentId: component.id, loading: false, error: null }
            : state.linkedComponentDetails,
    })),
    on(actions.loadLinkedComponentDetailsFailed, (state, { componentId, error }) => ({
        ...state,
        linkedComponentDetails: state.linkedComponentDetails.componentId === componentId
            ? { componentId, loading: false, error }
            : state.linkedComponentDetails,
    })),
    on(actions.clearLinkedComponentDetails, state => ({
        ...state,
        linkedComponentDetails: { componentId: null, loading: false, error: null },
    })),
    on(actions.linkedComponentLoadFailed, (state, { componentId, error }) => ({
        ...state,
        linkedComponentContents: withoutKey(state.linkedComponentContents, componentId),
        linkedComponentErrors: { ...state.linkedComponentErrors, [componentId]: error },
    })),
    on(actions.searchLinkedComponents, (state, { keyword, skip }) => ({
        ...state,
        linkedComponentsSearch: {
            ...state.linkedComponentsSearch,
            keyword,
            resultIds: skip && state.linkedComponentsSearch.keyword === keyword
                ? state.linkedComponentsSearch.resultIds
                : [],
            totalCount: skip && state.linkedComponentsSearch.keyword === keyword
                ? state.linkedComponentsSearch.totalCount
                : 0,
            loading: true,
            error: null,
        },
    })),
    on(actions.searchLinkedComponentsSuccess, (state, { keyword, result, append }) => ({
        ...state,
        linkedComponents: result.results.reduce(
            (cache, component) => ({
                ...cache,
                [component.id]: hasLoadedDetails(state, component.id)
                    ? state.linkedComponents[component.id]
                    : component,
            }),
            state.linkedComponents,
        ),
        linkedComponentsSearch: {
            keyword,
            resultIds: mergeResultIds(
                append && state.linkedComponentsSearch.keyword === keyword
                    ? state.linkedComponentsSearch.resultIds
                    : [],
                result.results.map(component => component.id),
            ),
            totalCount: result.totalCount,
            loading: false,
            error: null,
        },
    })),
    on(actions.searchLinkedComponentsFailed, (state, { keyword, error }) => ({
        ...state,
        linkedComponentsSearch: {
            ...state.linkedComponentsSearch,
            keyword,
            loading: false,
            error,
        },
    })),
    // on(actions.presetsTileMode, (state) => ({ ...state, mode: 'tile' })),
    // on(actions.applyPresetsFilter, (state, { filter }) => ({ ...state, presetsFilter: filter })),

);

function withoutKey<T>(source: Record<string, T>, key: string): Record<string, T> {
    const result = { ...source };
    delete result[key];
    return result;
}

function hasLoadedDetails(state: EditorDataState, componentId: string): boolean {
    return state.linkedComponentDetails.componentId === componentId
        && !state.linkedComponentDetails.loading
        && !state.linkedComponentDetails.error
        && !!state.linkedComponents[componentId];
}

function mergeResultIds(current: string[], next: string[]): string[] {
    return [...new Set([...current, ...next])];
}
