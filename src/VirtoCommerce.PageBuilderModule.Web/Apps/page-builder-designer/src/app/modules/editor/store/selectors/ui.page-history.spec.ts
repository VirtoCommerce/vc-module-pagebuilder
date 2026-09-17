import * as selectors from './ui';
import { PageVersion } from '@editor/models';

/**
 * Fixtures follow what the content repository actually answers: branches share history, saving is a
 * commit (so an afternoon of editing is a stack of them), and a repository-wide import shows up as an
 * unpublished version of every page it touched.
 */
function version(sha: string, overrides: Partial<PageVersion> = {}): PageVersion {
    return {
        sha: sha.padEnd(40, '0'),
        shortSha: sha.substring(0, 7),
        date: '2026-08-11T14:40:10Z',
        message: 'designer: save /about-us.page',
        author: { name: 'John', email: 'john@example.com' },
        branches: ['designer/john/about-us-1a2b3c4'],
        published: false,
        mine: true,
        bulk: false,
        changedFiles: 1,
        ...overrides,
    };
}

function state(versions: PageVersion[], extra: Record<string, unknown> = {}) {
    return {
        history: {
            versions,
            truncated: false,
            otherDraftCount: 0,
            isLoading: false,
            ...extra,
        },
    } as any;
}

// ── selectPageHistory ─────────────────────────────────────────────

describe('selectPageHistory', () => {
    it('returns null when the page has no history loaded', () => {
        expect(selectors.selectPageHistory.projector({} as any, false)).toBeNull();
    });

    it('folds a run of saves by the same author on the same branch into one row', () => {
        // twelve saves within three hours were measured on a single page; unfolded they bury the
        // versions somebody would want to go back to
        const versions = [
            version('aaaaaaa', { date: '2026-08-11T14:40:00Z' }),
            version('bbbbbbb', { date: '2026-08-11T14:35:00Z' }),
            version('ccccccc', { date: '2026-08-11T14:30:00Z' }),
        ];

        const result = selectors.selectPageHistory.projector(state(versions), false);

        expect(result!.groups.length).toBe(1);
        expect(result!.groups[0].version.shortSha).toBe('aaaaaaa');
        expect(result!.groups[0].older.map(x => x.shortSha)).toEqual(['bbbbbbb', 'ccccccc']);
    });

    it('keeps versions apart when more than the window separates them', () => {
        const versions = [
            version('aaaaaaa', { date: '2026-08-11T14:40:00Z' }),
            version('bbbbbbb', { date: '2026-08-11T13:00:00Z' }),
        ];

        const result = selectors.selectPageHistory.projector(state(versions), false);

        expect(result!.groups.length).toBe(2);
    });

    it('never folds a published version together with an unpublished one', () => {
        // whether a version is live is the first thing the panel says about it
        const versions = [
            version('aaaaaaa', { date: '2026-08-11T14:40:00Z', published: false }),
            version('bbbbbbb', { date: '2026-08-11T14:39:00Z', published: true }),
        ];

        const result = selectors.selectPageHistory.projector(state(versions), false);

        expect(result!.groups.length).toBe(2);
    });

    it('keeps different authors and different branches apart', () => {
        const versions = [
            version('aaaaaaa', { date: '2026-08-11T14:40:00Z' }),
            version('bbbbbbb', { date: '2026-08-11T14:39:00Z', author: { email: 'kate@example.com' } }),
            version('ccccccc', { date: '2026-08-11T14:38:00Z', branches: ['content/hero-copy'] }),
        ];

        const result = selectors.selectPageHistory.projector(state(versions), false);

        expect(result!.groups.length).toBe(3);
    });

    it('passes unsaved changes through, because a restore would discard them', () => {
        const result = selectors.selectPageHistory.projector(state([version('aaaaaaa')]), true);

        expect(result!.hasDirty).toBe(true);
    });

    it('passes truncation through so the panel can admit the list may be incomplete', () => {
        const result = selectors.selectPageHistory.projector(
            state([version('aaaaaaa')], { truncated: true, endCursor: 'cursor' }), false);

        expect(result!.truncated).toBe(true);
        expect(result!.endCursor).toBe('cursor');
    });
});

// ── the History button ────────────────────────────────────────────

describe('selectToolbarButtonsState with history', () => {
    const context = { useTheme: false, useDrafts: false, useUnpublish: false, useExternalPreview: false, useHistory: true };

    it('offers the button only when the store keeps history', () => {
        const withoutHistory = selectors.selectToolbarButtonsState({ ...context, useHistory: false })
            .projector(false, null).flat();
        expect(withoutHistory.find(button => button.alias === 'history')).toBeFalsy();

        const withHistory = selectors.selectToolbarButtonsState(context).projector(false, null).flat();
        expect(withHistory.find(button => button.alias === 'history')).toBeTruthy();
    });

    it('counts other editors unpublished versions in the title', () => {
        // the case the feature exists for: an edit made outside the builder used to be invisible
        const result = selectors.selectToolbarButtonsState(context)
            .projector(false, state([], { otherDraftCount: 2 }))
            .flat();

        expect(result.find(button => button.alias === 'history')!.title).toBe('Version history (2)');
    });

    it('says nothing about a count when there is none', () => {
        const result = selectors.selectToolbarButtonsState(context)
            .projector(false, state([], { otherDraftCount: 0 }))
            .flat();

        expect(result.find(button => button.alias === 'history')!.title).toBe('Version history');
    });
});
