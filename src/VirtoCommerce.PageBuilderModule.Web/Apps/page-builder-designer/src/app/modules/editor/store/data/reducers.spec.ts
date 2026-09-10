import { editorDataReducers } from './reducers';
import { initialState } from './state';
import * as actions from '../actions/data';
import * as sharedComponentActions from '../actions/shared-components';

const template = (name: string) => ({ settings: { name }, content: [] }) as any;

describe('editorDataReducers', () => {
    it('returns initial state for unknown action', () => {
        const state = editorDataReducers(undefined, { type: '@@INIT' });
        expect(state).toEqual(initialState);
    });

    describe('useSchemasAction', () => {
        it('sets schemas', () => {
            const schemas = { hero: { settings: [] } } as any;
            const state = editorDataReducers(initialState, actions.useSchemasAction({ schemas }));
            expect(state.schemas).toBe(schemas);
            expect(state.templates).toEqual({});
        });
    });

    describe('loadTemplateModelSuccess', () => {
        it('adds template by key', () => {
            const t = template('Home');
            const state = editorDataReducers(initialState, actions.loadTemplateModelSuccess({ template: t, templateKey: 'home' }));
            expect(state.templates['home']).toBe(t);
        });

        it('does not overwrite other templates', () => {
            const t1 = template('Home');
            const t2 = template('About');
            const s1 = editorDataReducers(initialState, actions.loadTemplateModelSuccess({ template: t1, templateKey: 'home' }));
            const s2 = editorDataReducers(s1, actions.loadTemplateModelSuccess({ template: t2, templateKey: 'about' }));
            expect(s2.templates['home']).toBe(t1);
            expect(s2.templates['about']).toBe(t2);
        });
    });

    describe('reloadTemplateModel', () => {
        it('removes template to force re-fetch', () => {
            const t = template('Home');
            const loaded = editorDataReducers(initialState, actions.loadTemplateModelSuccess({ template: t, templateKey: 'home' }));
            const reloading = editorDataReducers(loaded, actions.reloadTemplateModel({ templateKey: 'home' }));
            expect(reloading.templates['home']).toBeUndefined();
        });

        it('leaves other templates intact', () => {
            const t1 = template('Home');
            const t2 = template('About');
            let state = editorDataReducers(initialState, actions.loadTemplateModelSuccess({ template: t1, templateKey: 'home' }));
            state = editorDataReducers(state, actions.loadTemplateModelSuccess({ template: t2, templateKey: 'about' }));
            state = editorDataReducers(state, actions.reloadTemplateModel({ templateKey: 'home' }));
            expect(state.templates['about']).toBe(t2);
        });
    });

    describe('updateTemplateAction', () => {
        it('replaces template in place', () => {
            const original = template('v1');
            const updated = template('v2');
            let state = editorDataReducers(initialState, actions.loadTemplateModelSuccess({ template: original, templateKey: 'home' }));
            state = editorDataReducers(state, actions.updateTemplateAction({ template: updated, templateKey: 'home' }));
            expect(state.templates['home']).toBe(updated);
        });

        it('tracks a removed shared component until its usage metadata is refreshed', () => {
            const original = {
                settings: {},
                content: [{ id: 'placement-1', type: 'componentRef', componentRef: 'component-1' }],
            } as any;
            const detached = template('Detached');
            let state = editorDataReducers(initialState, actions.loadTemplateModelSuccess({
                template: original,
                templateKey: 'pages::page-1',
            }));

            state = editorDataReducers(state, actions.updateTemplateAction({
                template: detached,
                templateKey: 'pages::page-1',
            }));

            expect(state.sharedComponentUsageRefreshIdsByTemplate['pages::page-1']).toEqual(['component-1']);

            state = editorDataReducers(state, sharedComponentActions.clearSharedComponentUsageRefresh({
                templateKey: 'pages::page-1',
            }));
            expect(state.sharedComponentUsageRefreshIdsByTemplate['pages::page-1']).toBeUndefined();
        });
    });

    it('discards a synthetic shared-component template without affecting page templates', () => {
        const page = template('Home');
        const shared = template('Shared component');
        const templateKey = 'shared-component::component-1';
        const previous = {
            ...initialState,
            templates: { home: page, [templateKey]: shared },
            sharedComponentUsageRefreshIdsByTemplate: { [templateKey]: ['component-2'] },
        };

        const state = editorDataReducers(previous, sharedComponentActions.discardSharedComponentChanges({ templateKey }));

        expect(state.templates).toEqual({ home: page });
        expect(state.sharedComponentUsageRefreshIdsByTemplate[templateKey]).toBeUndefined();
    });

    it('caches shared metadata/content without changing raw templates', () => {
        const raw = template('Home');
        const content = template('Shared');
        const component = { id: 'component-1', storeId: 'store-1', name: 'Shared', usageCount: 0, usagePages: [] };
        let state = editorDataReducers(initialState, actions.loadTemplateModelSuccess({ template: raw, templateKey: 'home' }));

        state = editorDataReducers(state, sharedComponentActions.cacheSharedComponent({ component, content }));

        expect(state.templates['home']).toBe(raw);
        expect(state.sharedComponents[component.id]).toBe(component);
        expect(state.sharedComponentContents[component.id]).toBe(content);
    });

    it('adds a newly created component to the active library search once', () => {
        const first = { id: 'component-1', storeId: 'store-1', name: 'Alpha', usageCount: 1, usagePages: [] };
        const created = { id: 'component-2', storeId: 'store-1', name: 'Beta', usageCount: 0, usagePages: [] };
        let state = editorDataReducers(initialState, sharedComponentActions.searchSharedComponentsSuccess({
            keyword: '',
            result: { totalCount: 1, results: [first] },
        }));

        state = editorDataReducers(state, sharedComponentActions.cacheSharedComponent({
            component: created,
            addToSearchResults: true,
        }));
        state = editorDataReducers(state, sharedComponentActions.cacheSharedComponent({
            component: created,
            addToSearchResults: true,
        }));

        expect(state.sharedComponentsSearch.resultIds).toEqual([first.id, created.id]);
        expect(state.sharedComponentsSearch.optimisticResultIds).toEqual([created.id]);
        expect(state.sharedComponentsSearch.loadedCount).toBe(1);
        expect(state.sharedComponentsSearch.totalCount).toBe(2);
        expect(state.sharedComponentsSearch.rebasePending).toBe(true);
    });

    it('keeps active search and pagination semantics when caching a created component', () => {
        const first = { id: 'component-1', storeId: 'store-1', name: 'Hero', usageCount: 1, usagePages: [] };
        const matching = { id: 'component-2', storeId: 'store-1', name: 'Hero banner', usageCount: 0, usagePages: [] };
        const nonMatching = { id: 'component-3', storeId: 'store-1', name: 'Footer', usageCount: 0, usagePages: [] };
        let state = editorDataReducers(initialState, sharedComponentActions.searchSharedComponents({ keyword: 'hero' }));
        state = editorDataReducers(state, sharedComponentActions.searchSharedComponentsSuccess({
            keyword: 'hero',
            result: { totalCount: 5, results: [first] },
        }));

        state = editorDataReducers(state, sharedComponentActions.cacheSharedComponent({
            component: matching,
            addToSearchResults: true,
        }));
        state = editorDataReducers(state, sharedComponentActions.cacheSharedComponent({
            component: nonMatching,
            addToSearchResults: true,
        }));

        expect(state.sharedComponentsSearch.resultIds).toEqual([first.id, matching.id]);
        expect(state.sharedComponentsSearch.optimisticResultIds).toEqual([matching.id]);
        expect(state.sharedComponentsSearch.loadedCount).toBe(1);
        expect(state.sharedComponentsSearch.totalCount).toBe(6);
        expect(state.sharedComponentsSearch.rebasePending).toBe(true);
    });

    it('ignores stale first-page success after the active keyword changes', () => {
        const stale = { id: 'stale', storeId: 'store-1', name: 'Hero', usageCount: 0, usagePages: [] };
        let state = editorDataReducers(initialState, sharedComponentActions.retrySharedComponentsSearch({ keyword: 'hero' }));
        state = editorDataReducers(state, sharedComponentActions.searchSharedComponents({ keyword: 'footer' }));

        const next = editorDataReducers(state, sharedComponentActions.searchSharedComponentsSuccess({
            keyword: 'hero',
            result: { totalCount: 1, results: [stale] },
        }));

        expect(next).toBe(state);
        expect(next.sharedComponentsSearch.keyword).toBe('footer');
        expect(next.sharedComponentsSearch.loading).toBe(true);
        expect(next.sharedComponentsSearch.resultIds).toEqual([]);
        expect(next.sharedComponents[stale.id]).toBeUndefined();
    });

    it('ignores stale first-page failure after the active keyword changes', () => {
        let state = editorDataReducers(initialState, sharedComponentActions.retrySharedComponentsSearch({ keyword: 'hero' }));
        state = editorDataReducers(state, sharedComponentActions.searchSharedComponents({ keyword: 'footer' }));

        const next = editorDataReducers(state, sharedComponentActions.searchSharedComponentsFailed({
            keyword: 'hero',
            error: 'Stale request failed',
        }));

        expect(next).toBe(state);
        expect(next.sharedComponentsSearch.keyword).toBe('footer');
        expect(next.sharedComponentsSearch.loading).toBe(true);
        expect(next.sharedComponentsSearch.error).toBeNull();
    });

    it('normalizes the active keyword before an optimistic rebase', () => {
        let state = editorDataReducers(initialState, sharedComponentActions.searchSharedComponents({ keyword: '  hero  ' }));
        expect(state.sharedComponentsSearch.keyword).toBe('hero');

        state = editorDataReducers(state, sharedComponentActions.refreshSharedComponentsSearch({ keyword: ' hero ' }));

        expect(state.sharedComponentsSearch.keyword).toBe('hero');
        expect(state.sharedComponentsSearch.rebasePending).toBe(true);
    });

    it('rebases a partial search before loading the next page after an optimistic insert', () => {
        const bravo = { id: 'bravo', storeId: 'store-1', name: 'Bravo', usageCount: 0, usagePages: [] };
        const charlie = { id: 'charlie', storeId: 'store-1', name: 'Charlie', usageCount: 0, usagePages: [] };
        const delta = { id: 'delta', storeId: 'store-1', name: 'Delta', usageCount: 0, usagePages: [] };
        const echo = { id: 'echo', storeId: 'store-1', name: 'Echo', usageCount: 0, usagePages: [] };
        const created = { id: 'alpha', storeId: 'store-1', name: 'Alpha', usageCount: 0, usagePages: [] };
        let state = editorDataReducers(initialState, sharedComponentActions.searchSharedComponentsSuccess({
            keyword: '',
            result: { totalCount: 4, results: [bravo, charlie] },
        }));

        state = editorDataReducers(state, sharedComponentActions.cacheSharedComponent({
            component: created,
            addToSearchResults: true,
        }));
        expect(state.sharedComponentsSearch.resultIds).toEqual([created.id, bravo.id, charlie.id]);
        expect(state.sharedComponentsSearch.loadedCount).toBe(2);

        state = editorDataReducers(state, sharedComponentActions.refreshSharedComponentsSearch({ keyword: '' }));
        expect(state.sharedComponentsSearch.resultIds).toEqual([created.id, bravo.id, charlie.id]);

        state = editorDataReducers(state, sharedComponentActions.searchSharedComponentsSuccess({
            keyword: '',
            result: { totalCount: 5, results: [created, bravo] },
            rebase: true,
        }));
        expect(state.sharedComponentsSearch.resultIds).toEqual([created.id, bravo.id]);
        expect(state.sharedComponentsSearch.loadedCount).toBe(2);
        expect(state.sharedComponentsSearch.rebasePending).toBe(false);

        state = editorDataReducers(state, sharedComponentActions.searchSharedComponents({ keyword: '', skip: 2 }));
        state = editorDataReducers(state, sharedComponentActions.searchSharedComponentsSuccess({
            keyword: '',
            result: { totalCount: 5, results: [charlie, delta] },
            append: true,
        }));
        state = editorDataReducers(state, sharedComponentActions.searchSharedComponents({ keyword: '', skip: 4 }));
        state = editorDataReducers(state, sharedComponentActions.searchSharedComponentsSuccess({
            keyword: '',
            result: { totalCount: 5, results: [echo] },
            append: true,
        }));

        expect(state.sharedComponentsSearch.resultIds).toEqual([
            created.id,
            bravo.id,
            charlie.id,
            delta.id,
            echo.id,
        ]);
        expect(new Set(state.sharedComponentsSearch.resultIds).size).toBe(5);
        expect(state.sharedComponentsSearch.loadedCount).toBe(5);
        expect(state.sharedComponentsSearch.totalCount).toBe(5);
    });

    it('keeps a created component visible until server pagination reaches its sorted position', () => {
        const alpha = { id: 'alpha', storeId: 'store-1', name: 'Alpha', usageCount: 0, usagePages: [] };
        const bravo = { id: 'bravo', storeId: 'store-1', name: 'Bravo', usageCount: 0, usagePages: [] };
        const charlie = { id: 'charlie', storeId: 'store-1', name: 'Charlie', usageCount: 0, usagePages: [] };
        const delta = { id: 'delta', storeId: 'store-1', name: 'Delta', usageCount: 0, usagePages: [] };
        const created = { id: 'zulu', storeId: 'store-1', name: 'Zulu', usageCount: 0, usagePages: [] };
        let state = editorDataReducers(initialState, sharedComponentActions.searchSharedComponentsSuccess({
            keyword: '',
            result: { totalCount: 4, results: [alpha, bravo] },
        }));
        state = editorDataReducers(state, sharedComponentActions.cacheSharedComponent({
            component: created,
            addToSearchResults: true,
        }));

        state = editorDataReducers(state, sharedComponentActions.searchSharedComponentsSuccess({
            keyword: '',
            result: { totalCount: 5, results: [alpha, bravo] },
            rebase: true,
        }));
        expect(state.sharedComponentsSearch.resultIds).toEqual([alpha.id, bravo.id, created.id]);
        expect(state.sharedComponentsSearch.optimisticResultIds).toEqual([created.id]);
        expect(state.sharedComponentsSearch.loadedCount).toBe(2);

        state = editorDataReducers(state, sharedComponentActions.searchSharedComponentsSuccess({
            keyword: '',
            result: { totalCount: 5, results: [charlie, delta] },
            append: true,
        }));
        state = editorDataReducers(state, sharedComponentActions.searchSharedComponentsSuccess({
            keyword: '',
            result: { totalCount: 5, results: [created] },
            append: true,
        }));

        expect(state.sharedComponentsSearch.resultIds).toEqual([
            alpha.id,
            bravo.id,
            charlie.id,
            delta.id,
            created.id,
        ]);
        expect(state.sharedComponentsSearch.optimisticResultIds).toEqual([]);
        expect(state.sharedComponentsSearch.loadedCount).toBe(5);
    });

    it('removes stale shared content when a refresh fails', () => {
        const content = template('Shared');
        const component = { id: 'component-1', storeId: 'store-1', name: 'Shared', usageCount: 0, usagePages: [] };
        let state = editorDataReducers(initialState, sharedComponentActions.cacheSharedComponent({ component, content }));

        state = editorDataReducers(state, sharedComponentActions.sharedComponentLoadFailed({
            componentId: component.id,
            error: 'not found',
        }));

        expect(state.sharedComponentContents[component.id]).toBeUndefined();
        expect(state.sharedComponentErrors[component.id]).toBe('not found');
    });

    it('keeps a content error on metadata-only refresh and clears it after content succeeds', () => {
        const content = template('Shared');
        const component = { id: 'component-1', storeId: 'store-1', name: 'Shared', usageCount: 0, usagePages: [] };
        let state = editorDataReducers(initialState, sharedComponentActions.sharedComponentLoadFailed({
            componentId: component.id,
            error: 'not found',
        }));

        state = editorDataReducers(state, sharedComponentActions.cacheSharedComponent({ component }));
        expect(state.sharedComponentErrors[component.id]).toBe('not found');

        state = editorDataReducers(state, sharedComponentActions.cacheSharedComponentContent({
            componentId: component.id,
            content,
        }));
        expect(state.sharedComponentErrors[component.id]).toBeUndefined();
    });

    it('replaces search metadata with complete details and finishes the active request', () => {
        const summary = { id: 'component-1', storeId: 'store-1', name: 'Shared', usageCount: 4, usagePages: [] };
        const details = { ...summary, usagePages: [{ id: 'homepage', name: 'Homepage' }] };
        let state = editorDataReducers(initialState, sharedComponentActions.cacheSharedComponent({ component: summary }));
        state = editorDataReducers(state, sharedComponentActions.loadSharedComponentDetails({ componentId: summary.id }));

        state = editorDataReducers(state, sharedComponentActions.loadSharedComponentDetailsSuccess({ component: details }));

        expect(state.sharedComponents[summary.id]).toBe(details);
        expect(state.sharedComponentDetails).toEqual({
            componentId: summary.id,
            loading: false,
            error: null,
        });
    });

    it('ignores stale detail status while retaining valid metadata from the response', () => {
        const component = { id: 'component-1', storeId: 'store-1', name: 'Shared', usageCount: 1, usagePages: [] };
        let state = editorDataReducers(initialState, sharedComponentActions.loadSharedComponentDetails({ componentId: component.id }));
        state = editorDataReducers(state, sharedComponentActions.loadSharedComponentDetails({ componentId: 'component-2' }));

        state = editorDataReducers(state, sharedComponentActions.loadSharedComponentDetailsSuccess({ component }));

        expect(state.sharedComponents[component.id]).toBe(component);
        expect(state.sharedComponentDetails).toEqual({
            componentId: 'component-2',
            loading: true,
            error: null,
        });
    });

    it('does not downgrade active details when a delayed search response arrives', () => {
        const summary = { id: 'component-1', storeId: 'store-1', name: 'Shared', usageCount: 4, usagePages: [] };
        const details = { ...summary, usagePages: [{ id: 'homepage', name: 'Homepage' }] };
        let state = editorDataReducers(initialState, sharedComponentActions.loadSharedComponentDetails({ componentId: summary.id }));
        state = editorDataReducers(state, sharedComponentActions.loadSharedComponentDetailsSuccess({ component: details }));

        state = editorDataReducers(state, sharedComponentActions.searchSharedComponentsSuccess({
            keyword: '',
            result: { totalCount: 1, results: [summary] },
        }));

        expect(state.sharedComponents[summary.id]).toBe(details);
    });

    it('appends a next search page without duplicating overlapping results', () => {
        const first = { id: 'component-1', storeId: 'store-1', name: 'First', usageCount: 0, usagePages: [] };
        const second = { id: 'component-2', storeId: 'store-1', name: 'Second', usageCount: 0, usagePages: [] };
        let state = editorDataReducers(initialState, sharedComponentActions.searchSharedComponentsSuccess({
            keyword: '',
            result: { totalCount: 3, results: [first] },
        }));
        state = editorDataReducers(state, sharedComponentActions.searchSharedComponents({ keyword: '', skip: 1 }));

        state = editorDataReducers(state, sharedComponentActions.searchSharedComponentsSuccess({
            keyword: '',
            result: { totalCount: 3, results: [first, second] },
            append: true,
        }));

        expect(state.sharedComponentsSearch.resultIds).toEqual([first.id, second.id]);
        expect(state.sharedComponentsSearch.loadedCount).toBe(3);
        expect(state.sharedComponentsSearch.totalCount).toBe(3);
    });
});
