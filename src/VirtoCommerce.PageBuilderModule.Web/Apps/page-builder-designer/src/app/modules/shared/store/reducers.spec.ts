import { sharedReducers } from './reducers';
import { initialState } from './state';
import * as actions from './actions';

describe('sharedReducers', () => {
    it('returns initial state for unknown action', () => {
        const state = sharedReducers(undefined, { type: '@@INIT' });
        expect(state).toEqual(initialState);
    });

    // ── loadTemplateEntries ───────────────────────────────────────

    describe('loadTemplateEntries', () => {
        it('sets loading true', () => {
            const state = sharedReducers(initialState, actions.loadTemplateEntries());
            expect(state.templatesEntriesLoading).toBe(true);
            expect(state.templatesEntriesLoaded).toBe(false);
        });
    });

    describe('useTemplateEntries', () => {
        it('stores entries and clears loading', () => {
            const entries = { home: { type: 'page' } } as any;
            const prev = { ...initialState, templatesEntriesLoading: true };
            const state = sharedReducers(prev, actions.useTemplateEntries({ templatesEntries: entries }));
            expect(state.templatesEntriesLoading).toBe(false);
            expect(state.templatesEntriesLoaded).toBe(true);
            expect(state.templatesEntries).toBe(entries);
        });
    });

    describe('loadTemplateEntriesFails', () => {
        it('clears loading', () => {
            const prev = { ...initialState, templatesEntriesLoading: true };
            const state = sharedReducers(prev, actions.loadTemplateEntriesFails({ error: {} as any }));
            expect(state.templatesEntriesLoading).toBe(false);
            expect(state.templatesEntriesLoaded).toBe(false);
        });
    });

    // ── initShared ────────────────────────────────────────────────

    describe('initShared', () => {
        it('sets appInitialized to true', () => {
            const state = sharedReducers(initialState, actions.initShared());
            expect(state.appInitialized).toBe(true);
        });
    });

    // ── filterTemplates ───────────────────────────────────────────

    describe('filterTemplates', () => {
        it('sets templatesFilter', () => {
            const state = sharedReducers(initialState, actions.filterTemplates({ filter: 'home' }));
            expect(state.templatesFilter).toBe('home');
        });
    });

    // ── displayRootTemplates ──────────────────────────────────────

    describe('displayRootTemplates', () => {
        it('clears selection and filter', () => {
            const prev = { ...initialState, templateSelected: 'home', templatesFilter: 'test' };
            const state = sharedReducers(prev, actions.displayRootTemplates());
            expect(state.templateSelected).toBeNull();
            expect(state.templatesFilter).toBeNull();
        });
    });

    // ── children templates ────────────────────────────────────────

    describe('loadChildrenTemplates', () => {
        it('sets templateSelected and loading for key', () => {
            const state = sharedReducers(initialState, actions.loadChildrenTemplates({ templateKey: 'parent', onInit: false }));
            expect(state.templateSelected).toBe('parent');
            expect(state.childrenTemplatesState['parent'].isLoading).toBe(true);
        });
    });

    describe('switchToChildrenTemplates', () => {
        it('clears filter', () => {
            const prev = { ...initialState, templatesFilter: 'test' };
            const state = sharedReducers(prev, actions.switchToChildrenTemplates({ templateKey: 'parent' }));
            expect(state.templatesFilter).toBeNull();
        });
    });

    describe('loadChildrenTemplatesSuccess', () => {
        it('stores children entries and clears loading', () => {
            const prev = {
                ...initialState,
                childrenTemplatesState: { parent: { isLoading: true } as any },
            };
            const children = { child1: { type: 'page' } } as any;
            const state = sharedReducers(prev, actions.loadChildrenTemplatesSuccess({
                parentTemplate: 'parent', childrenEntries: children,
            }));
            expect(state.childrenTemplatesState['parent'].isLoading).toBe(false);
            expect(state.childrenTemplatesState['parent'].templates).toBe(children);
            expect(state.childrenTemplatesState['parent'].error).toBeNull();
        });
    });

    describe('loadChildrenTemplatesFails', () => {
        it('stores error and clears loading', () => {
            const prev = {
                ...initialState,
                childrenTemplatesState: { parent: { isLoading: true } as any },
            };
            const state = sharedReducers(prev, actions.loadChildrenTemplatesFails({
                error: { message: 'fail' } as any, parentTemplate: 'parent',
            }));
            expect(state.childrenTemplatesState['parent'].isLoading).toBe(false);
            expect(state.childrenTemplatesState['parent'].error).toBeTruthy();
        });
    });

    // ── dirty states ──────────────────────────────────────────────

    describe('setRootDirtyState', () => {
        it('sets dirty for root template', () => {
            const state = sharedReducers(initialState, actions.setRootDirtyState({ templateKey: 'home', dirty: true }));
            expect(state.entriesStates['home'].isDirty).toBe(true);
        });

        it('clears dirty for root template', () => {
            const prev = {
                ...initialState,
                entriesStates: { home: { isDirty: true } } as any,
            };
            const state = sharedReducers(prev, actions.setRootDirtyState({ templateKey: 'home', dirty: false }));
            expect(state.entriesStates['home'].isDirty).toBe(false);
        });
    });

    describe('setDirtyState', () => {
        it('sets dirty for child template', () => {
            const prev = {
                ...initialState,
                childrenTemplatesState: { parent: { isLoading: false, templates: {}, states: {} } as any },
            };
            const state = sharedReducers(prev, actions.setDirtyState({ templateKey: 'child1', parentKey: 'parent', dirty: true }));
            expect(state.childrenTemplatesState['parent'].states['child1'].isDirty).toBe(true);
        });

        it('preserves other children states', () => {
            const prev = {
                ...initialState,
                childrenTemplatesState: {
                    parent: { isLoading: false, states: { child1: { isDirty: true } } } as any,
                },
            };
            const state = sharedReducers(prev, actions.setDirtyState({ templateKey: 'child2', parentKey: 'parent', dirty: false }));
            expect(state.childrenTemplatesState['parent'].states['child1'].isDirty).toBe(true);
            expect(state.childrenTemplatesState['parent'].states['child2'].isDirty).toBe(false);
        });
    });
});
