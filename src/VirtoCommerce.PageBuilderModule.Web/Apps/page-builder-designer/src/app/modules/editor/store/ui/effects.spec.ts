import { TestBed } from '@angular/core/testing';
import { provideMockActions } from '@ngrx/effects/testing';
import { provideMockStore, MockStore } from '@ngrx/store/testing';
import { ReplaySubject, firstValueFrom } from 'rxjs';
import { take, toArray } from 'rxjs/operators';
import { Action } from '@ngrx/store';

import { TemplateEditorUiEffects } from './effects';
import * as actions from '../actions';
import * as sharedActions from '@shared/store/actions';
import * as routingActions from '@shared/routing/actions';
import * as selectors from '../selectors';
import * as sharedSelectors from '@shared/store/selectors';
import { createTemplate, createSection, createSchema } from '@app/testing';

describe('TemplateEditorUiEffects', () => {
    let effects: TemplateEditorUiEffects;
    let actions$: ReplaySubject<Action>;
    let store: MockStore;

    beforeEach(() => {
        actions$ = new ReplaySubject<Action>(1);

        TestBed.configureTestingModule({
            providers: [
                TemplateEditorUiEffects,
                provideMockActions(() => actions$),
                provideMockStore({
                    selectors: [
                        { selector: sharedSelectors.selectCurrentTemplateEntry, value: { name: 'Home', key: 'home' } },
                        { selector: selectors.selectCurrentTemplateModel, value: createTemplate() },
                        { selector: sharedSelectors.selectParentTemplateKey, value: null },
                        { selector: selectors.selectSectionModelFromRoute, value: null },
                        { selector: selectors.selectBlockModelFromRoute, value: null },
                        { selector: selectors.selectSharedSchemas, value: {} },
                        { selector: selectors.selectObjectsSchemas, value: {} },
                        { selector: selectors.selectLinkedComponents, value: {} },
                    ],
                }),
            ],
        });

        store = TestBed.inject(MockStore);
        effects = TestBed.inject(TemplateEditorUiEffects);
    });

    afterEach(() => store.resetSelectors());

    // ── navigateToAddSection$ ─────────────────────────────────────

    describe('navigateToAddSection$', () => {
        it('navigates to /pages/add when no sectionId', async () => {
            actions$.next(actions.showBlankSections({ sectionId: null, positionIndex: 2 }));
            const result = await firstValueFrom(effects.navigateToAddSection$);
            expect(result.type).toBe(routingActions.go.type);
            expect((result as any).path).toEqual(['/pages/add', 2]);
        });
    });

    describe('navigateToAddBlock$', () => {
        it('navigates with sectionId when provided', async () => {
            actions$.next(actions.showBlankSections({ sectionId: 's1', positionIndex: 0 }));
            const result = await firstValueFrom(effects.navigateToAddBlock$);
            expect(result.type).toBe(routingActions.go.type);
            expect((result as any).path).toEqual(['/pages/add', 's1', 0]);
        });
    });

    // ── navigateToEditTemplate$ ───────────────────────────────────

    describe('navigateToEditTemplate$', () => {
        it('navigates to /pages and resets groups', async () => {
            actions$.next(actions.closeAddItemPanel());
            const results = await firstValueFrom(effects.navigateToEditTemplate$.pipe(take(2), toArray()));
            expect(results[0].type).toBe(routingActions.go.type);
            expect(results[1].type).toBe(actions.resetGroupsState.type);
        });
    });

    // ── navigateToEditSettings$ ───────────────────────────────────

    describe('navigateToEditSettings$', () => {
        it('navigates to settings path', async () => {
            actions$.next(actions.editSettings({ schema: createSchema({ type: 'hero' }) }));
            const results = await firstValueFrom(effects.navigateToEditSettings$.pipe(take(1), toArray()));
            expect(results[0].type).toBe(routingActions.go.type);
            expect((results[0] as any).path).toEqual(['/pages', 'settings', 'hero']);
        });
    });

    // ── navigateToEditSection$ ────────────────────────────────────

    describe('navigateToEditSection$', () => {
        it('navigates to section path', async () => {
            actions$.next(actions.editSectionAction({ sectionId: 's1' }));
            const results = await firstValueFrom(effects.navigateToEditSection$.pipe(take(1), toArray()));
            expect(results[0].type).toBe(routingActions.go.type);
            expect((results[0] as any).path).toEqual(['/pages', 's1']);
        });
    });

    // ── navigateToEditBlock$ ──────────────────────────────────────

    describe('navigateToEditBlock$', () => {
        it('navigates to block path', async () => {
            actions$.next(actions.editBlockAction({ sectionId: 's1', blockId: 'b1' }));
            const results = await firstValueFrom(effects.navigateToEditBlock$.pipe(take(1), toArray()));
            expect(results[0].type).toBe(routingActions.go.type);
            expect((results[0] as any).path).toEqual(['/pages', 's1', 'b1']);
        });
    });

    // ── raiseUpdateTemplate$ ──────────────────────────────────────

    describe('raiseUpdateTemplate$', () => {
        it('dispatches templateContentChanged', async () => {
            actions$.next(actions.updateTemplateAction({ template: createTemplate(), templateKey: 'home' }));
            const result = await firstValueFrom(effects.raiseUpdateTemplate$);
            expect(result.type).toBe(actions.templateContentChanged.type);
        });
    });

    // ── navigateToThemeSettings$ ──────────────────────────────────

    describe('navigateToThemeSettings$', () => {
        it('jumps to /themes on theme-settings action', async () => {
            actions$.next(actions.executeToolbarAction({ action: 'theme-settings' }));
            const result = await firstValueFrom(effects.navigateToThemeSettings$);
            expect(result.type).toBe(routingActions.jump.type);
            expect((result as any).path).toEqual(['/themes']);
        });
    });

    // ── notifyFailsSave$ ──────────────────────────────────────────

    describe('notifyFailsSave$', () => {
        it('shows error notification', async () => {
            actions$.next(actions.saveTemplateFails({ error: {} as any }));
            const result = await firstValueFrom(effects.notifyFailsSave$);
            expect(result.type).toBe(sharedActions.showNotification.type);
            expect((result as any).msgType).toBe('error');
        });
    });

    describe('notifySuccessSave$', () => {
        it('uses the Linked Component name for a linked document', async () => {
            store.overrideSelector(selectors.selectLinkedComponents, {
                'component-1': {
                    id: 'component-1',
                    storeId: 'store-1',
                    name: 'Shared hero',
                    usageCount: 0,
                    usagePages: [],
                },
            });
            store.refreshState();
            actions$.next(actions.saveTemplateSuccess({
                templateKey: 'linked-component::component-1',
                template: createTemplate(),
            }));

            const results = await firstValueFrom(effects.notifySuccessSave$.pipe(take(2), toArray()));
            const notification = results.find(result => result.type === sharedActions.showNotification.type) as any;

            expect(notification.message).toContain('Shared Component “Shared hero”');
        });

        it('does not clear dirty state when newer edits were made during save', async () => {
            actions$.next(actions.saveTemplateSuccess({
                templateKey: 'linked-component::component-1',
                template: createTemplate(),
                clearDirty: false,
            }));

            const results = await firstValueFrom(effects.notifySuccessSave$.pipe(take(2), toArray()));
            const dirtyAction = results.find(result => result.type === sharedActions.setRootDirtyState.type) as ReturnType<typeof sharedActions.setRootDirtyState>;
            expect(dirtyAction.dirty).toBe(true);
        });
    });

    // ── hoverSection$ ─────────────────────────────────────────────

    describe('hoverSection$', () => {
        it('broadcasts hover preview message', async () => {
            actions$.next(actions.hoverSection({ sectionId: 's1' }));
            const result = await firstValueFrom(effects.hoverSection$);
            expect(result.type).toBe(sharedActions.broadcastPreviewMessage.type);
            expect((result as any).msg.type).toBe('hover');
            expect((result as any).msg.sectionId).toBe('s1');
        });

        it('broadcasts hover leave', async () => {
            actions$.next(actions.hoverSection({ sectionId: null }));
            const result = await firstValueFrom(effects.hoverSection$);

            expect((result as any).msg).toEqual({ type: 'hover', sectionId: null });
        });
    });

    // ── setWindowTitle$ ───────────────────────────────────────────

    describe('setWindowTitle$', () => {
        it('dispatches shared setWindowTitle with entry name', async () => {
            actions$.next(actions.setWindowTitle());
            const result = await firstValueFrom(effects.setWindowTitle$);
            expect(result.type).toBe(sharedActions.setWindowTitle.type);
            expect((result as any).title).toBe('Home');
        });

        it('dispatches null title when entry has no name', async () => {
            store.overrideSelector(sharedSelectors.selectCurrentTemplateEntry, { key: 'home' } as any);
            store.refreshState();

            actions$.next(actions.setWindowTitle());
            const result = await firstValueFrom(effects.setWindowTitle$);
            expect((result as any).title).toBeNull();
        });
    });

    // ── completeEditSection$ ──────────────────────────────────────

    describe('completeEditSection$', () => {
        it('navigates to /pages on closeEditItemPanel', async () => {
            actions$.next(actions.closeEditItemPanel());
            const results = await firstValueFrom(effects.completeEditSection$.pipe(take(1), toArray()));
            expect(results[0].type).toBe(routingActions.go.type);
            expect((results[0] as any).path).toEqual(['/pages']);
        });
    });

    // ── templateChanged$ ──────────────────────────────────────────

    describe('templateChanged$', () => {
        it('navigates with template params on templateChanged', async () => {
            actions$.next(sharedActions.templateChanged({ templateType: 'blog', path: '/blog.json', parent: null }));
            const results = await firstValueFrom(effects.templateChanged$.pipe(take(1), toArray()));
            expect(results[0].type).toBe(routingActions.go.type);
            expect((results[0] as any).queryParams).toEqual(
                expect.objectContaining({ type: 'blog', path: '/blog.json' })
            );
        });
    });

    // ── templateContentChanged$ ───────────────────────────────────

    describe('templateContentChanged$', () => {
        it('broadcasts preview and sets root dirty', async () => {
            actions$.next(actions.templateContentChanged());
            const results = await firstValueFrom(effects.templateContentChanged$.pipe(take(2), toArray()));
            const types = results.map(r => r.type);
            expect(types).toContain(actions.broadcastResolvedPreview.type);
            expect(types).toContain(sharedActions.setRootDirtyState.type);
        });

        it('sets child dirty when parentKey exists', async () => {
            store.overrideSelector(sharedSelectors.selectParentTemplateKey, 'parent-key');
            store.refreshState();

            actions$.next(actions.templateContentChanged());
            const results = await firstValueFrom(effects.templateContentChanged$.pipe(take(2), toArray()));
            const types = results.map(r => r.type);
            expect(types).toContain(sharedActions.setDirtyState.type);
        });
    });

    // ── scrollToSectionInPreview$ ─────────────────────────────────

    describe('scrollToSectionInPreview$', () => {
        it('broadcasts select message with section', async () => {
            actions$.next(actions.editSectionAction({ sectionId: 's1' }));

            // editSectionAction triggers both navigateToEditSection$ and scrollToSectionInPreview$
            const result = await firstValueFrom(effects.scrollToSectionInPreview$);
            expect(result.type).toBe(actions.broadcastResolvedPreview.type);
            expect((result as any).msg.type).toBe('select');
            expect((result as any).msg.sectionId).toBe('s1');
        });
    });

    // ── scrollToBlockInPreview$ ───────────────────────────────────

    describe('scrollToBlockInPreview$', () => {
        it('broadcasts select message with block', async () => {
            store.overrideSelector(selectors.selectSectionModelFromRoute, createSection({ id: 's1' }));
            store.refreshState();

            actions$.next(actions.editBlockAction({ sectionId: 's1', blockId: 'b1' }));
            const result = await firstValueFrom(effects.scrollToBlockInPreview$);
            expect(result.type).toBe(actions.broadcastResolvedPreview.type);
            expect((result as any).msg.type).toBe('select');
            expect((result as any).msg.blockId).toBe('b1');
        });
    });

    // ── navigateToThemeSettings$ filter ───────────────────────────

    describe('navigateToThemeSettings$ filter', () => {
        it('does not trigger for non-theme-settings toolbar actions', async () => {
            actions$.next(actions.executeToolbarAction({ action: 'save' }));
            const results: Action[] = [];
            const sub = effects.navigateToThemeSettings$.subscribe(a => results.push(a));
            await new Promise(r => setTimeout(r, 50));
            sub.unsubscribe();
            expect(results.length).toBe(0);
        });
    });

    // ── navigateToAddSection$ filter ──────────────────────────────

    describe('navigateToAddSection$ filter', () => {
        it('does not trigger when sectionId is present', async () => {
            actions$.next(actions.showBlankSections({ sectionId: 's1', positionIndex: 0 }));
            const results: Action[] = [];
            const sub = effects.navigateToAddSection$.subscribe(a => results.push(a));
            await new Promise(r => setTimeout(r, 50));
            sub.unsubscribe();
            expect(results.length).toBe(0);
        });
    });
});
