import { createReducer, on } from '@ngrx/store';

import * as actions from '../actions';
import { isLinkedComponentReference } from '@editor/helpers/linked-component.helpers';
import { TemplateModel } from '@models/document';

import { EditorDataState, initialState } from './state';

export const editorDataReducers = createReducer<EditorDataState>(
    initialState,

    on(actions.useSchemasAction, (state, { schemas }) => ({ ...state, schemas })),
    on(actions.loadTemplateModelSuccess, (state, { template, templateKey }) => ({
        ...state,
        templates: { ...state.templates, [templateKey]: template },
        linkedComponentUsageRefreshIdsByTemplate: withoutKey(state.linkedComponentUsageRefreshIdsByTemplate, templateKey),
    })),
    on(actions.reloadTemplateModelSuccess, (state, { template, templateKey }) => ({
        ...state,
        templates: { ...state.templates, [templateKey]: template },
        linkedComponentUsageRefreshIdsByTemplate: withoutKey(state.linkedComponentUsageRefreshIdsByTemplate, templateKey),
    })),
    on(actions.reloadTemplateModel, (state, { templateKey }) => {
        const templates = { ...state.templates };
        if (templates[templateKey]) {
            delete templates[templateKey];
        }
        return { ...state, templates };
    }),
    on(actions.discardLinkedComponentChanges, (state, { templateKey }) => ({
        ...state,
        templates: withoutKey(state.templates, templateKey),
        linkedComponentUsageRefreshIdsByTemplate: withoutKey(
            state.linkedComponentUsageRefreshIdsByTemplate,
            templateKey,
        ),
    })),
    on(actions.updateTemplateAction, (state, { templateKey, template }) => ({
        ...state,
        templates: { ...state.templates, [templateKey]: template },
        linkedComponentUsageRefreshIdsByTemplate: trackRemovedLinkedComponents(state, templateKey, template),
    })),
    on(actions.clearLinkedComponentUsageRefresh, (state, { templateKey }) => ({
        ...state,
        linkedComponentUsageRefreshIdsByTemplate: withoutKey(state.linkedComponentUsageRefreshIdsByTemplate, templateKey),
    })),
    on(actions.cacheLinkedComponent, (state, { component, content, addToSearchResults }) => {
        const linkedComponents = { ...state.linkedComponents, [component.id]: component };
        const shouldAddToSearch = addToSearchResults === true
            && matchesKeyword(component, state.linkedComponentsSearch.keyword)
            && !state.linkedComponentsSearch.resultIds.includes(component.id);
        const resultIds = shouldAddToSearch
            ? sortResultIds([...state.linkedComponentsSearch.resultIds, component.id], linkedComponents)
            : state.linkedComponentsSearch.resultIds;
        const optimisticResultIds = shouldAddToSearch
            ? mergeResultIds(state.linkedComponentsSearch.optimisticResultIds, [component.id])
            : state.linkedComponentsSearch.optimisticResultIds;

        return {
            ...state,
            linkedComponents,
            linkedComponentContents: content
                ? { ...state.linkedComponentContents, [component.id]: content }
                : state.linkedComponentContents,
            linkedComponentErrors: content
                ? withoutKey(state.linkedComponentErrors, component.id)
                : state.linkedComponentErrors,
            linkedComponentsSearch: shouldAddToSearch
                ? {
                    ...state.linkedComponentsSearch,
                    resultIds,
                    optimisticResultIds,
                    totalCount: state.linkedComponentsSearch.totalCount + 1,
                    loading: true,
                    rebasePending: true,
                    error: null,
                }
                : state.linkedComponentsSearch,
        };
    }),
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
    on(actions.refreshLinkedComponentsSearch, (state, { keyword }) => {
        const normalizedKeyword = keyword.trim();
        return state.linkedComponentsSearch.keyword !== normalizedKeyword
            ? state
            : {
                ...state,
                linkedComponentsSearch: {
                    ...state.linkedComponentsSearch,
                    loading: true,
                    rebasePending: true,
                    error: null,
                },
            };
    }),
    on(actions.searchLinkedComponents, actions.retryLinkedComponentsSearch, (state, { keyword, skip }) => {
        const normalizedKeyword = keyword.trim();
        const appendToCurrentSearch = !!skip && state.linkedComponentsSearch.keyword === normalizedKeyword;

        return {
            ...state,
            linkedComponentsSearch: {
                ...state.linkedComponentsSearch,
                keyword: normalizedKeyword,
                resultIds: appendToCurrentSearch
                    ? state.linkedComponentsSearch.resultIds
                    : [],
                optimisticResultIds: appendToCurrentSearch
                    ? state.linkedComponentsSearch.optimisticResultIds
                    : [],
                loadedCount: appendToCurrentSearch
                    ? state.linkedComponentsSearch.loadedCount
                    : 0,
                totalCount: appendToCurrentSearch
                    ? state.linkedComponentsSearch.totalCount
                    : 0,
                loading: true,
                rebasePending: appendToCurrentSearch
                    ? state.linkedComponentsSearch.rebasePending
                    : false,
                error: null,
            },
        };
    }),
    on(actions.searchLinkedComponentsSuccess, (state, { keyword, result, append, rebase }) => {
        const normalizedKeyword = keyword.trim();
        if (state.linkedComponentsSearch.keyword !== normalizedKeyword) {
            return state;
        }
        if (append && state.linkedComponentsSearch.rebasePending) {
            return state;
        }

        const linkedComponents = result.results.reduce(
            (cache, component) => ({
                ...cache,
                [component.id]: hasLoadedDetails(state, component.id)
                    ? state.linkedComponents[component.id]
                    : component,
            }),
            state.linkedComponents,
        );
        const serverResultIds = result.results.map(component => component.id);
        const optimisticResultIds = append || rebase
            ? state.linkedComponentsSearch.optimisticResultIds.filter(id => !serverResultIds.includes(id))
            : [];
        let currentResultIds: string[] = [];
        if (append && state.linkedComponentsSearch.keyword === keyword) {
            currentResultIds = state.linkedComponentsSearch.resultIds;
        } else if (rebase) {
            currentResultIds = optimisticResultIds;
        }

        return {
            ...state,
            linkedComponents,
            linkedComponentsSearch: {
                keyword: normalizedKeyword,
                resultIds: sortResultIds(
                    mergeResultIds(currentResultIds, serverResultIds),
                    linkedComponents,
                ),
                optimisticResultIds,
                loadedCount: append && state.linkedComponentsSearch.keyword === keyword
                    ? state.linkedComponentsSearch.loadedCount + result.results.length
                    : result.results.length,
                totalCount: result.totalCount,
                loading: false,
                rebasePending: false,
                error: null,
            },
        };
    }),
    on(actions.searchLinkedComponentsFailed, (state, { keyword, error }) => {
        const normalizedKeyword = keyword.trim();
        return state.linkedComponentsSearch.keyword !== normalizedKeyword
            ? state
            : {
                ...state,
                linkedComponentsSearch: {
                    ...state.linkedComponentsSearch,
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

function trackRemovedLinkedComponents(
    state: EditorDataState,
    templateKey: string,
    template: TemplateModel,
): Record<string, string[]> {
    const previousIds = getLinkedComponentIds(state.templates[templateKey]);
    const currentIds = new Set(getLinkedComponentIds(template));
    const removedIds = previousIds.filter(componentId => !currentIds.has(componentId));
    if (removedIds.length === 0) {
        return state.linkedComponentUsageRefreshIdsByTemplate;
    }

    return {
        ...state.linkedComponentUsageRefreshIdsByTemplate,
        [templateKey]: [
            ...new Set([
                ...(state.linkedComponentUsageRefreshIdsByTemplate[templateKey] || []),
                ...removedIds,
            ]),
        ],
    };
}

function getLinkedComponentIds(template: TemplateModel | undefined): string[] {
    return template
        ? [...new Set(template.content.filter(isLinkedComponentReference).map(reference => reference.componentRef))]
        : [];
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

function matchesKeyword(component: { name: string }, keyword: string): boolean {
    return component.name.toLocaleLowerCase().includes(keyword.trim().toLocaleLowerCase());
}

function sortResultIds(
    ids: string[],
    components: EditorDataState['linkedComponents'],
): string[] {
    return [...ids].sort((leftId, rightId) => {
        const left = components[leftId];
        const right = components[rightId];
        const nameComparison = left.name.localeCompare(right.name);
        return nameComparison || left.id.localeCompare(right.id);
    });
}
