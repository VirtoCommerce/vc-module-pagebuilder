import { TestBed } from '@angular/core/testing';
import { provideMockActions } from '@ngrx/effects/testing';
import { provideMockStore, MockStore } from '@ngrx/store/testing';
import { ReplaySubject, of, firstValueFrom, throwError } from 'rxjs';
import { take, toArray } from 'rxjs/operators';
import { Action } from '@ngrx/store';

import { TemplateEditorDataEffects } from './effects';
import * as actions from '../actions';
import * as sharedActions from '@shared/store/actions';
import * as selectors from '../selectors';
import * as fromRoute from '@shared/routing';
import * as fromShared from '@shared/store/selectors';
import { ModalService } from '@core/services';
import { createTemplate, createSection } from '@app/testing';

describe('TemplateEditorDataEffects', () => {
    let effects: TemplateEditorDataEffects;
    let actions$: ReplaySubject<Action>;
    let store: MockStore;
    let schemasService: { getSchemas: ReturnType<typeof vi.fn> };
    let templatesService: {
        getTemplate: ReturnType<typeof vi.fn>;
        saveTemplates: ReturnType<typeof vi.fn>;
        getTemplatePublishStatus: ReturnType<typeof vi.fn>;
        publishTemplate: ReturnType<typeof vi.fn>;
        unpublishTemplate: ReturnType<typeof vi.fn>;
        externalPreview: ReturnType<typeof vi.fn>;
        saveGroupedPage: ReturnType<typeof vi.fn>;
    };

    const template = createTemplate({
        content: [createSection({ id: 's1', type: 'hero' })],
    });

    beforeEach(() => {
        actions$ = new ReplaySubject<Action>(1);
        schemasService = { getSchemas: vi.fn().mockReturnValue(of({ sections: {}, blocks: {}, objects: {}, shared: {} })) };
        templatesService = {
            getTemplate: vi.fn().mockReturnValue(of(template)),
            saveTemplates: vi.fn().mockReturnValue(of(null)),
            getTemplatePublishStatus: vi.fn().mockReturnValue(of({ hasChanges: false, published: true })),
            publishTemplate: vi.fn().mockReturnValue(of(null)),
            unpublishTemplate: vi.fn().mockReturnValue(of(null)),
            externalPreview: vi.fn(),
            saveGroupedPage: vi.fn().mockReturnValue(of(null)),
        };

        TestBed.configureTestingModule({
            providers: [
                TemplateEditorDataEffects,
                provideMockActions(() => actions$),
                provideMockStore({
                    selectors: [
                        { selector: selectors.selectCurrentTemplateModel, value: null },
                        { selector: selectors.selectCurrentTemplateState, value: null },
                        { selector: selectors.isSchemasLoaded, value: false },
                        { selector: selectors.selectAllSchemas, value: null },
                        { selector: selectors.selectCurrentItemForEdit, value: null },
                        { selector: selectors.selectChangedTemplates, value: [] },
                        { selector: selectors.selectRunActionContext, value: { templateKey: 'home', entry: {}, path: '/home.json', type: 'page', groupId: '' } },
                        {
                            selector: selectors.changeTemplateContext,
                            value: { template, templateEntry: { name: 'Home', key: 'home' } },
                        },
                        { selector: fromShared.selectCurrentTemplateEntry, value: { name: 'Home', key: 'home' } },
                        { selector: fromRoute.selectTemplateKeyParameter, value: 'home' },
                        { selector: fromRoute.selectPathParameter, value: '/home.json' },
                        { selector: fromRoute.selectTypeParameter, value: 'page' },
                        { selector: fromRoute.selectGroupIdParameter, value: '' },
                        { selector: fromRoute.selectSectionIdParameter, value: '' },
                        { selector: fromRoute.selectBlockIdParameter, value: '' },
                        { selector: fromRoute.isEmpty, value: false },
                    ],
                }),
                { provide: ModalService, useValue: { confirm: vi.fn(), show: vi.fn() } },
            ],
        });

        store = TestBed.inject(MockStore);
        effects = TestBed.inject(TemplateEditorDataEffects);
        (effects as any).schemas = schemasService;
        (effects as any).templates = templatesService;
    });

    afterEach(() => store.resetSelectors());

    // ── loadTemplateDataOnInit$ ───────────────────────────────────

    describe('loadTemplateDataOnInit$', () => {
        it('dispatches raiseLoadData and setWindowTitle on initApp', async () => {
            actions$.next(sharedActions.initApp());
            const results = await firstValueFrom(effects.loadTemplateDataOnInit$.pipe(take(2), toArray()));
            const types = results.map(r => r.type);
            expect(types).toContain(actions.raiseLoadData.type);
            expect(types).toContain(actions.setWindowTitle.type);
        });
    });

    // ── raiseLoadTemplateSchemas$ ─────────────────────────────────

    describe('raiseLoadTemplateSchemas$', () => {
        it('dispatches loadTemplateSchemas when not loaded', async () => {
            actions$.next(actions.raiseLoadData());
            const results = await firstValueFrom(effects.raiseLoadTemplateSchemas$.pipe(take(1), toArray()));
            expect(results[0].type).toBe(actions.loadTemplateSchemas.type);
        });

        it('does not dispatch when schemas already loaded', async () => {
            store.overrideSelector(selectors.isSchemasLoaded, true);
            store.refreshState();

            actions$.next(actions.raiseLoadData());
            const results: Action[] = [];
            const sub = effects.raiseLoadTemplateSchemas$.subscribe(a => results.push(a));
            await new Promise(r => setTimeout(r, 50));
            sub.unsubscribe();
            expect(results.length).toBe(0);
        });
    });

    // ── loadSchemas$ ──────────────────────────────────────────────

    describe('loadSchemas$', () => {
        it('dispatches success on load', async () => {
            actions$.next(actions.loadTemplateSchemas());
            const result = await firstValueFrom(effects.loadSchemas$);
            expect(result.type).toBe(actions.loadTemplateSchemasSuccess.type);
        });

        it('dispatches fails on error', async () => {
            schemasService.getSchemas.mockReturnValue(throwError(() => ({ message: 'fail' })));

            actions$.next(actions.loadTemplateSchemas());
            const result = await firstValueFrom(effects.loadSchemas$);
            expect(result.type).toBe(actions.loadTemplateSchemasFails.type);
        });
    });

    // ── loadTemplate$ ─────────────────────────────────────────────

    describe('loadTemplate$', () => {
        it('dispatches success actions on load', async () => {
            actions$.next(actions.loadTemplateModel({ templateKey: 'home' }));
            const results = await firstValueFrom(effects.loadTemplate$.pipe(take(4), toArray()));
            const types = results.map(r => r.type);
            expect(types).toContain(actions.getTemplatePublishStatus.type);
            expect(types).toContain(actions.loadTemplateModelSuccess.type);
            expect(types).toContain(actions.validateItemUnderEdit.type);
            expect(types).toContain(sharedActions.broadcastPreviewMessage.type);
        });

        it('dispatches fails and notification on error', async () => {
            templatesService.getTemplate.mockReturnValue(throwError(() => ({ message: 'fail' })));

            actions$.next(actions.loadTemplateModel({ templateKey: 'home' }));
            const results = await firstValueFrom(effects.loadTemplate$.pipe(take(2), toArray()));
            const types = results.map(r => r.type);
            expect(types).toContain(actions.loadTemplateModelFails.type);
            expect(types).toContain(sharedActions.showNotification.type);
        });
    });

    // ── passTemplateToPreview$ ────────────────────────────────────

    describe('passTemplateToPreview$', () => {
        it('broadcasts template on preview loaded', async () => {
            actions$.next(sharedActions.previewLoaded());
            const result = await firstValueFrom(effects.passTemplateToPreview$);
            expect(result.type).toBe(sharedActions.broadcastPreviewMessage.type);
            expect((result as any).msg.type).toBe('page');
        });
    });

    // ── resetTemplate$ ────────────────────────────────────────────

    describe('resetTemplate$', () => {
        it('dispatches reloadTemplateModel on reset-template context menu', async () => {
            actions$.next(actions.executeContextMenuAction({ action: 'reset-template', source: 'list' }));
            const result = await firstValueFrom(effects.resetTemplate$);
            expect(result.type).toBe(actions.reloadTemplateModel.type);
            expect((result as any).templateKey).toBe('home');
        });
    });

    // ── publishTemplate$ ──────────────────────────────────────────

    describe('publishTemplate$', () => {
        it('dispatches publish success and broadcasts platform message', async () => {
            actions$.next(actions.executeToolbarAction({ action: 'publish' }));
            const results = await firstValueFrom(effects.publishTemplate$.pipe(take(2), toArray()));
            const types = results.map(r => r.type);
            expect(types).toContain(actions.getTemplatePublishStatusSuccess.type);
            expect(types).toContain(sharedActions.broadcastPlatformMessage.type);
        });
    });

    // ── unpublishTemplate$ ────────────────────────────────────────

    describe('unpublishTemplate$', () => {
        it('dispatches unpublish success and broadcasts', async () => {
            actions$.next(actions.executeToolbarAction({ action: 'unpublish' }));
            const results = await firstValueFrom(effects.unpublishTemplate$.pipe(take(2), toArray()));
            const types = results.map(r => r.type);
            expect(types).toContain(actions.getTemplatePublishStatusSuccess.type);
            expect(types).toContain(sharedActions.broadcastPlatformMessage.type);
        });
    });

    // ── raiseLoadTemplateModel$ ───────────────────────────────────

    describe('raiseLoadTemplateModel$', () => {
        it('dispatches loadTemplateModel when template not loaded', async () => {
            actions$.next(actions.raiseLoadData());
            const results = await firstValueFrom(effects.raiseLoadTemplateModel$.pipe(take(1), toArray()));
            expect(results[0].type).toBe(actions.loadTemplateModel.type);
            expect((results[0] as any).templateKey).toBe('home');
        });

        it('does not dispatch when template already loaded', async () => {
            store.overrideSelector(selectors.selectCurrentTemplateModel, template);
            store.overrideSelector(selectors.selectCurrentTemplateState, { isLoading: false } as any);
            store.overrideSelector(fromShared.selectCurrentTemplateEntry, { name: 'Home', key: 'home' } as any);
            store.refreshState();

            actions$.next(actions.raiseLoadData());
            const results: Action[] = [];
            const sub = effects.raiseLoadTemplateModel$.subscribe(a => results.push(a));
            await new Promise(r => setTimeout(r, 50));
            sub.unsubscribe();
            expect(results.length).toBe(0);
        });
    });

    // ── mergeServerSchemas$ ───────────────────────────────────────

    describe('mergeServerSchemas$', () => {
        it('dispatches useSchemasAction with merged schemas', async () => {
            const schemas = { sections: { hero: {} }, blocks: {}, objects: {}, shared: {} } as any;
            actions$.next(actions.loadTemplateSchemasSuccess({ schemas }));
            const result = await firstValueFrom(effects.mergeServerSchemas$);
            expect(result.type).toBe(actions.useSchemasAction.type);
        });
    });

    // ── mergeCustomSchemas$ ───────────────────────────────────────

    describe('mergeCustomSchemas$', () => {
        it('dispatches useSchemasAction when custom schemas arrive', async () => {
            const schemas = { sections: { banner: {} }, blocks: {}, objects: {}, shared: {} } as any;
            actions$.next(sharedActions.updateCustomSchemas({ schemas }));
            const result = await firstValueFrom(effects.mergeCustomSchemas$);
            expect(result.type).toBe(actions.useSchemasAction.type);
        });
    });

    // ── validateItemUnderEdit$ ────────────────────────────────────

    describe('validateItemUnderEdit$', () => {
        it('redirects to /pages when item not found but route has sectionId', async () => {
            store.overrideSelector(fromRoute.selectSectionIdParameter, 's1');
            store.overrideSelector(selectors.selectCurrentItemForEdit, null);
            store.refreshState();

            actions$.next(actions.validateItemUnderEdit());
            const results = await firstValueFrom(effects.validateItemUnderEdit$.pipe(take(1), toArray()));
            expect(results[0].type).toBe('[router] router go');
        });

        it('does not redirect when item exists', async () => {
            store.overrideSelector(fromRoute.selectSectionIdParameter, 's1');
            store.overrideSelector(selectors.selectCurrentItemForEdit, createSection({ id: 's1' }));
            store.refreshState();

            actions$.next(actions.validateItemUnderEdit());
            const results: Action[] = [];
            const sub = effects.validateItemUnderEdit$.subscribe(a => results.push(a));
            await new Promise(r => setTimeout(r, 50));
            sub.unsubscribe();
            expect(results.length).toBe(0);
        });

        it('does not redirect when no sectionId in route', async () => {
            actions$.next(actions.validateItemUnderEdit());
            const results: Action[] = [];
            const sub = effects.validateItemUnderEdit$.subscribe(a => results.push(a));
            await new Promise(r => setTimeout(r, 50));
            sub.unsubscribe();
            expect(results.length).toBe(0);
        });
    });

    // ── getTemplatePublishStatus$ ─────────────────────────────────

    describe('getTemplatePublishStatus$', () => {
        it('dispatches success with status', async () => {
            templatesService.getTemplatePublishStatus.mockReturnValue(of({ hasChanges: true, published: false }));
            actions$.next(actions.getTemplatePublishStatus({ templateKey: 'home' }));
            const result = await firstValueFrom(effects.getTemplatePublishStatus$);
            expect(result.type).toBe(actions.getTemplatePublishStatusSuccess.type);
            expect((result as any).hasChanges).toBe(true);
            expect((result as any).published).toBe(false);
        });

        it('dispatches fails on error', async () => {
            templatesService.getTemplatePublishStatus.mockReturnValue(throwError(() => ({ message: 'fail' })));
            actions$.next(actions.getTemplatePublishStatus({ templateKey: 'home' }));
            const result = await firstValueFrom(effects.getTemplatePublishStatus$);
            expect(result.type).toBe(actions.getTemplatePublishStatusFails.type);
        });
    });

    // ── reloadTemplate$ ───────────────────────────────────────────

    describe('reloadTemplate$', () => {
        it('dispatches success and refreshPreview on reload', async () => {
            actions$.next(actions.reloadTemplateModel({ templateKey: 'home' }));
            const results = await firstValueFrom(effects.reloadTemplate$.pipe(take(2), toArray()));
            const types = results.map(r => r.type);
            expect(types).toContain(actions.reloadTemplateModelSuccess.type);
            expect(types).toContain(actions.refreshPreview.type);
        });

        it('dispatches fails on error', async () => {
            templatesService.getTemplate.mockReturnValue(throwError(() => ({ message: 'fail' })));
            actions$.next(actions.reloadTemplateModel({ templateKey: 'home' }));
            const result = await firstValueFrom(effects.reloadTemplate$);
            expect(result.type).toBe(actions.reloadTemplateModelFails.type);
        });
    });

    // ── saveTemplate$ ─────────────────────────────────────────────

    describe('saveTemplate$', () => {
        it('dispatches saveTemplates when single changed template and no groupId', async () => {
            const changed = [{ entry: { path: '/home.json', type: 'page' }, info: { key: 'home' }, content: template }];
            store.overrideSelector(selectors.selectChangedTemplates, changed as any);
            store.refreshState();

            actions$.next(actions.executeToolbarAction({ action: 'save' }));
            const result = await firstValueFrom(effects.saveTemplate$);
            expect(result.type).toBe(actions.saveTemplates.type);
        });

        it('does not dispatch when no changed templates', async () => {
            actions$.next(actions.executeToolbarAction({ action: 'save' }));
            const results: Action[] = [];
            const sub = effects.saveTemplate$.subscribe(a => results.push(a));
            await new Promise(r => setTimeout(r, 50));
            sub.unsubscribe();
            expect(results.length).toBe(0);
        });

        it('does not dispatch for non-save toolbar actions', async () => {
            actions$.next(actions.executeToolbarAction({ action: 'publish' }));
            const results: Action[] = [];
            const sub = effects.saveTemplate$.subscribe(a => results.push(a));
            await new Promise(r => setTimeout(r, 50));
            sub.unsubscribe();
            expect(results.length).toBe(0);
        });
    });

    // ── externalPreviewAction$ ────────────────────────────────────

    describe('externalPreviewAction$', () => {
        it('calls externalPreview on service', () => {
            actions$.next(actions.executeToolbarAction({ action: 'external-preview' }));
            effects.externalPreviewAction$.subscribe();
            expect(templatesService.externalPreview).toHaveBeenCalled();
        });

        it('does not call for non-external-preview actions', () => {
            actions$.next(actions.executeToolbarAction({ action: 'save' }));
            effects.externalPreviewAction$.subscribe();
            expect(templatesService.externalPreview).not.toHaveBeenCalled();
        });
    });

    // ── sendTemplateToServer$ ─────────────────────────────────────

    describe('sendTemplateToServer$', () => {
        it('dispatches success actions for each template', async () => {
            const entry = { path: '/home.json', type: 'page' };
            const info = { key: 'home', parent: null };
            const templates = [{ entry, info, content: template }] as any;

            store.overrideSelector(selectors.selectCurrentTemplateState, { published: false } as any);
            store.refreshState();

            actions$.next(actions.saveTemplates({ templates }));
            const results = await firstValueFrom(effects.sendTemplateToServer$.pipe(take(3), toArray()));
            const types = results.map(r => r.type);
            expect(types).toContain(actions.saveTemplateSuccess.type);
            expect(types).toContain(actions.getTemplatePublishStatusSuccess.type);
            expect(types).toContain(sharedActions.broadcastPlatformMessage.type);
        });

        it('dispatches fails on server error', async () => {
            const entry = { path: '/home.json', type: 'page' };
            const info = { key: 'home' };
            const templates = [{ entry, info, content: template }] as any;
            templatesService.saveTemplates.mockReturnValue(throwError(() => ({ message: 'fail' })));

            actions$.next(actions.saveTemplates({ templates }));
            const result = await firstValueFrom(effects.sendTemplateToServer$);
            expect(result.type).toBe(actions.saveTemplateFails.type);
        });
    });
});
