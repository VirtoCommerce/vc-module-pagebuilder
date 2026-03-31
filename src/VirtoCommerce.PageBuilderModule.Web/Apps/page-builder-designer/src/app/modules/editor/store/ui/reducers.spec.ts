import { editorUIReducers } from './reducers';
import { initialState } from './state';
import * as actions from '../actions';
import * as sharedActions from '@shared/store/actions';

describe('editorUIReducers', () => {
    it('returns initial state for unknown action', () => {
        const state = editorUIReducers(undefined, { type: '@@INIT' });
        expect(state).toEqual(initialState);
    });

    // ── toggleGroupAction ─────────────────────────────────────────

    describe('toggleGroupAction', () => {
        it('opens a closed group', () => {
            const state = editorUIReducers(initialState, actions.toggleGroupAction({ groupId: 'g1' }));
            expect(state.addSectionPaneStates['g1'].opened).toBe(true);
        });

        it('closes an open group', () => {
            const prev = {
                ...initialState,
                addSectionPaneStates: { g1: { opened: true } },
            };
            const state = editorUIReducers(prev, actions.toggleGroupAction({ groupId: 'g1' }));
            expect(state.addSectionPaneStates['g1'].opened).toBe(false);
        });

        it('does not affect other groups', () => {
            const prev = {
                ...initialState,
                addSectionPaneStates: { g1: { opened: true } },
            };
            const state = editorUIReducers(prev, actions.toggleGroupAction({ groupId: 'g2' }));
            expect(state.addSectionPaneStates['g1'].opened).toBe(true);
            expect(state.addSectionPaneStates['g2'].opened).toBe(true);
        });
    });

    // ── previewItemAction ─────────────────────────────────────────

    describe('previewItemAction', () => {
        it('sets previewItemType', () => {
            const state = editorUIReducers(initialState, actions.previewItemAction({ item: { type: 'hero' } as any }));
            expect(state.previewItemType).toBe('hero');
        });
    });

    // ── applySectionsFilter ───────────────────────────────────────

    describe('applySectionsFilter', () => {
        it('sets filter', () => {
            const state = editorUIReducers(initialState, actions.applySectionsFilter({ filter: 'hero' }));
            expect(state.currentSectionsFilter).toBe('hero');
        });

        it('clears filter with null', () => {
            const prev = { ...initialState, currentSectionsFilter: 'hero' };
            const state = editorUIReducers(prev, actions.applySectionsFilter({ filter: null }));
            expect(state.currentSectionsFilter).toBeNull();
        });
    });

    // ── resetGroupsState ──────────────────────────────────────────

    describe('resetGroupsState', () => {
        it('resets filter and preview', () => {
            const prev = { ...initialState, currentSectionsFilter: 'hero', previewItemType: 'banner' };
            const state = editorUIReducers(prev, actions.resetGroupsState());
            expect(state.currentSectionsFilter).toBeNull();
            expect(state.previewItemType).toBeNull();
        });
    });

    // ── drag section ──────────────────────────────────────────────

    describe('startDragSection', () => {
        it('adds section id to drag list', () => {
            const state = editorUIReducers(initialState, actions.startDragSection({ sectionId: 's1' }));
            expect(state.dragSectionIds).toEqual(['s1']);
        });

        it('accumulates multiple drag ids', () => {
            let state = editorUIReducers(initialState, actions.startDragSection({ sectionId: 's1' }));
            state = editorUIReducers(state, actions.startDragSection({ sectionId: 's2' }));
            expect(state.dragSectionIds).toEqual(['s1', 's2']);
        });
    });

    describe('releaseDragSection', () => {
        it('clears drag list', () => {
            const prev = { ...initialState, dragSectionIds: ['s1', 's2'] };
            const state = editorUIReducers(prev, actions.releaseDragSection({ sectionId: 's1' }));
            expect(state.dragSectionIds).toEqual([]);
        });
    });

    // ── template loading states ───────────────────────────────────

    describe('template loading', () => {
        it('loadTemplateModel sets isTemplateLoading', () => {
            const state = editorUIReducers(initialState, actions.loadTemplateModel({ templateKey: 'k' }));
            expect(state.isTemplateLoading).toBe(true);
        });

        it('loadTemplateModelSuccess clears isTemplateLoading', () => {
            const prev = { ...initialState, isTemplateLoading: true };
            const state = editorUIReducers(prev, actions.loadTemplateModelSuccess({ template: {} as any, templateKey: 'k' }));
            expect(state.isTemplateLoading).toBe(false);
        });

        it('loadTemplateModelFails clears isTemplateLoading', () => {
            const prev = { ...initialState, isTemplateLoading: true };
            const state = editorUIReducers(prev, actions.loadTemplateModelFails({ error: {} as any, templateKey: 'k' }));
            expect(state.isTemplateLoading).toBe(false);
        });
    });

    // ── save loading states ───────────────────────────────────────

    describe('save loading', () => {
        it('saveTemplates sets isTemplateLoading', () => {
            const state = editorUIReducers(initialState, actions.saveTemplates({ templates: [] }));
            expect(state.isTemplateLoading).toBe(true);
        });

        it('saveTemplateSuccess clears isTemplateLoading', () => {
            const prev = { ...initialState, isTemplateLoading: true };
            const state = editorUIReducers(prev, actions.saveTemplateSuccess({ templateKey: 'k', template: {} as any }));
            expect(state.isTemplateLoading).toBe(false);
        });

        it('saveTemplateFails clears isTemplateLoading', () => {
            const prev = { ...initialState, isTemplateLoading: true };
            const state = editorUIReducers(prev, actions.saveTemplateFails({ error: {} as any }));
            expect(state.isTemplateLoading).toBe(false);
        });
    });

    // ── schema loading states ─────────────────────────────────────

    describe('schema loading', () => {
        it('loadTemplateSchemas sets isSchemasLoading', () => {
            const state = editorUIReducers(initialState, actions.loadTemplateSchemas());
            expect(state.isSchemasLoading).toBe(true);
        });

        it('loadTemplateSchemasSuccess clears isSchemasLoading', () => {
            const prev = { ...initialState, isSchemasLoading: true };
            const state = editorUIReducers(prev, actions.loadTemplateSchemasSuccess({ schemas: null }));
            expect(state.isSchemasLoading).toBe(false);
        });

        it('loadTemplateSchemasFails clears isSchemasLoading', () => {
            const prev = { ...initialState, isSchemasLoading: true };
            const state = editorUIReducers(prev, actions.loadTemplateSchemasFails({ error: {} as any }));
            expect(state.isSchemasLoading).toBe(false);
        });
    });

    // ── previewSectionHovered ─────────────────────────────────────

    describe('previewSectionHovered', () => {
        it('sets hoveredSectionId', () => {
            const state = editorUIReducers(initialState, sharedActions.previewSectionHovered({ sectionId: 's1' }));
            expect(state.hoveredSectionId).toBe('s1');
        });

        it('clears hoveredSectionId with null', () => {
            const prev = { ...initialState, hoveredSectionId: 's1' };
            const state = editorUIReducers(prev, sharedActions.previewSectionHovered({ sectionId: null }));
            expect(state.hoveredSectionId).toBeNull();
        });
    });
});
