import { TestBed } from '@angular/core/testing';
import { provideMockActions } from '@ngrx/effects/testing';
import { provideMockStore, MockStore } from '@ngrx/store/testing';
import { ReplaySubject, Subject, of, firstValueFrom, throwError } from 'rxjs';
import { take, toArray } from 'rxjs/operators';
import { Action } from '@ngrx/store';

import { SharedEffects } from './effects';
import * as actions from './actions';
import * as fromRoute from '@shared/routing';
import * as fromState from '@shared/store/selectors';
import { EventsBusService, NotificationsService } from '@core/services';
import { PreviewBridgeService } from '@shared/services';
import type { PreviewInboundMessage } from '@shared/models';

describe('SharedEffects', () => {
    let effects: SharedEffects;
    let actions$: ReplaySubject<Action>;
    let store: MockStore;
    let eventsBus: { emit: ReturnType<typeof vi.fn> };
    let notification: { show: ReturnType<typeof vi.fn> };
    let templatesService: {
        getTemplatesList: ReturnType<typeof vi.fn>;
        getChildrenTemplates: ReturnType<typeof vi.fn>;
    };
    let metaDataService: { setTitle: ReturnType<typeof vi.fn> };
    let previewMessages$: Subject<PreviewInboundMessage>;

    beforeEach(() => {
        actions$ = new ReplaySubject<Action>(1);
        eventsBus = { emit: vi.fn() };
        notification = { show: vi.fn() };
        templatesService = {
            getTemplatesList: vi.fn().mockReturnValue(of({})),
            getChildrenTemplates: vi.fn().mockReturnValue(of({})),
        };
        metaDataService = { setTitle: vi.fn() };
        previewMessages$ = new Subject<PreviewInboundMessage>();

        TestBed.configureTestingModule({
            providers: [
                SharedEffects,
                provideMockActions(() => actions$),
                provideMockStore({
                    selectors: [
                        { selector: fromState.isAppInitialized, value: false },
                        { selector: fromState.selectTemplatesEntries, value: {} },
                        { selector: fromState.selectTemplatesEntriesAsList, value: [] },
                        { selector: fromState.selectCurrentTemplateEntry, value: { name: 'Home', key: 'home', previewUrl: '/preview' } },
                        { selector: fromState.selectParentTemplate, value: null },
                        { selector: fromState.selectParentTemplateKey, value: null },
                        { selector: fromState.selectCurrentFilter, value: null },
                        { selector: fromState.selectCurrentTemplatesEntries, value: {} },
                        { selector: fromRoute.selectTemplateKeyParameter, value: 'home' },
                        { selector: fromRoute.selectTypeParameter, value: 'page' },
                        { selector: fromRoute.selectPathParameter, value: '/home.json' },
                        { selector: fromRoute.selectParentTemplateParameter, value: '' },
                        { selector: fromRoute.isEmpty, value: false },
                    ],
                }),
                { provide: EventsBusService, useValue: eventsBus },
                { provide: NotificationsService, useValue: notification },
                { provide: PreviewBridgeService, useValue: { messages$: previewMessages$ } },
                { provide: 'MetaDataService', useValue: metaDataService },
            ],
        });

        store = TestBed.inject(MockStore);
        effects = TestBed.inject(SharedEffects);
        // Override injected services
        (effects as any).templatesService = templatesService;
        (effects as any).metaDataService = metaDataService;
    });

    afterEach(() => store.resetSelectors());

    // ── initShared$ ───────────────────────────────────────────────

    describe('initShared$', () => {
        it('dispatches loadTemplateEntries', async () => {
            actions$.next(actions.initShared());
            const results = await firstValueFrom(effects.initShared$.pipe(take(1), toArray()));
            expect(results[0].type).toBe(actions.loadTemplateEntries.type);
        });
    });

    // ── loadTemplateEntries$ ──────────────────────────────────────

    describe('loadTemplateEntries$', () => {
        it('dispatches success on load', async () => {
            const entries = { home: { name: 'Home' } };
            templatesService.getTemplatesList.mockReturnValue(of(entries));

            actions$.next(actions.loadTemplateEntries());
            const result = await firstValueFrom(effects.loadTemplateEntries$);
            expect(result.type).toBe(actions.loadTemplateEntriesSuccess.type);
        });

        it('dispatches fail on error', async () => {
            templatesService.getTemplatesList.mockReturnValue(throwError(() => ({ message: 'fail' })));

            actions$.next(actions.loadTemplateEntries());
            const result = await firstValueFrom(effects.loadTemplateEntries$);
            expect(result.type).toBe(actions.loadTemplateEntriesFails.type);
        });
    });

    // ── mergeServerTemplateEntries$ ───────────────────────────────

    describe('mergeServerTemplateEntries$', () => {
        it('dispatches useTemplateEntries with merged data', async () => {
            actions$.next(actions.loadTemplateEntriesSuccess({ templatesEntries: { new: { name: 'New' } } as any }));
            const result = await firstValueFrom(effects.mergeServerTemplateEntries$);
            expect(result.type).toBe(actions.useTemplateEntries.type);
        });
    });

    // ── changePreviewMode$ ────────────────────────────────────────

    describe('changePreviewMode$', () => {
        it('navigates with preview-mode query param', async () => {
            actions$.next(actions.changePreviewMode({ mode: 'fullscreen' }));
            const result = await firstValueFrom(effects.changePreviewMode$);
            expect(result.type).toBe('[router] router go');
            expect((result as any).queryParams).toEqual({ 'preview-mode': 'fullscreen' });
        });
    });

    // ── broadcastPreviewMessage$ ──────────────────────────────────

    describe('broadcastPreviewMessage$', () => {
        it('emits to eventsBus', () => {
            actions$.next(actions.broadcastPreviewMessage({ msg: { type: 'test' } }));
            effects.broadcastPreviewMessage$.subscribe();
            expect(eventsBus.emit).toHaveBeenCalledWith({ target: 'preview', payload: { type: 'test' } });
        });
    });

    // ── broadcastPlatformMessage$ ─────────────────────────────────

    describe('broadcastPlatformMessage$', () => {
        it('emits to eventsBus with platform target', () => {
            actions$.next(actions.broadcastPlatformMessage({ msg: { type: 'save' } }));
            effects.broadcastPlatformMessage$.subscribe();
            expect(eventsBus.emit).toHaveBeenCalledWith({ target: 'platform', payload: { type: 'save' } });
        });
    });

    // ── inbound preview messages ─────────────────────────────────

    describe('inbound preview messages', () => {
        it('dispatches selectSection with a string section id', async () => {
            const result = firstValueFrom(effects.selectSectionMessage$);

            previewMessages$.next({
                source: 'preview',
                type: 'select',
                data: { sectionId: 'placement-1' },
            });

            await expect(result).resolves.toEqual(actions.selectSection({ sectionId: 'placement-1' }));
        });

        it('clears preview hover when the iframe emits hover leave', async () => {
            const result = firstValueFrom(effects.hoverSectionMessage$);

            previewMessages$.next({
                source: 'preview',
                type: 'hover',
                data: { sectionId: null },
            });

            await expect(result).resolves.toEqual(actions.previewSectionHovered({ sectionId: null }));
        });
    });

    // ── showNotification$ ─────────────────────────────────────────

    describe('showNotification$', () => {
        it('calls notification service', () => {
            actions$.next(actions.showNotification({ message: 'Saved', msgType: 'success', top: true }));
            effects.showNotification$.subscribe();
            expect(notification.show).toHaveBeenCalledWith('Saved', 'success', 'tr');
        });

        it('uses bottom-left for non-top', () => {
            actions$.next(actions.showNotification({ message: 'Info', msgType: 'info' }));
            effects.showNotification$.subscribe();
            expect(notification.show).toHaveBeenCalledWith('Info', 'info', 'bl');
        });
    });

    // ── previewLoaded$ ────────────────────────────────────────────

    describe('previewLoaded$', () => {
        it('emits preview-loaded and dispatches setLivePreviewUrl', async () => {
            actions$.next(actions.previewLoaded());
            const result = await firstValueFrom(effects.previewLoaded$);
            expect(eventsBus.emit).toHaveBeenCalledWith({ target: 'preview', payload: { type: 'preview-loaded' } });
            expect(result.type).toBe(actions.setLivePreviewUrl.type);
        });
    });

    // ── setWindowTitle$ ───────────────────────────────────────────

    describe('setWindowTitle$', () => {
        it('calls metaDataService.setTitle', () => {
            actions$.next(actions.setWindowTitle({ title: 'My Page' }));
            effects.setWindowTitle$.subscribe();
            expect(metaDataService.setTitle).toHaveBeenCalledWith('My Page');
        });
    });

    // ── switchToChildrenTemplates$ ────────────────────────────────

    describe('switchToChildrenTemplates$', () => {
        it('dispatches loadChildrenTemplates', async () => {
            actions$.next(actions.switchToChildrenTemplates({ templateKey: 'parent' }));
            const result = await firstValueFrom(effects.switchToChildrenTemplates$);
            expect(result.type).toBe(actions.loadChildrenTemplates.type);
            expect((result as any).templateKey).toBe('parent');
        });
    });

    // ── raiseInitModule$ ──────────────────────────────────────────

    describe('raiseInitModule$', () => {
        it('dispatches initShared when app not initialized', async () => {
            actions$.next({ type: '@ngrx/router-store/navigated' } as any);
            const results = await firstValueFrom(effects.raiseInitModule$.pipe(take(1), toArray()));
            expect(results[0].type).toBe(actions.initShared.type);
        });

        it('does not dispatch when already initialized', async () => {
            store.overrideSelector(fromState.isAppInitialized, true);
            store.refreshState();

            actions$.next({ type: '@ngrx/router-store/navigated' } as any);
            const results: Action[] = [];
            const sub = effects.raiseInitModule$.subscribe(a => results.push(a));
            await new Promise(r => setTimeout(r, 50));
            sub.unsubscribe();
            expect(results.length).toBe(0);
        });
    });

    // ── selectDefaultTemplate$ ────────────────────────────────────

    describe('selectDefaultTemplate$', () => {
        it('dispatches selectDefaultTemplate when no template key', async () => {
            store.overrideSelector(fromRoute.selectTemplateKeyParameter, '');
            store.refreshState();

            actions$.next({ type: '@ngrx/router-store/navigated' } as any);
            const results = await firstValueFrom(effects.selectDefaultTemplate$.pipe(take(1), toArray()));
            expect(results[0].type).toBe(actions.selectDefaultTemplate.type);
        });

        it('does not dispatch when template key exists', async () => {
            actions$.next({ type: '@ngrx/router-store/navigated' } as any);
            const results: Action[] = [];
            const sub = effects.selectDefaultTemplate$.subscribe(a => results.push(a));
            await new Promise(r => setTimeout(r, 50));
            sub.unsubscribe();
            expect(results.length).toBe(0);
        });
    });

    // ── raiseInitApp$ ─────────────────────────────────────────────

    describe('raiseInitApp$', () => {
        it('dispatches initApp and setLivePreviewUrl when no parent', async () => {
            actions$.next(actions.loadTemplateEntriesSuccess({ templatesEntries: {} }));
            const results = await firstValueFrom(effects.raiseInitApp$.pipe(take(2), toArray()));
            const types = results.map(r => r.type);
            expect(types).toContain(actions.initApp.type);
            expect(types).toContain(actions.setLivePreviewUrl.type);
        });

        it('does not dispatch when parent exists', async () => {
            store.overrideSelector(fromRoute.selectParentTemplateParameter, 'parent-key');
            store.refreshState();

            actions$.next(actions.loadTemplateEntriesSuccess({ templatesEntries: {} }));
            const results: Action[] = [];
            const sub = effects.raiseInitApp$.subscribe(a => results.push(a));
            await new Promise(r => setTimeout(r, 50));
            sub.unsubscribe();
            expect(results.length).toBe(0);
        });
    });

    // ── loadChildrenOnStartApp$ ───────────────────────────────────

    describe('loadChildrenOnStartApp$', () => {
        it('dispatches raiseLoadChildrenTemplates when parent exists', async () => {
            store.overrideSelector(fromRoute.selectParentTemplateParameter, 'parent-key');
            store.refreshState();

            actions$.next(actions.loadTemplateEntriesSuccess({ templatesEntries: {} }));
            const results = await firstValueFrom(effects.loadChildrenOnStartApp$.pipe(take(1), toArray()));
            expect(results[0].type).toBe(actions.raiseLoadChildrenTemplates.type);
        });
    });

    // ── loadChildrenTemplates$ ────────────────────────────────────

    describe('loadChildrenTemplates$', () => {
        it('dispatches success with children entries', async () => {
            const parentEntry = { name: 'Parent', key: 'parent', type: 'page' };
            store.overrideSelector(fromState.selectTemplatesEntries, { parent: parentEntry } as any);
            store.refreshState();
            const children = { child1: { name: 'Child' } };
            templatesService.getChildrenTemplates.mockReturnValue(of(children));

            actions$.next(actions.loadChildrenTemplates({ templateKey: 'parent', onInit: false }));
            const result = await firstValueFrom(effects.loadChildrenTemplates$);
            expect(result.type).toBe(actions.loadChildrenTemplatesSuccess.type);
            expect((result as any).parentTemplate).toBe('parent');
        });

        it('dispatches initApp when onInit=true', async () => {
            const parentEntry = { name: 'Parent', key: 'parent', type: 'page' };
            store.overrideSelector(fromState.selectTemplatesEntries, { parent: parentEntry } as any);
            store.refreshState();
            templatesService.getChildrenTemplates.mockReturnValue(of({}));

            actions$.next(actions.loadChildrenTemplates({ templateKey: 'parent', onInit: true }));
            const results = await firstValueFrom(effects.loadChildrenTemplates$.pipe(take(3), toArray()));
            const types = results.map(r => r.type);
            expect(types).toContain(actions.initApp.type);
            expect(types).toContain(actions.setLivePreviewUrl.type);
        });

        it('dispatches fails on error', async () => {
            const parentEntry = { name: 'Parent', key: 'parent', type: 'page' };
            store.overrideSelector(fromState.selectTemplatesEntries, { parent: parentEntry } as any);
            store.refreshState();
            templatesService.getChildrenTemplates.mockReturnValue(throwError(() => ({ message: 'fail' })));

            actions$.next(actions.loadChildrenTemplates({ templateKey: 'parent', onInit: false }));
            const result = await firstValueFrom(effects.loadChildrenTemplates$);
            expect(result.type).toBe(actions.loadChildrenTemplatesFails.type);
        });

        it('does not dispatch for empty templateKey', async () => {
            actions$.next(actions.loadChildrenTemplates({ templateKey: '', onInit: false }));
            const results: Action[] = [];
            const sub = effects.loadChildrenTemplates$.subscribe(a => results.push(a));
            await new Promise(r => setTimeout(r, 50));
            sub.unsubscribe();
            expect(results.length).toBe(0);
        });
    });

    // ── onStartPreviewUrl$ ────────────────────────────────────────

    describe('onStartPreviewUrl$', () => {
        it('emits navigate event when entry has previewUrl', () => {
            actions$.next(actions.setLivePreviewUrl());
            effects.onStartPreviewUrl$.subscribe();
            expect(eventsBus.emit).toHaveBeenCalledWith({
                target: 'preview',
                payload: { type: 'navigate', url: '/preview' },
            });
        });

        it('does not emit when no previewUrl', () => {
            store.overrideSelector(fromState.selectCurrentTemplateEntry, { name: 'Home', key: 'home', previewUrl: '' } as any);
            store.refreshState();

            actions$.next(actions.setLivePreviewUrl());
            effects.onStartPreviewUrl$.subscribe();
            // emit is called only from other effects in beforeEach setup, not from this one
            expect(eventsBus.emit).not.toHaveBeenCalledWith(
                expect.objectContaining({ payload: expect.objectContaining({ type: 'navigate' }) })
            );
        });
    });

    // ── mergeCustomTemplateEntries$ ───────────────────────────────

    describe('mergeCustomTemplateEntries$', () => {
        it('dispatches useTemplateEntries with merged data', async () => {
            const schemas = { templates: { custom: { name: 'Custom' } } } as any;
            actions$.next(actions.updateCustomSchemas({ schemas }));
            const result = await firstValueFrom(effects.mergeCustomTemplateEntries$);
            expect(result.type).toBe(actions.useTemplateEntries.type);
        });
    });

    // ── setWindowTitle$ with null ─────────────────────────────────

    describe('setWindowTitle$ edge cases', () => {
        it('passes null title', () => {
            actions$.next(actions.setWindowTitle({ title: null }));
            effects.setWindowTitle$.subscribe();
            expect(metaDataService.setTitle).toHaveBeenCalledWith(null);
        });
    });
});
