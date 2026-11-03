import * as selectors from './ui';

// ── selectGroupsState ─────────────────────────────────────────────

describe('selectGroupsState', () => {
    it('marks opened groups', () => {
        const schema = [
            { name: 'colors', inline: true },
            { name: 'fonts', inline: true },
        ] as any;
        const result = selectors.selectGroupsState.projector(schema, ['colors']);
        expect(result['colors'].opened).toBe(true);
        expect(result['fonts'].opened).toBe(false);
    });

    it('does not open non-inline groups', () => {
        const schema = [{ name: 'colors', inline: false }] as any;
        const result = selectors.selectGroupsState.projector(schema, ['colors']);
        expect(result['colors'].opened).toBe(false);
    });

    it('returns undefined when schema is null', () => {
        expect(selectors.selectGroupsState.projector(null, [])).toBeUndefined();
    });
});

// ── isLoading ─────────────────────────────────────────────────────

describe('isLoading', () => {
    it('returns true when settings is loading', () => {
        expect(selectors.isLoading.projector({ settingsLoading: true, schemaLoading: false } as any)).toBe(true);
    });

    it('returns true when schema is loading', () => {
        expect(selectors.isLoading.projector({ settingsLoading: false, schemaLoading: true } as any)).toBe(true);
    });

    it('returns false when nothing loading', () => {
        expect(selectors.isLoading.projector({ settingsLoading: false, schemaLoading: false } as any)).toBe(false);
    });
});

// ── selectPresetsContext ──────────────────────────────────────────

describe('selectPresetsContext', () => {
    it('combines presets and state', () => {
        const presets = { dark: { color: 'black' } };
        const state = { dark: { current: true } };
        const result = selectors.selectPresetsContext.projector(presets, state);
        expect(result).toEqual({ presets, state });
    });
});

// ── selectToolbarButtonsState ─────────────────────────────────────

describe('selectToolbarButtonsState', () => {
    it('includes Cancel and Save buttons', () => {
        const result = selectors.selectToolbarButtonsState.projector(false);
        const allButtons = result.flat();
        expect(allButtons.find(b => b.alias === 'cancel')).toBeTruthy();
        expect(allButtons.find(b => b.alias === 'save')).toBeTruthy();
    });

    it('enables save when dirty', () => {
        const result = selectors.selectToolbarButtonsState.projector(true);
        const saveBtn = result.flat().find(b => b.alias === 'save');
        expect(saveBtn!.canAction).toBe(true);
    });

    it('disables save when not dirty', () => {
        const result = selectors.selectToolbarButtonsState.projector(false);
        const saveBtn = result.flat().find(b => b.alias === 'save');
        expect(saveBtn!.canAction).toBe(false);
    });
});
