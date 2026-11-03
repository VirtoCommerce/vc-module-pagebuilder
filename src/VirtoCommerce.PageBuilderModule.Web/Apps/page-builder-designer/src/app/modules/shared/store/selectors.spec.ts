import * as selectors from './selectors';
import { TemplateEntry, TemplateEntryList } from '@shared/models';

function createEntry(overrides: Partial<TemplateEntry> = {}): TemplateEntry {
    return {
        name: 'Home',
        key: 'home',
        type: 'page',
        previewUrl: '',
        previewRule: '',
        hasChildren: false,
        ...overrides,
    } as TemplateEntry;
}

function createSharedState(overrides: any = {}) {
    return {
        appInitialized: true,
        templatesEntriesLoading: false,
        templatesEntriesLoaded: true,
        templatesEntries: {},
        entriesStates: {},
        templatesFilter: null,
        templateSelected: null,
        childrenTemplatesState: {},
        ...overrides,
    };
}

// ── selectTemplatesEntries ────────────────────────────────────────

describe('selectTemplatesEntries', () => {
    it('returns templatesEntries from state', () => {
        const entries = { home: createEntry() };
        expect(selectors.selectTemplatesEntries.projector(createSharedState({ templatesEntries: entries }))).toBe(entries);
    });
});

// ── isHttpLoading ─────────────────────────────────────────────────

describe('isHttpLoading', () => {
    it('returns true when entries are loading', () => {
        expect(selectors.isHttpLoading.projector(createSharedState({ templatesEntriesLoading: true }))).toBeTruthy();
    });

    it('returns true when app not initialized', () => {
        expect(selectors.isHttpLoading.projector(createSharedState({ appInitialized: false }))).toBeTruthy();
    });

    it('returns true when children are loading', () => {
        const state = createSharedState({
            childrenTemplatesState: { parent: { isLoading: true } },
        });
        expect(selectors.isHttpLoading.projector(state)).toBeTruthy();
    });

    it('returns falsy when nothing is loading', () => {
        expect(selectors.isHttpLoading.projector(createSharedState())).toBeFalsy();
    });
});

// ── selectTemplatesEntriesAsList ──────────────────────────────────

describe('selectTemplatesEntriesAsList', () => {
    it('filters by name case-insensitively', () => {
        const entries = [
            createEntry({ key: 'home', name: 'Home Page' }),
            createEntry({ key: 'about', name: 'About Page' }),
        ];
        const result = selectors.selectTemplatesEntriesAsList.projector(entries, 'home');
        expect(result.length).toBe(1);
        expect(result[0].key).toBe('home');
    });

    it('returns all when filter is null', () => {
        const entries = [createEntry(), createEntry({ key: 'about', name: 'About' })];
        expect(selectors.selectTemplatesEntriesAsList.projector(entries, null)).toEqual(entries);
    });
});

// ── selectTemplatesEntriesWithState ───────────────────────────────

describe('selectTemplatesEntriesWithState', () => {
    it('pairs entries with their states', () => {
        const entries = [createEntry({ key: 'home' })];
        const states = { home: { id: 'home', isDirty: true } };
        const result = selectors.selectTemplatesEntriesWithState.projector(entries, states);
        expect(result[0].entry.key).toBe('home');
        expect(result[0].state).toEqual({ id: 'home', isDirty: true });
    });

    it('uses empty object for missing state', () => {
        const entries = [createEntry({ key: 'home' })];
        const result = selectors.selectTemplatesEntriesWithState.projector(entries, {});
        expect(result[0].state).toEqual({});
    });
});

// ── selectCurrentFilter ───────────────────────────────────────────

describe('selectCurrentFilter', () => {
    it('returns filter from state', () => {
        expect(selectors.selectCurrentFilter.projector(createSharedState({ templatesFilter: 'test' }))).toBe('test');
    });

    it('returns null when no filter', () => {
        expect(selectors.selectCurrentFilter.projector(createSharedState())).toBeNull();
    });
});

// ── isAppInitialized ──────────────────────────────────────────────

describe('isAppInitialized', () => {
    it('returns appInitialized from state', () => {
        expect(selectors.isAppInitialized.projector(createSharedState({ appInitialized: true }))).toBe(true);
        expect(selectors.isAppInitialized.projector(createSharedState({ appInitialized: false }))).toBe(false);
    });
});

// ── hasDirty ──────────────────────────────────────────────────────

describe('hasDirty', () => {
    it('returns true when any state is dirty', () => {
        expect(selectors.hasDirty.projector([{ id: '1', isDirty: false }, { id: '2', isDirty: true }])).toBe(true);
    });

    it('returns false when none dirty', () => {
        expect(selectors.hasDirty.projector([{ id: '1', isDirty: false }])).toBe(false);
    });

    it('returns false for empty states', () => {
        expect(selectors.hasDirty.projector([])).toBe(false);
    });
});

// ── selectRootTemplateTitle ───────────────────────────────────────

describe('selectRootTemplateTitle', () => {
    it('returns template name', () => {
        expect(selectors.selectRootTemplateTitle.projector({ name: 'Blog' } as any)).toBe('Blog');
    });

    it('returns "Templates" when no name', () => {
        expect(selectors.selectRootTemplateTitle.projector(null)).toBe('Templates');
    });
});

// ── selectParentTemplate ──────────────────────────────────────────

describe('selectParentTemplate', () => {
    it('returns template by key', () => {
        const entries: TemplateEntryList = { home: createEntry() };
        expect(selectors.selectParentTemplate.projector(entries, 'home')).toBe(entries['home']);
    });

    it('returns null when key is null', () => {
        expect(selectors.selectParentTemplate.projector({}, null)).toBeNull();
    });
});

// ── selectPreviewUrl ──────────────────────────────────────────────

describe('selectPreviewUrl', () => {
    it('returns previewUrl from entry', () => {
        expect(selectors.selectPreviewUrl.projector({ previewUrl: '/preview' } as any)).toBe('/preview');
    });
});

// ── selectCurrentTemplatesEntries ─────────────────────────────────

describe('selectCurrentTemplatesEntries', () => {
    it('returns children templates when available', () => {
        const root: TemplateEntryList = { home: createEntry() };
        const children = { templates: { child: createEntry({ key: 'child' }) } } as any;
        expect(selectors.selectCurrentTemplatesEntries.projector(root, children)).toBe(children.templates);
    });

    it('returns root templates when children is null', () => {
        const root: TemplateEntryList = { home: createEntry() };
        expect(selectors.selectCurrentTemplatesEntries.projector(root, null)).toBe(root);
    });

    it('returns root templates when children has no templates', () => {
        const root: TemplateEntryList = { home: createEntry() };
        expect(selectors.selectCurrentTemplatesEntries.projector(root, { templates: null } as any)).toBe(root);
    });
});

// ── selectChildrenTemplatesEntriesAsList ───────────────────────────

describe('selectChildrenTemplatesEntriesAsList', () => {
    it('returns null when no entries', () => {
        expect(selectors.selectChildrenTemplatesEntriesAsList.projector(null, null)).toBeNull();
    });

    it('filters by name', () => {
        const entries = [
            createEntry({ key: 'child1', name: 'Child One' }),
            createEntry({ key: 'child2', name: 'Child Two' }),
        ];
        const result = selectors.selectChildrenTemplatesEntriesAsList.projector(entries, 'one');
        expect(result!.length).toBe(1);
        expect(result![0].key).toBe('child1');
    });

    it('returns all when no filter', () => {
        const entries = [createEntry(), createEntry({ key: 'about', name: 'About' })];
        expect(selectors.selectChildrenTemplatesEntriesAsList.projector(entries, null)).toEqual(entries);
    });
});

// ── selectTemplatesEntriesLoading ─────────────────────────────────

describe('selectTemplatesEntriesLoading', () => {
    it('returns loading state', () => {
        expect(selectors.selectTemplatesEntriesLoading.projector(createSharedState({ templatesEntriesLoading: true }))).toBe(true);
    });
});
