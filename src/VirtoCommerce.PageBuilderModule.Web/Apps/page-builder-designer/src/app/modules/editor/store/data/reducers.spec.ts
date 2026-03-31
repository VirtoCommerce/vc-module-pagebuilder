import { editorDataReducers } from './reducers';
import { initialState } from './state';
import * as actions from '../actions/data';

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
});
