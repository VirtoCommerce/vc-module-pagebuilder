import { themeDataReducers } from './reducers';
import { initialState } from './state';
import * as actions from '../actions';
import { SettingsDataModel } from '@theme/models';

function createSettingsData(overrides: Partial<SettingsDataModel> = {}): SettingsDataModel {
    return {
        current: { color: 'blue', fontSize: 14 },
        presets: {
            dark: { color: 'black', fontSize: 16 },
            light: { color: 'white', fontSize: 12 },
        },
        ...overrides,
    };
}

describe('themeDataReducers', () => {
    it('returns initial state for unknown action', () => {
        const state = themeDataReducers(undefined, { type: '@@INIT' });
        expect(state).toEqual(initialState);
    });

    // ── loadSettingsDataSuccess ────────────────────────────────────

    describe('loadSettingsDataSuccess', () => {
        it('stores settings when current is an object', () => {
            const data = createSettingsData();
            const state = themeDataReducers(initialState, actions.loadSettingsDataSuccess({ settingsData: data }));

            expect(state.settings).toEqual({ color: 'blue', fontSize: 14 });
            expect(state.sourceSettings).toBe(data);
        });

        it('resolves preset when current is a string', () => {
            const data = createSettingsData({ current: 'dark' });
            const state = themeDataReducers(initialState, actions.loadSettingsDataSuccess({ settingsData: data }));

            expect(state.settings).toEqual({ color: 'black', fontSize: 16 });
            expect(state.sourceSettings).toBe(data);
        });

        it('deep clones settings to prevent mutations', () => {
            const data = createSettingsData();
            const state = themeDataReducers(initialState, actions.loadSettingsDataSuccess({ settingsData: data }));

            expect(state.settings).not.toBe(data.current);
            expect(state.settings).toEqual(data.current);
        });

        it('handles null settingsData', () => {
            const state = themeDataReducers(initialState, actions.loadSettingsDataSuccess({ settingsData: null }));

            expect(state.settings).toBeNull();
            expect(state.sourceSettings).toBeNull();
        });
    });

    // ── useSettingsSchema ─────────────────────────────────────────

    describe('useSettingsSchema', () => {
        it('stores schema', () => {
            const schema = { settings: [] } as any;
            const state = themeDataReducers(initialState, actions.useSettingsSchema({ schema }));
            expect(state.settingsSchema).toBe(schema);
        });

        it('sets schema to null', () => {
            const prev = { ...initialState, settingsSchema: { settings: [] } as any };
            const state = themeDataReducers(prev, actions.useSettingsSchema({ schema: null }));
            expect(state.settingsSchema).toBeNull();
        });
    });

    // ── applyPreset ───────────────────────────────────────────────

    describe('applyPreset', () => {
        it('applies preset from sourceSettings', () => {
            const prev = {
                ...initialState,
                settings: { color: 'blue' },
                sourceSettings: createSettingsData(),
            };
            const state = themeDataReducers(prev, actions.applyPreset({ preset: 'dark' }));

            expect(state.settings).toEqual({ color: 'black', fontSize: 16 });
        });

        it('creates a new settings object (not same reference)', () => {
            const data = createSettingsData();
            const prev = { ...initialState, settings: {}, sourceSettings: data };
            const state = themeDataReducers(prev, actions.applyPreset({ preset: 'dark' }));

            expect(state.settings).not.toBe(data.presets['dark']);
        });
    });

    // ── updateSettings ────────────────────────────────────────────

    describe('updateSettings', () => {
        it('merges model into settings', () => {
            const prev = { ...initialState, settings: { color: 'blue', fontSize: 14 } };
            const state = themeDataReducers(prev, actions.updateSettings({ model: { color: 'red' } as any } as any));

            expect(state.settings).toEqual({ color: 'red', fontSize: 14 });
        });
    });

    // ── revertChanges ─────────────────────────────────────────────

    describe('revertChanges', () => {
        it('reverts to original settings (object current)', () => {
            const data = createSettingsData();
            const prev = {
                ...initialState,
                settings: { color: 'modified' },
                sourceSettings: data,
            };
            const state = themeDataReducers(prev, actions.revertChanges());

            expect(state.settings).toEqual({ color: 'blue', fontSize: 14 });
        });

        it('reverts to preset when current is string', () => {
            const data = createSettingsData({ current: 'light' });
            const prev = {
                ...initialState,
                settings: { color: 'modified' },
                sourceSettings: data,
            };
            const state = themeDataReducers(prev, actions.revertChanges());

            expect(state.settings).toEqual({ color: 'white', fontSize: 12 });
        });

        it('deep clones on revert', () => {
            const data = createSettingsData();
            const prev = { ...initialState, settings: { color: 'modified' }, sourceSettings: data };
            const state = themeDataReducers(prev, actions.revertChanges());

            expect(state.settings).not.toBe(data.current);
        });
    });

    // ── applyChanges ──────────────────────────────────────────────

    describe('applyChanges', () => {
        it('makes current settings the new source', () => {
            const data = createSettingsData();
            const currentSettings = { color: 'green', fontSize: 20 };
            const prev = {
                ...initialState,
                settings: currentSettings,
                sourceSettings: data,
            };
            const state = themeDataReducers(prev, actions.applyChanges());

            expect(state.sourceSettings!.current).toBe(currentSettings);
            expect(state.sourceSettings!.presets).toBe(data.presets);
        });
    });
});
