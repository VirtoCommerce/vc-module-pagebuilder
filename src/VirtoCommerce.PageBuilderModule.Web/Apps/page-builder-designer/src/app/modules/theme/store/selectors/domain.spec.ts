import * as selectors from './domain';

// ── selectOpenedGroups ────────────────────────────────────────────

describe('selectOpenedGroups', () => {
    it('splits comma-separated groups', () => {
        expect(selectors.selectOpenedGroups.projector('colors,fonts')).toEqual(['colors', 'fonts']);
    });

    it('returns empty array for empty string', () => {
        expect(selectors.selectOpenedGroups.projector('')).toEqual([]);
    });

    it('returns single group', () => {
        expect(selectors.selectOpenedGroups.projector('colors')).toEqual(['colors']);
    });
});

// ── selectEditableGroup ───────────────────────────────────────────

describe('selectEditableGroup', () => {
    it('finds group that is opened and not inline', () => {
        const schema = [
            { name: 'colors', inline: false },
            { name: 'fonts', inline: false },
        ] as any;
        expect(selectors.selectEditableGroup.projector(['fonts'], schema)).toEqual({ name: 'fonts', inline: false });
    });

    it('skips inline groups', () => {
        const schema = [
            { name: 'colors', inline: true },
            { name: 'fonts', inline: false },
        ] as any;
        expect(selectors.selectEditableGroup.projector(['colors'], schema)).toBeUndefined();
    });

    it('returns undefined when schema is null', () => {
        expect(selectors.selectEditableGroup.projector(['colors'], null)).toBeUndefined();
    });
});

// ── selectPresetsState ────────────────────────────────────────────

describe('selectPresetsState', () => {
    it('marks current preset', () => {
        const result = selectors.selectPresetsState.projector(['dark', 'light'], 'dark');
        expect(result['dark'].current).toBe(true);
        expect(result['light'].current).toBe(false);
    });

    it('returns empty object when no presets', () => {
        expect(selectors.selectPresetsState.projector(null as any, '')).toEqual({});
    });
});

// ── selectIsDirty ─────────────────────────────────────────────────

describe('selectIsDirty', () => {
    it('returns isDirty from domain state', () => {
        expect(selectors.selectIsDirty.projector({ isDirty: true })).toBe(true);
        expect(selectors.selectIsDirty.projector({ isDirty: false })).toBe(false);
    });
});
