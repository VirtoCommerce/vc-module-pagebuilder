import * as selectors from './data';

// ── selectCurrentSettings ─────────────────────────────────────────

describe('selectCurrentSettings', () => {
    it('returns settings from data state', () => {
        const settings = { color: 'blue' };
        expect(selectors.selectCurrentSettings.projector({ settings, sourceSettings: null, settingsSchema: null })).toBe(settings);
    });

    it('returns null when no settings', () => {
        expect(selectors.selectCurrentSettings.projector({ settings: null, sourceSettings: null, settingsSchema: null })).toBeNull();
    });
});

// ── selectPresets ─────────────────────────────────────────────────

describe('selectPresets', () => {
    it('returns presets from sourceSettings', () => {
        const presets = { dark: {}, light: {} };
        expect(selectors.selectPresets.projector({ sourceSettings: { presets, current: 'dark' }, settings: null, settingsSchema: null })).toBe(presets);
    });

    it('returns undefined when no sourceSettings', () => {
        expect(selectors.selectPresets.projector({ sourceSettings: null, settings: null, settingsSchema: null })).toBeUndefined();
    });
});

// ── selectFilteredPresets ─────────────────────────────────────────

describe('selectFilteredPresets', () => {
    const presets = { dark: { color: 'black' }, light: { color: 'white' }, dawn: { color: 'orange' } };

    it('returns all presets when no filter', () => {
        expect(selectors.selectFilteredPresets.projector(presets, null)).toBe(presets);
    });

    it('filters by name case-insensitively', () => {
        const result = selectors.selectFilteredPresets.projector(presets, 'DA') as any;
        expect(Object.keys(result)).toEqual(['dark', 'dawn']);
    });

    it('returns empty when presets is null and no filter', () => {
        expect(selectors.selectFilteredPresets.projector(null as any, null)).toEqual([]);
    });
});

// ── selectPresetsNames ────────────────────────────────────────────

describe('selectPresetsNames', () => {
    it('returns preset keys', () => {
        expect(selectors.selectPresetsNames.projector({ dark: {}, light: {} })).toEqual(['dark', 'light']);
    });

    it('returns empty array when no presets', () => {
        expect(selectors.selectPresetsNames.projector(null as any)).toEqual([]);
    });
});

// ── selectSettingsSchema ──────────────────────────────────────────

describe('selectSettingsSchema', () => {
    it('returns schema when settings exist', () => {
        const schema = [{ name: 'Colors' }] as any;
        const data = { settingsSchema: schema, settings: { color: 'blue' }, sourceSettings: null };
        expect(selectors.selectSettingsSchema.projector(data, { color: 'blue' })).toBe(schema);
    });

    it('returns null when no current settings', () => {
        const data = { settingsSchema: [{ name: 'Colors' }] as any, settings: null, sourceSettings: null };
        expect(selectors.selectSettingsSchema.projector(data, null)).toBeNull();
    });
});

// ── selectCurrentSettingsDataModel ────────────────────────────────

describe('selectCurrentSettingsDataModel', () => {
    it('combines source and current settings', () => {
        const source = { current: 'dark' as any, presets: { dark: {} } };
        const current = { color: 'blue' };
        const result = selectors.selectCurrentSettingsDataModel.projector(source, current);
        expect(result.current).toBe(current);
        expect(result.presets).toBe(source.presets);
    });
});
