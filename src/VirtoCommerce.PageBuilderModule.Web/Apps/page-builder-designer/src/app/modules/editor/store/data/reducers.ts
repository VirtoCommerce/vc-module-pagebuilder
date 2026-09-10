import { createReducer, on } from '@ngrx/store';

import * as actions from '../actions';
import { isSharedComponentReference } from '@editor/helpers/shared-component.helpers';
import { TemplateModel } from '@models/document';

import { EditorDataState, initialState } from './state';

export const editorDataReducers = createReducer<EditorDataState>(
    initialState,

    on(actions.useSchemasAction, (state, { schemas }) => ({ ...state, schemas })),
    on(actions.loadTemplateModelSuccess, (state, { template, templateKey }) => ({
        ...state,
        templates: { ...state.templates, [templateKey]: template },
        sharedComponentUsageRefreshIdsByTemplate: withoutKey(state.sharedComponentUsageRefreshIdsByTemplate, templateKey),
    })),
    on(actions.reloadTemplateModelSuccess, (state, { template, templateKey }) => ({
        ...state,
        templates: { ...state.templates, [templateKey]: template },
        sharedComponentUsageRefreshIdsByTemplate: withoutKey(state.sharedComponentUsageRefreshIdsByTemplate, templateKey),
    })),
    on(actions.reloadTemplateModel, (state, { templateKey }) => {
        const templates = { ...state.templates };
        if (templates[templateKey]) {
            delete templates[templateKey];
        }
        return { ...state, templates };
    }),
    on(actions.discardSharedComponentChanges, (state, { templateKey }) => ({
        ...state,
        templates: withoutKey(state.templates, templateKey),
        sharedComponentUsageRefreshIdsByTemplate: withoutKey(
            state.sharedComponentUsageRefreshIdsByTemplate,
            templateKey,
        ),
    })),
    on(actions.updateTemplateAction, (state, { templateKey, template }) => ({
        ...state,
        templates: { ...state.templates, [templateKey]: template },
        sharedComponentUsageRefreshIdsByTemplate: trackRemovedSharedComponents(state, templateKey, template),
    })),
    on(actions.clearSharedComponentUsageRefresh, (state, { templateKey }) => ({
        ...state,
        sharedComponentUsageRefreshIdsByTemplate: withoutKey(state.sharedComponentUsageRefreshIdsByTemplate, templateKey),
    })),
    on(actions.cacheSharedComponent, (state, { component, content, addToSearchResults }) => {
        const sharedComponents = { ...state.sharedComponents, [component.id]: component };
        const shouldAddToSearch = addToSearchResults === true
            && matchesKeyword(component, state.sharedComponentsSearch.keyword)
            && !state.sharedComponentsSearch.resultIds.includes(component.id);
        const resultIds = shouldAddToSearch
            ? sortResultIds([...state.sharedComponentsSearch.resultIds, component.id], sharedComponents)
            : state.sharedComponentsSearch.resultIds;
        const optimisticResultIds = shouldAddToSearch
            ? mergeResultIds(state.sharedComponentsSearch.optimisticResultIds, [component.id])
            : state.sharedComponentsSearch.optimisticResultIds;

        return {
            ...state,
            sharedComponents,
            sharedComponentContents: content
                ? { ...state.sharedComponentContents, [component.id]: content }
                : state.sharedComponentContents,
            sharedComponentErrors: content
                ? withoutKey(state.sharedComponentErrors, component.id)
                : state.sharedComponentErrors,
            sharedComponentsSearch: shouldAddToSearch
                ? {
                    ...state.sharedComponentsSearch,
                    resultIds,
                    optimisticResultIds,
                    totalCount: state.sharedComponentsSearch.totalCount + 1,
                    loading: true,
                    rebasePending: true,
                    error: null,
                }
                : state.sharedComponentsSearch,
        };
    }),
    on(actions.cacheSharedComponentContent, (state, { componentId, content }) => ({
        ...state,
        sharedComponentContents: { ...state.sharedComponentContents, [componentId]: content },
        sharedComponentErrors: withoutKey(state.sharedComponentErrors, componentId),
    })),
    on(actions.loadSharedComponentDetails, (state, { componentId }) => ({
        ...state,
        sharedComponentDetails: { componentId, loading: true, error: null },
    })),
    on(actions.loadSharedComponentDetailsSuccess, (state, { component }) => ({
        ...state,
        sharedComponents: { ...state.sharedComponents, [component.id]: component },
        sharedComponentDetails: state.sharedComponentDetails.componentId === component.id
            ? { componentId: component.id, loading: false, error: null }
            : state.sharedComponentDetails,
    })),
    on(actions.loadSharedComponentDetailsFailed, (state, { componentId, error }) => ({
        ...state,
        sharedComponentDetails: state.sharedComponentDetails.componentId === componentId
            ? { componentId, loading: false, error }
            : state.sharedComponentDetails,
    })),
    on(actions.clearSharedComponentDetails, state => ({
        ...state,
        sharedComponentDetails: { componentId: null, loading: false, error: null },
    })),
    on(actions.sharedComponentLoadFailed, (state, { componentId, error }) => ({
        ...state,
        sharedComponentContents: withoutKey(state.sharedComponentContents, componentId),
        sharedComponentErrors: { ...state.sharedComponentErrors, [componentId]: error },
    })),
    on(actions.refreshSharedComponentsSearch, (state, { keyword }) => {
        const normalizedKeyword = keyword.trim();
        return state.sharedComponentsSearch.keyword !== normalizedKeyword
            ? state
            : {
                ...state,
                sharedComponentsSearch: {
                    ...state.sharedComponentsSearch,
                    loading: true,
                    rebasePending: true,
                    error: null,
                },
            };
    }),
    on(actions.searchSharedComponents, actions.retrySharedComponentsSearch, (state, { keyword, skip }) => {
        const normalizedKeyword = keyword.trim();
        const appendToCurrentSearch = !!skip && state.sharedComponentsSearch.keyword === normalizedKeyword;

        return {
            ...state,
            sharedComponentsSearch: {
                ...state.sharedComponentsSearch,
                keyword: normalizedKeyword,
                resultIds: appendToCurrentSearch
                    ? state.sharedComponentsSearch.resultIds
                    : [],
                optimisticResultIds: appendToCurrentSearch
                    ? state.sharedComponentsSearch.optimisticResultIds
                    : [],
                loadedCount: appendToCurrentSearch
                    ? state.sharedComponentsSearch.loadedCount
                    : 0,
                totalCount: appendToCurrentSearch
                    ? state.sharedComponentsSearch.totalCount
                    : 0,
                loading: true,
                rebasePending: appendToCurrentSearch
                    ? state.sharedComponentsSearch.rebasePending
                    : false,
                error: null,
            },
        };
    }),
    on(actions.searchSharedComponentsSuccess, (state, { keyword, result, append, rebase }) => {
        const normalizedKeyword = keyword.trim();
        if (state.sharedComponentsSearch.keyword !== normalizedKeyword) {
            return state;
        }
        if (append && state.sharedComponentsSearch.rebasePending) {
            return state;
        }

        const sharedComponents = result.results.reduce(
            (cache, component) => ({
                ...cache,
                [component.id]: hasLoadedDetails(state, component.id)
                    ? state.sharedComponents[component.id]
                    : component,
            }),
            state.sharedComponents,
        );
        const serverResultIds = result.results.map(component => component.id);
        const optimisticResultIds = append || rebase
            ? state.sharedComponentsSearch.optimisticResultIds.filter(id => !serverResultIds.includes(id))
            : [];
        let currentResultIds: string[] = [];
        if (append && state.sharedComponentsSearch.keyword === keyword) {
            currentResultIds = state.sharedComponentsSearch.resultIds;
        } else if (rebase) {
            currentResultIds = optimisticResultIds;
        }

        return {
            ...state,
            sharedComponents,
            sharedComponentsSearch: {
                keyword: normalizedKeyword,
                resultIds: sortResultIds(
                    mergeResultIds(currentResultIds, serverResultIds),
                    sharedComponents,
                ),
                optimisticResultIds,
                loadedCount: append && state.sharedComponentsSearch.keyword === keyword
                    ? state.sharedComponentsSearch.loadedCount + result.results.length
                    : result.results.length,
                totalCount: result.totalCount,
                loading: false,
                rebasePending: false,
                error: null,
            },
        };
    }),
    on(actions.searchSharedComponentsFailed, (state, { keyword, error }) => {
        const normalizedKeyword = keyword.trim();
        return state.sharedComponentsSearch.keyword !== normalizedKeyword
            ? state
            : {
                ...state,
                sharedComponentsSearch: {
                    ...state.sharedComponentsSearch,
                    loading: false,
                    error,
                },
            };
    }),
    // on(actions.presetsTileMode, (state) => ({ ...state, mode: 'tile' })),
    // on(actions.applyPresetsFilter, (state, { filter }) => ({ ...state, presetsFilter: filter })),

);

function withoutKey<T>(source: Record<string, T>, key: string): Record<string, T> {
    const result = { ...source };
    delete result[key];
    return result;
}

function trackRemovedSharedComponents(
    state: EditorDataState,
    templateKey: string,
    template: TemplateModel,
): Record<string, string[]> {
    const previousIds = getSharedComponentIds(state.templates[templateKey]);
    const currentIds = new Set(getSharedComponentIds(template));
    const removedIds = previousIds.filter(componentId => !currentIds.has(componentId));
    if (removedIds.length === 0) {
        return state.sharedComponentUsageRefreshIdsByTemplate;
    }

    return {
        ...state.sharedComponentUsageRefreshIdsByTemplate,
        [templateKey]: [
            ...new Set([
                ...(state.sharedComponentUsageRefreshIdsByTemplate[templateKey] || []),
                ...removedIds,
            ]),
        ],
    };
}

function getSharedComponentIds(template: TemplateModel | undefined): string[] {
    return template
        ? [...new Set(template.content.filter(isSharedComponentReference).map(reference => reference.componentRef))]
        : [];
}

function hasLoadedDetails(state: EditorDataState, componentId: string): boolean {
    return state.sharedComponentDetails.componentId === componentId
        && !state.sharedComponentDetails.loading
        && !state.sharedComponentDetails.error
        && !!state.sharedComponents[componentId];
}

function mergeResultIds(current: string[], next: string[]): string[] {
    return [...new Set([...current, ...next])];
}

function matchesKeyword(component: { name: string }, keyword: string): boolean {
    return component.name.toLocaleLowerCase().includes(keyword.trim().toLocaleLowerCase());
}

function sortResultIds(
    ids: string[],
    components: EditorDataState['sharedComponents'],
): string[] {
    return [...ids].sort((leftId, rightId) => {
        const left = components[leftId];
        const right = components[rightId];
        const nameComparison = left.name.localeCompare(right.name);
        return nameComparison || left.id.localeCompare(right.id);
    });
}
