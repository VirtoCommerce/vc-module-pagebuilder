import { editorDomainReducers } from './reducers';
import { initialState } from './state';
import * as actions from '../actions';

describe('editorDomainReducers', () => {
    it('returns initial state for unknown action', () => {
        const state = editorDomainReducers(undefined, { type: '@@INIT' });
        expect(state).toEqual(initialState);
    });

    // ── loadTemplateSchemas ───────────────────────────────────────

    describe('loadTemplateSchemas', () => {
        it('sets schemaLoading to true', () => {
            const state = editorDomainReducers(initialState, actions.loadTemplateSchemas());
            expect(state.schemaLoading).toBe(true);
        });
    });

    describe('loadTemplateSchemasSuccess', () => {
        it('sets schemaLoading to false', () => {
            const loading = { ...initialState, schemaLoading: true };
            const state = editorDomainReducers(loading, actions.loadTemplateSchemasSuccess({ schemas: null }));
            expect(state.schemaLoading).toBe(false);
        });
    });

    // ── loadTemplateModel ─────────────────────────────────────────

    describe('loadTemplateModel', () => {
        it('sets isLoading for template key', () => {
            const state = editorDomainReducers(initialState, actions.loadTemplateModel({ templateKey: 'home' }));
            expect(state.states['home'].isLoading).toBe(true);
        });

        it('preserves existing sections', () => {
            const prev = {
                ...initialState,
                states: { home: { isLoading: false, sections: { s1: { expanded: true } } } as any },
            };
            const state = editorDomainReducers(prev, actions.loadTemplateModel({ templateKey: 'home' }));
            expect(state.states['home'].sections['s1']).toEqual({ expanded: true });
        });

        it('does not affect other template keys', () => {
            const prev = {
                ...initialState,
                states: { about: { isLoading: false, sections: {} } as any },
            };
            const state = editorDomainReducers(prev, actions.loadTemplateModel({ templateKey: 'home' }));
            expect(state.states['about'].isLoading).toBe(false);
        });
    });

    describe('loadTemplateModelSuccess', () => {
        it('clears isLoading and error', () => {
            const prev = {
                ...initialState,
                states: { home: { isLoading: true, error: 'prev', sections: {} } as any },
            };
            const state = editorDomainReducers(prev, actions.loadTemplateModelSuccess({ template: {} as any, templateKey: 'home' }));
            expect(state.states['home'].isLoading).toBe(false);
            expect(state.states['home'].error).toBeUndefined();
        });
    });

    it('discards only the synthetic shared-component domain state', () => {
        const templateKey = 'shared-component::component-1';
        const previous = {
            ...initialState,
            states: {
                home: { isLoading: false, sections: {} } as any,
                [templateKey]: { isLoading: false, sections: {}, error: 'stale' } as any,
            },
        };

        const state = editorDomainReducers(previous, actions.discardSharedComponentChanges({ templateKey }));

        expect(state.states['home']).toBe(previous.states['home']);
        expect(state.states[templateKey]).toBeUndefined();
    });

    describe('loadTemplateModelFails', () => {
        it('stores error message and clears loading', () => {
            const prev = {
                ...initialState,
                states: { home: { isLoading: true, sections: {} } as any },
            };
            const error = { message: 'Not found' } as any;
            const state = editorDomainReducers(prev, actions.loadTemplateModelFails({ error, templateKey: 'home' }));
            expect(state.states['home'].isLoading).toBe(false);
            expect(state.states['home'].error).toBe('Not found');
        });
    });

    // ── getTemplatePublishStatusSuccess ────────────────────────────

    describe('getTemplatePublishStatusSuccess', () => {
        it('sets hasChanges and published', () => {
            const prev = {
                ...initialState,
                states: { home: { isLoading: false, sections: {} } as any },
            };
            const state = editorDomainReducers(prev, actions.getTemplatePublishStatusSuccess({
                templateKey: 'home', hasChanges: true, published: false,
            }));
            expect(state.states['home'].hasChanges).toBe(true);
            expect(state.states['home'].published).toBe(false);
        });
    });

    // ── sectionStateChangedAction ─────────────────────────────────

    describe('sectionStateChangedAction', () => {
        it('updates section state', () => {
            const prev = {
                ...initialState,
                states: { home: { isLoading: false, sections: {} } as any },
            };
            const state = editorDomainReducers(prev, actions.sectionStateChangedAction({
                templateKey: 'home',
                sectionId: 's1',
                state: { expanded: true, blocks: {} } as any,
            }));
            expect(state.states['home'].sections['s1'].expanded).toBe(true);
        });

        it('deep merges blocks', () => {
            const prev = {
                ...initialState,
                states: {
                    home: {
                        isLoading: false,
                        sections: {
                            s1: { expanded: true, blocks: { b1: { active: true } } },
                        },
                    } as any,
                },
            };
            const state = editorDomainReducers(prev, actions.sectionStateChangedAction({
                templateKey: 'home',
                sectionId: 's1',
                state: { blocks: { b2: { active: false } } } as any,
            }));
            expect(state.states['home'].sections['s1'].blocks['b1']).toEqual({ active: true });
            expect(state.states['home'].sections['s1'].blocks['b2']).toEqual({ active: false });
        });

        it('preserves other sections', () => {
            const prev = {
                ...initialState,
                states: {
                    home: {
                        isLoading: false,
                        sections: { s1: { expanded: true, blocks: {} } },
                    } as any,
                },
            };
            const state = editorDomainReducers(prev, actions.sectionStateChangedAction({
                templateKey: 'home',
                sectionId: 's2',
                state: { expanded: false, blocks: {} } as any,
            }));
            expect(state.states['home'].sections['s1'].expanded).toBe(true);
            expect(state.states['home'].sections['s2'].expanded).toBe(false);
        });
    });
});
