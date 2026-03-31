import * as selectors from './domain';

// ── isSchemasLoaded ───────────────────────────────────────────────

describe('isSchemasLoaded', () => {
    it('returns true when schemas exist', () => {
        expect(selectors.isSchemasLoaded.projector({ schemas: { sections: {} }, templates: {} } as any)).toBe(true);
    });

    it('returns false when schemas is null', () => {
        expect(selectors.isSchemasLoaded.projector({ schemas: null, templates: {} })).toBe(false);
    });
});

// ── selectCurrentTemplateState ────────────────────────────────────

describe('selectCurrentTemplateState', () => {
    it('returns state for template key', () => {
        const domainState = { states: { home: { isLoading: false, sections: {} } }, schemaLoading: false } as any;
        const result = selectors.selectCurrentTemplateState.projector(domainState, 'home');
        expect(result).toEqual({ isLoading: false, sections: {}, key: 'home' });
    });

    it('returns null when templateKey is empty', () => {
        const domainState = { states: {}, schemaLoading: false };
        expect(selectors.selectCurrentTemplateState.projector(domainState, '')).toBeNull();
    });

    it('returns object with key even for missing template', () => {
        const domainState = { states: {}, schemaLoading: false };
        const result = selectors.selectCurrentTemplateState.projector(domainState, 'missing');
        expect(result).toEqual({ key: 'missing' });
    });
});
