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

    // ── loadPageHistorySuccess ────────────────────────────────────

    describe('loadPageHistorySuccess', () => {
        const version = (sha: string, extra: any = {}) => ({
            sha, shortSha: sha.slice(0, 7), branches: [], published: false, mine: false, bulk: false, ...extra
        });

        const at = (day: number) => `2026-08-${String(day).padStart(2, '0')}T10:00:00Z`;

        const listed = (versions: any[], otherDraftCount = 0) => ({
            ...initialState,
            states: { home: { isLoading: false, sections: {}, history: { versions, truncated: true, otherDraftCount } } as any },
        });

        it('replaces the list when the versions were loaded from the start', () => {
            const prev = listed([version('aaa')]);

            const state = editorDomainReducers(prev, actions.loadPageHistorySuccess({
                templateKey: 'home',
                history: { versions: [version('bbb')], truncated: false, otherDraftCount: 0 } as any,
            }));

            expect(state.states['home'].history!.versions.map(x => x.sha)).toEqual(['bbb']);
        });

        it('keeps the versions already listed when more branches are scanned', () => {
            // each scan answers with its own page of branches, so taking it as the whole list would drop
            // the drafts the previous page found — the opposite of what asking for more branches means
            const prev = listed([version('aaa'), version('bbb')]);

            const state = editorDomainReducers(prev, actions.loadPageHistorySuccess({
                templateKey: 'home',
                after: 'cursor',
                history: { versions: [version('ccc')], truncated: false, otherDraftCount: 1 } as any,
            }));

            expect(state.states['home'].history!.versions.map(x => x.sha)).toEqual(['aaa', 'bbb', 'ccc']);
        });

        it('lists a version reachable from two branches once', () => {
            const prev = listed([version('aaa')]);

            const state = editorDomainReducers(prev, actions.loadPageHistorySuccess({
                templateKey: 'home',
                after: 'cursor',
                history: { versions: [version('aaa'), version('ccc')], truncated: false, otherDraftCount: 0 } as any,
            }));

            expect(state.states['home'].history!.versions.map(x => x.sha)).toEqual(['aaa', 'ccc']);
        });

        it('recounts the other-drafts badge over the whole list', () => {
            // the server counts it per answer, and after a merge that number describes a shorter list
            const prev = listed([version('aaa'), version('bbb')], 2);

            const state = editorDomainReducers(prev, actions.loadPageHistorySuccess({
                templateKey: 'home',
                after: 'cursor',
                history: {
                    versions: [version('ccc'), version('ddd', { published: true }), version('eee', { mine: true })],
                    truncated: false,
                    otherDraftCount: 1,
                } as any,
            }));

            expect(state.states['home'].history!.otherDraftCount).toBe(3);
        });

        it('files a newly scanned draft above the published history, not at the end', () => {
            // a draft found by scanning further is the reason the button was pressed; appending it would
            // put it below versions that are already live
            const prev = listed([
                version('aaa', { date: at(20) }),
                version('old', { date: at(19), published: true }),
            ]);

            const state = editorDomainReducers(prev, actions.loadPageHistorySuccess({
                templateKey: 'home',
                after: 'cursor',
                history: { versions: [version('new', { date: at(18) })], truncated: false, otherDraftCount: 1 } as any,
            }));

            expect(state.states['home'].history!.versions.map(x => x.sha)).toEqual(['aaa', 'new', 'old']);
        });

        it('orders each half newest first', () => {
            const prev = listed([version('aaa', { date: at(18) })]);

            const state = editorDomainReducers(prev, actions.loadPageHistorySuccess({
                templateKey: 'home',
                after: 'cursor',
                history: { versions: [version('ccc', { date: at(21) })], truncated: false, otherDraftCount: 1 } as any,
            }));

            expect(state.states['home'].history!.versions.map(x => x.sha)).toEqual(['ccc', 'aaa']);
        });

        it('takes truncated from the newest answer', () => {
            const prev = listed([version('aaa')]);

            const state = editorDomainReducers(prev, actions.loadPageHistorySuccess({
                templateKey: 'home',
                after: 'cursor',
                history: { versions: [version('ccc')], truncated: false, otherDraftCount: 0 } as any,
            }));

            expect(state.states['home'].history!.truncated).toBe(false);
        });
    });
});
