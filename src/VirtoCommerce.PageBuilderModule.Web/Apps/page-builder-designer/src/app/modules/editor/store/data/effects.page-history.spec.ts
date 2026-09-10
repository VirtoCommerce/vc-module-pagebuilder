import { TestBed } from '@angular/core/testing';
import { provideMockActions } from '@ngrx/effects/testing';
import { provideMockStore, MockStore } from '@ngrx/store/testing';
import { ReplaySubject, of, firstValueFrom, throwError } from 'rxjs';
import { take, toArray } from 'rxjs/operators';
import { Action } from '@ngrx/store';

import { ModalService } from '@core/services';
import { createTemplate } from '@app/testing';

import { TemplateEditorDataEffects } from './effects';
import * as actions from '../actions';
import * as sharedActions from '@shared/store/actions';
import * as selectors from '../selectors';
import * as fromRoute from '@shared/routing';
import * as fromShared from '@shared/store/selectors';

/**
 * The version-history side of the editor effects: reading the list, continuing from a version, and
 * previewing one.
 */
describe('TemplateEditorDataEffects: page history', () => {
    let effects: TemplateEditorDataEffects;
    let actions$: ReplaySubject<Action>;
    let store: MockStore;
    let templatesService: {
        getTemplate: ReturnType<typeof vi.fn>;
        getPageHistory: ReturnType<typeof vi.fn>;
        restoreVersion: ReturnType<typeof vi.fn>;
        previewVersion: ReturnType<typeof vi.fn>;
        getTemplatePublishStatus: ReturnType<typeof vi.fn>;
    };
    let modals: { show: ReturnType<typeof vi.fn>, confirm: ReturnType<typeof vi.fn> };

    const sha = '70a3c34b5c6d7e8f90a1b2c3d4e5f60718293a4b';
    const history = { versions: [], truncated: false, otherDraftCount: 2 };

    beforeEach(() => {
        actions$ = new ReplaySubject<Action>(1);
        templatesService = {
            getTemplate: vi.fn().mockReturnValue(of(createTemplate({}))),
            getPageHistory: vi.fn().mockReturnValue(of(history)),
            restoreVersion: vi.fn().mockReturnValue(of({ branch: 'designer/john/about-us-1a2b3c4', commitSha: 'abc1234' })),
            previewVersion: vi.fn(),
            getTemplatePublishStatus: vi.fn().mockReturnValue(of({ hasChanges: true, published: true })),
        };
        modals = { show: vi.fn().mockReturnValue(of(undefined)), confirm: vi.fn().mockReturnValue(of(true)) };

        TestBed.configureTestingModule({
            providers: [
                TemplateEditorDataEffects,
                provideMockActions(() => actions$),
                provideMockStore({
                    selectors: [
                        { selector: selectors.selectRunActionContext, value: { templateKey: 'home', entry: {}, path: '/about-us.page', type: 'pages', groupId: '' } },
                        { selector: fromShared.selectCurrentTemplateEntry, value: { name: 'About', key: 'home' } },
                        { selector: fromRoute.selectTemplateKeyParameter, value: 'home' },
                        { selector: fromRoute.selectPathParameter, value: '/about-us.page' },
                        { selector: fromRoute.selectTypeParameter, value: 'pages' },
                        { selector: fromRoute.selectGroupIdParameter, value: '' },
                    ],
                }),
                { provide: ModalService, useValue: modals },
            ],
        });

        store = TestBed.inject(MockStore);
        effects = TestBed.inject(TemplateEditorDataEffects);
        (effects as any).templates = templatesService;
        (effects as any).modals = modals;
    });

    afterEach(() => store.resetSelectors());

    // ── loadPageHistory$ ──────────────────────────────────────────

    describe('loadPageHistory$', () => {
        it('asks the server for the page versions', async () => {
            actions$.next(actions.loadPageHistory({ templateKey: 'home' }));

            const result = await firstValueFrom(effects.loadPageHistory$);

            expect(templatesService.getPageHistory).toHaveBeenCalledWith('/about-us.page', 'pages', { name: 'About', key: 'home' }, '', undefined);
            expect(result).toEqual(actions.loadPageHistorySuccess({ templateKey: 'home', history: history as any }));
        });

        it('passes the branch cursor on when asked to scan further', async () => {
            actions$.next(actions.loadPageHistory({ templateKey: 'home', after: 'cursor' }));

            await firstValueFrom(effects.loadPageHistory$);

            expect(templatesService.getPageHistory).toHaveBeenCalledWith('/about-us.page', 'pages', { name: 'About', key: 'home' }, '', 'cursor');
        });

        it('stays silent for a store that keeps no history', async () => {
            // no descriptor, no versions: that is an answer, not a failure, and it must not raise an error
            templatesService.getPageHistory.mockReturnValue(of(null));
            let emitted = false;
            const subscription = effects.loadPageHistory$.subscribe(() => emitted = true);

            actions$.next(actions.loadPageHistory({ templateKey: 'home' }));

            expect(emitted).toBe(false);
            subscription.unsubscribe();
        });

        it('reports a failure instead of showing an empty list', async () => {
            templatesService.getPageHistory.mockReturnValue(throwError(() => ({ message: 'fail' })));

            actions$.next(actions.loadPageHistory({ templateKey: 'home' }));
            const result = await firstValueFrom(effects.loadPageHistory$);

            expect(result.type).toBe(actions.loadPageHistoryFails.type);
        });
    });

    // ── loadPageHistoryWithTemplate$ ──────────────────────────────

    describe('loadPageHistoryWithTemplate$', () => {
        it('loads the versions once the page is open, so the toolbar can show the count', async () => {
            actions$.next(actions.loadTemplateModelSuccess({ templateKey: 'home', template: createTemplate({}) }));

            const result = await firstValueFrom(effects.loadPageHistoryWithTemplate$);

            expect(result).toEqual(actions.loadPageHistory({ templateKey: 'home' }));
        });
    });

    // ── openPageHistory$ ──────────────────────────────────────────

    describe('openPageHistory$', () => {
        it('reloads the list and opens the panel', async () => {
            // reloaded on open: somebody may have pushed a version since the page was opened
            actions$.next(actions.executeToolbarAction({ action: 'history' }));

            const results = await firstValueFrom(effects.openPageHistory$.pipe(take(2), toArray()));

            expect(results.map(x => x.type)).toEqual([actions.loadPageHistory.type, actions.showPageHistoryPanel.type]);
        });

        it('ignores other toolbar buttons', async () => {
            let emitted = false;
            const subscription = effects.openPageHistory$.subscribe(() => emitted = true);

            actions$.next(actions.executeToolbarAction({ action: 'publish' }));

            expect(emitted).toBe(false);
            subscription.unsubscribe();
        });
    });

    // ── previewVersion$ ───────────────────────────────────────────

    describe('previewVersion$', () => {
        it('opens the storefront preview of that exact commit', async () => {
            const subscription = effects.previewVersion$.subscribe();

            actions$.next(actions.previewVersion({ sha }));

            expect(templatesService.previewVersion).toHaveBeenCalledWith('/about-us.page', 'pages', {}, '', sha);
            subscription.unsubscribe();
        });
    });

    // ── restoreVersion$ ───────────────────────────────────────────

    describe('restoreVersion$', () => {
        it('re-reads the page, refreshes the status and reloads the list', async () => {
            // the restore appended a commit to my branch: the editor has to be shown what it now holds,
            // and the restore itself has to appear in the version list
            actions$.next(actions.restoreVersion({ templateKey: 'home', sha }));

            const results = await firstValueFrom(effects.restoreVersion$.pipe(take(5), toArray()));
            const types = results.map(x => x.type);

            expect(templatesService.restoreVersion).toHaveBeenCalledWith('/about-us.page', 'pages', {}, '', sha);
            expect(types).toContain(actions.restoreVersionSuccess.type);
            expect(types).toContain(actions.reloadTemplateModel.type);
            expect(types).toContain(actions.getTemplatePublishStatus.type);
            expect(types).toContain(actions.loadPageHistory.type);
            expect(types).toContain(sharedActions.showNotification.type);
        });

        it('says so when the server refuses', async () => {
            templatesService.restoreVersion.mockReturnValue(throwError(() => ({ message: 'not a commit sha' })));

            actions$.next(actions.restoreVersion({ templateKey: 'home', sha }));
            const results = await firstValueFrom(effects.restoreVersion$.pipe(take(2), toArray()));

            expect(results[0].type).toBe(actions.restoreVersionFails.type);
            expect(results[1].type).toBe(sharedActions.showNotification.type);
        });
    });
});
