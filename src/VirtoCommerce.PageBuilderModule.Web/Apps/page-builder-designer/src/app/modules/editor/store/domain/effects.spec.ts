import { TestBed } from '@angular/core/testing';
import { provideMockActions } from '@ngrx/effects/testing';
import { provideMockStore, MockStore } from '@ngrx/store/testing';
import { ReplaySubject, of, firstValueFrom } from 'rxjs';
import { take, toArray } from 'rxjs/operators';
import { Action } from '@ngrx/store';

import { TemplateEditorDomainEffects } from './effects';
import * as actions from '../actions';
import * as sharedActions from '@shared/store/actions';
import * as selectors from '../selectors';
import * as sharedSelectors from '@shared/store/selectors';
import * as routingSelectors from '@shared/routing/selectors';
import { ClipboardService, ModalService, EventsBusService } from '@core/services';
import { AppConfig, EvaluatorService } from '@integration/services';
import { createTemplate, createSection, createSchema } from '@app/testing';

describe('TemplateEditorDomainEffects', () => {
    let effects: TemplateEditorDomainEffects;
    let actions$: ReplaySubject<Action>;
    let store: MockStore;
    let clipboardService: { copy: ReturnType<typeof vi.fn>; getData: ReturnType<typeof vi.fn> };
    let modalService: { confirm: ReturnType<typeof vi.fn>; show: ReturnType<typeof vi.fn> };
    let eventsBus: { emit: ReturnType<typeof vi.fn> };
    let appConfig: { getValue: ReturnType<typeof vi.fn> };

    const template = createTemplate({
        content: [
            createSection({ id: 's1', type: 'hero', blocks: [createSection({ id: 'b1', type: 'text' })] }),
            createSection({ id: 's2', type: 'banner' }),
        ],
    });

    beforeEach(() => {
        actions$ = new ReplaySubject<Action>(1);
        clipboardService = { copy: vi.fn(), getData: vi.fn().mockResolvedValue(null) };
        modalService = { confirm: vi.fn().mockReturnValue(of(true)), show: vi.fn() };
        eventsBus = { emit: vi.fn() };
        appConfig = { getValue: vi.fn().mockReturnValue(true) };

        TestBed.configureTestingModule({
            providers: [
                TemplateEditorDomainEffects,
                provideMockActions(() => actions$),
                provideMockStore({
                    selectors: [
                        {
                            selector: selectors.changeTemplateContext,
                            value: {
                                template,
                                section: template.content[0],
                                block: null,
                                sectionsSchemas: { hero: { blocks: ['text'] } },
                                blocksSchemas: { text: {} },
                                templateKey: 'home',
                                sectionId: 's1',
                                blockId: '',
                                insertIndex: -1,
                                templateEntry: { name: 'Home', key: 'home', sections: [] },
                            },
                        },
                        { selector: selectors.selectSharedSchemas, value: {} },
                        { selector: selectors.selectObjectsSchemas, value: {} },
                        { selector: selectors.selectCheckedItems, value: [] },
                        { selector: sharedSelectors.selectCurrentTemplateEntry, value: { name: 'Home', key: 'home' } },
                        { selector: selectors.selectCurrentTemplateModel, value: template },
                        { selector: routingSelectors.selectLinkedComponentIdParameter, value: '' },
                    ],
                }),
                { provide: ClipboardService, useValue: clipboardService },
                { provide: ModalService, useValue: modalService },
                { provide: EventsBusService, useValue: eventsBus },
                { provide: EvaluatorService, useValue: { evaluate: vi.fn() } },
                { provide: AppConfig, useValue: appConfig },
            ],
        });

        store = TestBed.inject(MockStore);
        effects = TestBed.inject(TemplateEditorDomainEffects);
    });

    afterEach(() => store.resetSelectors());

    // ── copyItemToClipboard$ ──────────────────────────────────────

    describe('copyItemToClipboard$', () => {
        it('copies section to clipboard and shows notification', async () => {
            const section = createSection({ id: 's1', type: 'hero' });
            actions$.next(actions.executeContextMenuAction({ action: 'copy', source: 'list', section }));

            const result = await firstValueFrom(effects.copyItemToClipboard$);

            expect(clipboardService.copy).toHaveBeenCalledWith({
                content: expect.objectContaining({ id: 's1', type: 'hero' }),
                type: 'section',
            });
            expect(result.type).toBe(sharedActions.showNotification.type);
        });

        it('copies block with type "block"', async () => {
            const section = createSection({ id: 's1' });
            const block = createSection({ id: 'b1', type: 'text' });
            actions$.next(actions.executeContextMenuAction({ action: 'copy', source: 'list', section, block }));

            effects.copyItemToClipboard$.subscribe();

            expect(clipboardService.copy).toHaveBeenCalledWith(
                expect.objectContaining({ type: 'block' })
            );
        });
    });

    // ── refreshPreviewAction$ ─────────────────────────────────────

    describe('refreshPreviewAction$', () => {
        it('maps refresh-preview context menu to refreshPreview action', async () => {
            actions$.next(actions.executeContextMenuAction({ action: 'refresh-preview', source: 'list' }));
            const result = await firstValueFrom(effects.refreshPreviewAction$);
            expect(result.type).toBe(actions.refreshPreview.type);
        });
    });

    // ── refreshPreview$ ───────────────────────────────────────────

    describe('refreshPreview$', () => {
        it('broadcasts reload message', async () => {
            actions$.next(actions.refreshPreview());
            const results = await firstValueFrom(effects.refreshPreview$.pipe(take(1), toArray()));
            expect(results[0].type).toBe(actions.broadcastResolvedPreview.type);
            expect((results[0] as any).msg.type).toBe('reload');
        });
    });

    // ── deleteSectionOrBlock$ ─────────────────────────────────────

    describe('deleteSectionOrBlock$', () => {
        it('dispatches updateTemplate on confirm', async () => {
            const section = createSection({ id: 's1', type: 'hero' });
            modalService.confirm.mockReturnValue(of(true));

            actions$.next(actions.executeContextMenuAction({ action: 'delete', source: 'list', section }));

            const results = await firstValueFrom(effects.deleteSectionOrBlock$.pipe(take(3), toArray()));
            const types = results.map(r => r.type);
            expect(types).toContain(actions.updateTemplateAction.type);
            expect(types).toContain(actions.broadcastResolvedPreview.type);
            expect(types).toContain(actions.closeAddItemPanel.type);
        });

        it('dispatches empty on cancel', async () => {
            const section = createSection({ id: 's1' });
            modalService.confirm.mockReturnValue(of(false));

            actions$.next(actions.executeContextMenuAction({ action: 'delete', source: 'list', section }));

            const results = await firstValueFrom(effects.deleteSectionOrBlock$.pipe(take(1), toArray()));
            expect(results[0].type).toBe(sharedActions.empty.type);
        });
    });

    // ── updateEditableModel$ ──────────────────────────────────────

    describe('updateEditableModel$', () => {
        it('dispatches updateTemplateAction for section changes', async () => {
            actions$.next(actions.sectionChangedAction({ changes: { hidden: true } as any }));

            const results = await firstValueFrom(effects.updateEditableModel$.pipe(take(1), toArray()));
            expect(results[0].type).toBe(actions.updateTemplateAction.type);
        });

        it('ignores changes to a read-only linked-component original', () => {
            store.overrideSelector(routingSelectors.selectLinkedComponentIdParameter, 'component-1');
            appConfig.getValue.mockImplementation((key: string) => key === 'canInsertLinkedComponents');
            store.refreshState();
            const emitted: Action[] = [];
            const subscription = effects.updateEditableModel$.subscribe(action => emitted.push(action));

            actions$.next(actions.sectionChangedAction({ changes: { hidden: true } as any }));

            expect(emitted).toEqual([]);
            subscription.unsubscribe();
        });
    });

    // ── duplicateItem$ ────────────────────────────────────────────

    describe('duplicateItem$', () => {
        it('duplicates section and notifies', async () => {
            const section = createSection({ id: 's1', type: 'hero' });
            actions$.next(actions.executeContextMenuAction({ action: 'duplicate', source: 'list', section }));

            const results = await firstValueFrom(effects.duplicateItem$.pipe(take(3), toArray()));
            const types = results.map(r => r.type);
            expect(types).toContain(actions.broadcastResolvedPreview.type);
            expect(types).toContain(actions.updateTemplateAction.type);
            expect(types).toContain(sharedActions.showNotification.type);
        });

        it('opens editor when source is "editor"', async () => {
            const section = createSection({ id: 's1', type: 'hero' });
            actions$.next(actions.executeContextMenuAction({ action: 'duplicate', source: 'editor', section }));

            const results = await firstValueFrom(effects.duplicateItem$.pipe(take(4), toArray()));
            const types = results.map(r => r.type);
            expect(types).toContain(actions.editSectionAction.type);
        });

        it('does not open editor when source is "list"', async () => {
            const section = createSection({ id: 's1', type: 'hero' });
            actions$.next(actions.executeContextMenuAction({ action: 'duplicate', source: 'list', section }));

            const results = await firstValueFrom(effects.duplicateItem$.pipe(take(3), toArray()));
            const types = results.map(r => r.type);
            expect(types).not.toContain(actions.editSectionAction.type);
            expect(types).not.toContain(actions.editBlockAction.type);
        });
    });

    // ── showItem$ ─────────────────────────────────────────────────

    describe('showItem$', () => {
        it('dispatches updateTemplate with hidden=true for hide action', async () => {
            const section = createSection({ id: 's1', type: 'hero' });
            actions$.next(actions.executeContextMenuAction({ action: 'hide', source: 'list', section }));

            const results = await firstValueFrom(effects.showItem$.pipe(take(2), toArray()));
            const types = results.map(r => r.type);
            expect(types).toContain(actions.updateTemplateAction.type);
            expect(types).toContain(actions.broadcastResolvedPreview.type);
        });

        it('dispatches updateTemplate with hidden=false for show action', async () => {
            const section = createSection({ id: 's1', type: 'hero' });
            actions$.next(actions.executeContextMenuAction({ action: 'show', source: 'list', section }));

            const results = await firstValueFrom(effects.showItem$.pipe(take(2), toArray()));
            expect(results[0].type).toBe(actions.updateTemplateAction.type);
        });

        it('ignores non-show/hide actions', async () => {
            actions$.next(actions.executeContextMenuAction({ action: 'copy', source: 'list' }));
            const results: Action[] = [];
            const sub = effects.showItem$.subscribe(a => results.push(a));
            await new Promise(r => setTimeout(r, 50));
            sub.unsubscribe();
            expect(results.length).toBe(0);
        });
    });

    // ── updateTemplateSettings$ ───────────────────────────────────

    describe('updateTemplateSettings$', () => {
        it('dispatches updateTemplate for settings when no sectionId', async () => {
            store.overrideSelector(selectors.changeTemplateContext, {
                template,
                section: null,
                block: null,
                sectionsSchemas: {},
                blocksSchemas: {},
                templateKey: 'home',
                sectionId: '',
                blockId: '',
                insertIndex: -1,
                templateEntry: { name: 'Home', key: 'home' },
            } as any);
            store.refreshState();

            actions$.next(actions.sectionChangedAction({ changes: { name: 'Updated' } as any }));
            const results = await firstValueFrom(effects.updateTemplateSettings$.pipe(take(1), toArray()));
            expect(results[0].type).toBe(actions.updateTemplateAction.type);
        });
    });

    // ── orderSections$ ────────────────────────────────────────────

    describe('orderSections$', () => {
        it('reorders sections and broadcasts swap message', async () => {
            actions$.next(actions.sortItems({
                options: { currentIndex: 0, previousIndex: 1, parent: null } as any,
            }));

            const results = await firstValueFrom(effects.orderSections$.pipe(take(2), toArray()));
            const types = results.map(r => r.type);
            expect(types).toContain(actions.updateTemplateAction.type);
            expect(types).toContain(actions.broadcastResolvedPreview.type);
            expect((results[1] as any).msg.type).toBe('swap');
        });

        it('ignores sort with parent (block sort)', async () => {
            actions$.next(actions.sortItems({
                options: { currentIndex: 0, previousIndex: 1, parent: createSection({ id: 's1' }) } as any,
            }));

            const results: Action[] = [];
            const sub = effects.orderSections$.subscribe(a => results.push(a));
            await new Promise(r => setTimeout(r, 50));
            sub.unsubscribe();
            expect(results.length).toBe(0);
        });
    });

    // ── orderBlocks$ ──────────────────────────────────────────────

    describe('orderBlocks$', () => {
        it('reorders blocks when parent is set', async () => {
            actions$.next(actions.sortItems({
                options: { currentIndex: 0, previousIndex: 1, parent: createSection({ id: 's1' }) } as any,
            }));

            const results = await firstValueFrom(effects.orderBlocks$.pipe(take(1), toArray()));
            expect(results[0].type).toBe(actions.updateTemplateAction.type);
        });
    });

    // ── pasteFromClipboardAction$ ─────────────────────────────────

    describe('pasteFromClipboardAction$', () => {
        it('dispatches actions from clipboardHelpers', async () => {
            const value = { type: 'section', content: { type: 'banner' } } as any;
            actions$.next(actions.pasteFromClipboard({ value, action: 'paste-after', source: 'list' }));

            const results = await firstValueFrom(effects.pasteFromClipboardAction$.pipe(take(3), toArray()));
            const types = results.map(r => r.type);
            expect(types).toContain(actions.broadcastResolvedPreview.type);
            expect(types).toContain(actions.updateTemplateAction.type);
        });
    });

    // ── pasteItemFromClipboardAction$ ─────────────────────────────

    describe('pasteItemFromClipboardAction$', () => {
        it('reads clipboard and dispatches pasteFromClipboard', async () => {
            const clipData = { type: 'section', content: { type: 'banner' } };
            clipboardService.getData.mockResolvedValue(clipData);

            const section = createSection({ id: 's1', type: 'hero' });
            actions$.next(actions.executeContextMenuAction({ action: 'paste-after', source: 'list', section }));

            const result = await firstValueFrom(effects.pasteItemFromClipboardAction$);
            expect(result.type).toBe(actions.pasteFromClipboard.type);
            expect((result as any).value).toBe(clipData);
        });

        it('does not dispatch when clipboard is empty', async () => {
            clipboardService.getData.mockResolvedValue(null);

            actions$.next(actions.executeContextMenuAction({ action: 'paste-after', source: 'list' }));
            const results: Action[] = [];
            const sub = effects.pasteItemFromClipboardAction$.subscribe(a => results.push(a));
            await new Promise(r => setTimeout(r, 100));
            sub.unsubscribe();
            expect(results.length).toBe(0);
        });
    });

    // ── context menu action filters ───────────────────────────────

    describe('context menu action filters', () => {
        it('refreshPreviewAction$ ignores non-refresh actions', async () => {
            actions$.next(actions.executeContextMenuAction({ action: 'copy', source: 'list' }));
            const results: Action[] = [];
            const sub = effects.refreshPreviewAction$.subscribe(a => results.push(a));
            await new Promise(r => setTimeout(r, 50));
            sub.unsubscribe();
            expect(results.length).toBe(0);
        });

        it('deleteSectionOrBlock$ ignores non-delete actions', async () => {
            actions$.next(actions.executeContextMenuAction({ action: 'copy', source: 'list' }));
            const results: Action[] = [];
            const sub = effects.deleteSectionOrBlock$.subscribe(a => results.push(a));
            await new Promise(r => setTimeout(r, 50));
            sub.unsubscribe();
            expect(results.length).toBe(0);
        });
    });

    // ── refreshPreview$ guard ─────────────────────────────────────

    describe('refreshPreview$ guard', () => {
        it('does not broadcast when template is null', async () => {
            store.overrideSelector(selectors.changeTemplateContext, {
                template: null, section: null, block: null,
                sectionsSchemas: {}, blocksSchemas: {},
                templateKey: 'home', sectionId: '', blockId: '', insertIndex: -1,
                templateEntry: { name: 'Home', key: 'home' },
            } as any);
            store.refreshState();

            actions$.next(actions.refreshPreview());
            const results: Action[] = [];
            const sub = effects.refreshPreview$.subscribe(a => results.push(a));
            await new Promise(r => setTimeout(r, 50));
            sub.unsubscribe();
            expect(results.length).toBe(0);
        });
    });
});
