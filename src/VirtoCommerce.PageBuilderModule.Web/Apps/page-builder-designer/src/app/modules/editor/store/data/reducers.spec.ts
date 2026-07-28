import { editorDataReducers } from './reducers';
import { initialState } from './state';
import * as actions from '../actions/data';
import * as linkedActions from '../actions/linked-components';

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
    });

    it('caches linked metadata/content without changing raw templates', () => {
        const raw = template('Home');
        const content = template('Shared');
        const component = { id: 'component-1', storeId: 'store-1', name: 'Shared', usageCount: 0, usagePages: [] };
        let state = editorDataReducers(initialState, actions.loadTemplateModelSuccess({ template: raw, templateKey: 'home' }));

        state = editorDataReducers(state, linkedActions.cacheLinkedComponent({ component, content }));

        expect(state.templates['home']).toBe(raw);
        expect(state.linkedComponents[component.id]).toBe(component);
        expect(state.linkedComponentContents[component.id]).toBe(content);
    });

    it('removes stale linked content when a refresh fails', () => {
        const content = template('Shared');
        const component = { id: 'component-1', storeId: 'store-1', name: 'Shared', usageCount: 0, usagePages: [] };
        let state = editorDataReducers(initialState, linkedActions.cacheLinkedComponent({ component, content }));

        state = editorDataReducers(state, linkedActions.linkedComponentLoadFailed({
            componentId: component.id,
            error: 'not found',
        }));

        expect(state.linkedComponentContents[component.id]).toBeUndefined();
        expect(state.linkedComponentErrors[component.id]).toBe('not found');
    });

    it('keeps a content error on metadata-only refresh and clears it after content succeeds', () => {
        const content = template('Shared');
        const component = { id: 'component-1', storeId: 'store-1', name: 'Shared', usageCount: 0, usagePages: [] };
        let state = editorDataReducers(initialState, linkedActions.linkedComponentLoadFailed({
            componentId: component.id,
            error: 'not found',
        }));

        state = editorDataReducers(state, linkedActions.cacheLinkedComponent({ component }));
        expect(state.linkedComponentErrors[component.id]).toBe('not found');

        state = editorDataReducers(state, linkedActions.cacheLinkedComponentContent({
            componentId: component.id,
            content,
        }));
        expect(state.linkedComponentErrors[component.id]).toBeUndefined();
    });

    it('replaces search metadata with complete details and finishes the active request', () => {
        const summary = { id: 'component-1', storeId: 'store-1', name: 'Shared', usageCount: 4, usagePages: [] };
        const details = { ...summary, usagePages: [{ id: 'homepage', name: 'Homepage' }] };
        let state = editorDataReducers(initialState, linkedActions.cacheLinkedComponent({ component: summary }));
        state = editorDataReducers(state, linkedActions.loadLinkedComponentDetails({ componentId: summary.id }));

        state = editorDataReducers(state, linkedActions.loadLinkedComponentDetailsSuccess({ component: details }));

        expect(state.linkedComponents[summary.id]).toBe(details);
        expect(state.linkedComponentDetails).toEqual({
            componentId: summary.id,
            loading: false,
            error: null,
        });
    });

    it('ignores stale detail status while retaining valid metadata from the response', () => {
        const component = { id: 'component-1', storeId: 'store-1', name: 'Shared', usageCount: 1, usagePages: [] };
        let state = editorDataReducers(initialState, linkedActions.loadLinkedComponentDetails({ componentId: component.id }));
        state = editorDataReducers(state, linkedActions.loadLinkedComponentDetails({ componentId: 'component-2' }));

        state = editorDataReducers(state, linkedActions.loadLinkedComponentDetailsSuccess({ component }));

        expect(state.linkedComponents[component.id]).toBe(component);
        expect(state.linkedComponentDetails).toEqual({
            componentId: 'component-2',
            loading: true,
            error: null,
        });
    });

    it('does not downgrade active details when a delayed search response arrives', () => {
        const summary = { id: 'component-1', storeId: 'store-1', name: 'Shared', usageCount: 4, usagePages: [] };
        const details = { ...summary, usagePages: [{ id: 'homepage', name: 'Homepage' }] };
        let state = editorDataReducers(initialState, linkedActions.loadLinkedComponentDetails({ componentId: summary.id }));
        state = editorDataReducers(state, linkedActions.loadLinkedComponentDetailsSuccess({ component: details }));

        state = editorDataReducers(state, linkedActions.searchLinkedComponentsSuccess({
            keyword: '',
            result: { totalCount: 1, results: [summary] },
        }));

        expect(state.linkedComponents[summary.id]).toBe(details);
    });

    it('appends a next search page without duplicating overlapping results', () => {
        const first = { id: 'component-1', storeId: 'store-1', name: 'First', usageCount: 0, usagePages: [] };
        const second = { id: 'component-2', storeId: 'store-1', name: 'Second', usageCount: 0, usagePages: [] };
        let state = editorDataReducers(initialState, linkedActions.searchLinkedComponentsSuccess({
            keyword: '',
            result: { totalCount: 3, results: [first] },
        }));
        state = editorDataReducers(state, linkedActions.searchLinkedComponents({ keyword: '', skip: 1 }));

        state = editorDataReducers(state, linkedActions.searchLinkedComponentsSuccess({
            keyword: '',
            result: { totalCount: 3, results: [first, second] },
            append: true,
        }));

        expect(state.linkedComponentsSearch.resultIds).toEqual([first.id, second.id]);
        expect(state.linkedComponentsSearch.totalCount).toBe(3);
    });
});
