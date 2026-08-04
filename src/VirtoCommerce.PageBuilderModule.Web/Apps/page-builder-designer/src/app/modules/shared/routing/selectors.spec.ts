import * as selectors from './selectors';

// ── selectQueryParams ─────────────────────────────────────────────

describe('selectQueryParams', () => {
    it('returns query params from router state', () => {
        const route = { state: { queryParams: { type: 'page' } } } as any;
        expect(selectors.selectQueryParams.projector(route)).toEqual({ type: 'page' });
    });
});

describe('route query selectors', () => {
    it('returns cultureName and sharedComponentId query parameters', () => {
        const queryParams = { cultureName: 'en-US', sharedComponentId: 'component-1' };

        expect(selectors.selectCultureNameParameter.projector(queryParams)).toBe('en-US');
        expect(selectors.selectSharedComponentIdParameter.projector(queryParams)).toBe('component-1');
    });

    it('returns empty values when the query parameters are missing', () => {
        expect(selectors.selectCultureNameParameter.projector({})).toBe('');
        expect(selectors.selectSharedComponentIdParameter.projector({})).toBe('');
    });
});

// ── selectPathParams ──────────────────────────────────────────────

describe('selectPathParams', () => {
    it('returns path params from router state', () => {
        const route = { state: { params: { sectionId: 's1' } } } as any;
        expect(selectors.selectPathParams.projector(route)).toEqual({ sectionId: 's1' });
    });
});

// ── selectDataParams ──────────────────────────────────────────────

describe('selectDataParams', () => {
    it('returns data from route', () => {
        const route = { state: { data: { mode: 'edit' } } } as any;
        expect(selectors.selectDataParams.projector(route)).toEqual({ mode: 'edit' });
    });

    it('returns empty object when no data', () => {
        expect(selectors.selectDataParams.projector({ state: {} } as any)).toEqual({});
    });
});

// ── selectTemplateKeyParameter ────────────────────────────────────

describe('selectTemplateKeyParameter', () => {
    it('uses the Shared Component id before page route parameters', () => {
        expect(selectors.selectTemplateKeyParameter.projector('page', '/home.json', 'g1', 'component-1'))
            .toBe('shared-component::component-1');
    });

    it('returns type::path when type is set', () => {
        expect(selectors.selectTemplateKeyParameter.projector('page', '/home.json', '', '')).toBe('page::/home.json');
    });

    it('returns type::groupId when type and groupId are set', () => {
        expect(selectors.selectTemplateKeyParameter.projector('page', '/home.json', 'g1', '')).toBe('page::g1');
    });

    it('returns groupId when only groupId is set', () => {
        expect(selectors.selectTemplateKeyParameter.projector('', '', 'g1', '')).toBe('g1');
    });

    it('returns path when only path is set', () => {
        expect(selectors.selectTemplateKeyParameter.projector('', '/home.json', '', '')).toBe('/home.json');
    });
});

// ── selectSectionIdParameter ──────────────────────────────────────

describe('selectSectionIdParameter', () => {
    it('returns sectionId from params', () => {
        expect(selectors.selectSectionIdParameter.projector({ sectionId: 's1' })).toBe('s1');
    });

    it('returns empty string when missing', () => {
        expect(selectors.selectSectionIdParameter.projector({})).toBe('');
    });
});

// ── selectBlockIdParameter ────────────────────────────────────────

describe('selectBlockIdParameter', () => {
    it('returns blockId from params', () => {
        expect(selectors.selectBlockIdParameter.projector({ blockId: 'b1' })).toBe('b1');
    });

    it('returns empty string when missing', () => {
        expect(selectors.selectBlockIdParameter.projector({})).toBe('');
    });
});

// ── selectInsertIndexParameter ────────────────────────────────────

describe('selectInsertIndexParameter', () => {
    it('parses insertIndex', () => {
        expect(selectors.selectInsertIndexParameter.projector({ insertIndex: '3' })).toBe(3);
    });

    it('returns -1 for missing insertIndex', () => {
        expect(selectors.selectInsertIndexParameter.projector({})).toBe(-1);
    });

    it('returns -1 for non-numeric value', () => {
        expect(selectors.selectInsertIndexParameter.projector({ insertIndex: 'abc' })).toBe(-1);
    });
});

// ── selectSettingsTypeParameter ───────────────────────────────────

describe('selectSettingsTypeParameter', () => {
    it('returns settingsType when mode is edit-settings', () => {
        expect(selectors.selectSettingsTypeParameter.projector({ settingsType: 'hero' }, 'edit-settings')).toBe('hero');
    });

    it('returns empty string when settingsType missing and mode is edit-settings', () => {
        expect(selectors.selectSettingsTypeParameter.projector({}, 'edit-settings')).toBe('');
    });

    it('returns null when mode is not edit-settings', () => {
        expect(selectors.selectSettingsTypeParameter.projector({ settingsType: 'hero' }, 'edit')).toBeNull();
    });

    it('returns null when mode is null', () => {
        expect(selectors.selectSettingsTypeParameter.projector({ settingsType: 'hero' }, null)).toBeNull();
    });
});

// ── selectPreviewModeParameter ────────────────────────────────────

describe('selectPreviewModeParameter', () => {
    it('returns preview-mode param', () => {
        expect(selectors.selectPreviewModeParameter.projector({ 'preview-mode': 'fullscreen' })).toBe('fullscreen');
    });

    it('returns empty string when missing', () => {
        expect(selectors.selectPreviewModeParameter.projector({})).toBe('');
    });
});

// ── isFullscreenPreviewMode ───────────────────────────────────────

describe('isFullscreenPreviewMode', () => {
    it('returns true for fullscreen', () => {
        expect(selectors.isFullscreenPreviewMode.projector('fullscreen')).toBe(true);
    });

    it('returns false for other values', () => {
        expect(selectors.isFullscreenPreviewMode.projector('desktop-50')).toBe(false);
        expect(selectors.isFullscreenPreviewMode.projector('')).toBe(false);
    });
});

// ── isDesktop50 ───────────────────────────────────────────────────

describe('isDesktop50', () => {
    it('returns true for desktop-50', () => {
        expect(selectors.isDesktop50.projector('desktop-50')).toBe(true);
    });

    it('returns false for other values', () => {
        expect(selectors.isDesktop50.projector('fullscreen')).toBe(false);
    });
});

// ── isPresetPreviewMode ───────────────────────────────────────────

describe('isPresetPreviewMode', () => {
    it('returns true when preset is set', () => {
        expect(selectors.isPresetPreviewMode.projector('dark')).toBe(true);
    });

    it('returns false when preset is empty', () => {
        expect(selectors.isPresetPreviewMode.projector('')).toBe(false);
    });
});

// ── isEmpty ───────────────────────────────────────────────────────

describe('isEmpty', () => {
    it('returns isEmpty from state', () => {
        expect(selectors.isEmpty.projector({ state: { isEmpty: true } } as any)).toBe(true);
        expect(selectors.isEmpty.projector({ state: { isEmpty: false } } as any)).toBe(false);
    });
});

// ── selectPath ────────────────────────────────────────────────────

describe('selectPath', () => {
    it('strips query string from url', () => {
        expect(selectors.selectPath.projector('/pages/edit?type=page')).toBe('/pages/edit');
    });

    it('returns url without query as-is', () => {
        expect(selectors.selectPath.projector('/pages/edit')).toBe('/pages/edit');
    });
});

// ── getModeName ───────────────────────────────────────────────────

describe('getModeName', () => {
    it('returns mode from data', () => {
        expect(selectors.getModeName.projector({ mode: 'edit-settings' })).toBe('edit-settings');
    });

    it('returns null when no mode', () => {
        expect(selectors.getModeName.projector({})).toBeNull();
    });
});
