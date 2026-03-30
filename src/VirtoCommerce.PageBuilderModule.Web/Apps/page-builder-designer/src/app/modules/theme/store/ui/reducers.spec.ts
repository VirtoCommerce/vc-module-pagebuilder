import { themeUIReducers } from './reducers';
import { initialState } from './state';
import * as actions from '../actions';

describe('themeUIReducers', () => {
    it('returns initial state for unknown action', () => {
        const state = themeUIReducers(undefined, { type: '@@INIT' });
        expect(state).toEqual(initialState);
    });

    // ── display mode ──────────────────────────────────────────────

    describe('presetsListMode', () => {
        it('sets mode to list', () => {
            const prev = { ...initialState, mode: 'tile' as const };
            const state = themeUIReducers(prev, actions.presetsListMode());
            expect(state.mode).toBe('list');
        });
    });

    describe('presetsTileMode', () => {
        it('sets mode to tile', () => {
            const state = themeUIReducers(initialState, actions.presetsTileMode());
            expect(state.mode).toBe('tile');
        });
    });

    // ── settings loading ──────────────────────────────────────────

    describe('settings loading', () => {
        it('loadSettingsData sets settingsLoading', () => {
            const state = themeUIReducers(initialState, actions.loadSettingsData());
            expect(state.settingsLoading).toBe(true);
        });

        it('loadSettingsDataSuccess clears settingsLoading', () => {
            const prev = { ...initialState, settingsLoading: true };
            const state = themeUIReducers(prev, actions.loadSettingsDataSuccess({ settingsData: null }));
            expect(state.settingsLoading).toBe(false);
        });

        it('loadSettingsDataFail clears settingsLoading', () => {
            const prev = { ...initialState, settingsLoading: true };
            const state = themeUIReducers(prev, actions.loadSettingsDataFail({ error: {} as any }));
            expect(state.settingsLoading).toBe(false);
        });
    });

    // ── schema loading ────────────────────────────────────────────

    describe('schema loading', () => {
        it('loadSettingsSchema sets schemaLoading', () => {
            const state = themeUIReducers(initialState, actions.loadSettingsSchema());
            expect(state.schemaLoading).toBe(true);
        });

        it('loadSettingsSchemaSuccess clears schemaLoading', () => {
            const prev = { ...initialState, schemaLoading: true };
            const state = themeUIReducers(prev, actions.loadSettingsSchemaSuccess({ schema: null }));
            expect(state.schemaLoading).toBe(false);
        });

        it('loadSettingsSchemaFail clears schemaLoading', () => {
            const prev = { ...initialState, schemaLoading: true };
            const state = themeUIReducers(prev, actions.loadSettingsSchemaFail({ error: {} as any }));
            expect(state.schemaLoading).toBe(false);
        });
    });

    // ── save loading ──────────────────────────────────────────────

    describe('save loading', () => {
        it('saveSettings sets schemaLoading', () => {
            const state = themeUIReducers(initialState, actions.saveSettings());
            expect(state.schemaLoading).toBe(true);
        });

        it('saveSettingsSuccess clears schemaLoading', () => {
            const prev = { ...initialState, schemaLoading: true };
            const state = themeUIReducers(prev, actions.saveSettingsSuccess());
            expect(state.schemaLoading).toBe(false);
        });

        it('saveSettingsFail clears schemaLoading', () => {
            const prev = { ...initialState, schemaLoading: true };
            const state = themeUIReducers(prev, actions.saveSettingsFail({ error: {} as any }));
            expect(state.schemaLoading).toBe(false);
        });
    });

    // ── presets filter ────────────────────────────────────────────

    describe('applyPresetsFilter', () => {
        it('sets presetsFilter', () => {
            const state = themeUIReducers(initialState, actions.applyPresetsFilter({ filter: 'dark' }));
            expect(state.presetsFilter).toBe('dark');
        });
    });
});
