import { AppSettings } from './../services/app.settings';
import { Injectable } from '@angular/core';
import { Actions, Effect, ofType, createEffect } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { of, fromEvent, timer } from 'rxjs';
import {
    switchMapTo, debounceTime, distinctUntilChanged,
    withLatestFrom, tap, filter, map, switchMap, mapTo
} from 'rxjs/operators';
import { PreviewService, ApiUrlsService } from '@app/services';
import { BlockValuesModel } from '@shared/models';

import * as rootActions from './root.actions';
import * as fromRoot from '.';

import * as themeActions from '@themes/store/theme.actions';
import * as fromTheme from '@themes/store';

import * as editorActions from '@editor/store/editor.actions';
import * as fromEditor from '@editor/store';

@Injectable()
export class RootEffects {
    constructor(private actions$: Actions,
        private preview: PreviewService,
        private urls: ApiUrlsService,
        private rootStore$: Store<fromRoot.State>,
        private themeStore$: Store<fromTheme.State>,
        private editorStore$: Store<fromEditor.State>) { }

    resetData$ = createEffect(() => this.actions$.pipe(
        ofType(rootActions.resetData),
        switchMapTo([
            editorActions.clearPageChanges(),
            themeActions.clearThemeChanges()
        ])
    ));

    closeEditors$ = createEffect(() => this.actions$.pipe(
        ofType(rootActions.closeEditors),
        switchMapTo([
            editorActions.closeEditors(),
            themeActions.closeEditors()
        ])
    ));

    switchToLoadPage$ = createEffect(() => this.actions$.pipe(
        ofType(rootActions.loadData),
        withLatestFrom(
            this.editorStore$.select(fromEditor.getPage),
            this.editorStore$.select(fromEditor.getPageNotLoaded)
        ),
        filter(([, page, pageNotLoaded]) => !page || pageNotLoaded),
        mapTo(editorActions.loadPage())
    ));

    switchToLoadBlocks$ = createEffect(() => this.actions$.pipe(
        ofType(rootActions.loadData),
        withLatestFrom(
            this.editorStore$.select(fromEditor.getBlocksSchema),
            this.editorStore$.select(fromEditor.getSchemaNotLoaded)
        ),
        filter(([, blocksSchema, schemaNotLoaded]) => !blocksSchema || schemaNotLoaded),
        mapTo(editorActions.loadBlocksSchema())
    ));

    switchToLoadThemes$ = createEffect(() => this.actions$.pipe(
        ofType(rootActions.loadData),
        withLatestFrom(
            this.themeStore$.select(fromTheme.getPresets),
            this.themeStore$.select(fromTheme.getPresetsNotLoaded)
        ),
        filter(([, presets, presetsNotLoaded]) => !presets || presetsNotLoaded),
        mapTo(themeActions.loadThemes())
    ));

    switchToLoadThemeSchema$ = createEffect(() => this.actions$.pipe(
        ofType(rootActions.loadData),
        withLatestFrom(
            this.themeStore$.select(fromTheme.getSchema),
            this.themeStore$.select(fromTheme.getSchemaNotLoaded)
        ),
        filter(([, schema, schemaNotLoaded]) => !schema || schemaNotLoaded),
        mapTo(themeActions.loadSchema())
    ));

    saveData$ = createEffect(() => this.actions$.pipe(
        ofType(rootActions.saveData),
        switchMapTo([
            editorActions.savePage(),
            themeActions.saveTheme()
        ])
    ));

    setPreviewUrl = createEffect(() => createEffect(() => this.actions$.pipe(
        ofType(editorActions.loadPageSuccess),
        switchMap(action => {
            if (!!action.page) {
                const result = this.urls.getStoreUrl(<string>action.page['layout']);
                return [rootActions.setPreviewUrl({ url: result }), rootActions.reloadPreview()];
            }
            return [rootActions.setPreviewUrl({ url: null })];
        })
    )));

    previewFailed = createEffect(() => this.actions$.pipe(
        ofType(rootActions.previewError),
        tap(action => {
            console.log(action.error);
        })
    ), { dispatch: false });

    previewFailedByTimeout$ = createEffect(() => this.actions$.pipe(
        ofType(rootActions.checkPreviewLoadedOrError),
        withLatestFrom(
            this.rootStore$.select(fromRoot.getPrimaryIsLoaded),
            this.rootStore$.select(fromRoot.getSecondaryIsLoaded),
            this.rootStore$.select(fromRoot.getPreviewLoading)
        ),
        filter(([, primaryLoaded, secondaryLoaded, isLoadingStill]) => (!primaryLoaded && !secondaryLoaded) || isLoadingStill),
        map(() => rootActions.previewError({ error: 'timeoutError' }))
    ));

    // themes

    uploadPreviewPreset$ = createEffect(() => this.actions$.pipe(
        ofType(themeActions.updateDraftSuccess),
        withLatestFrom(
            this.rootStore$.select(fromRoot.getSecondaryFrameId),
            this.rootStore$.select(fromRoot.getSecondaryIsLoaded)
        ),
        switchMap(([, frameId, previewReady]) => {
            if (previewReady) {
                this.preview.reload(frameId);
            }
            return of(rootActions.previewLoading({ isLoading: true, msg: 'update draft success' }));
        })
    ));

    // editor

    sendHoverToPreview$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.highlightInPreview),
        withLatestFrom(
            this.rootStore$.select(fromRoot.getPrimaryFrameId),
            this.rootStore$.select(fromRoot.getPrimaryIsLoaded)
        ),
        tap(([action, frameId, previewReady]) => previewReady && this.preview.hover(action.block, frameId))
    ), { dispatch: false });

    sendPreviewPageItem$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.previewPageItem),
        withLatestFrom(
            this.rootStore$.select(fromRoot.getPrimaryFrameId),
            this.rootStore$.select(fromRoot.getPrimaryIsLoaded)
        ),
        tap(([action, frameId, previewReady]) => previewReady && this.preview.preview(action.block, frameId))
    ), { dispatch: false });

    sendNewBlockToStoreLoaded$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.addPageItem),
        withLatestFrom(
            this.rootStore$.select(fromRoot.getPrimaryFrameId),
            this.rootStore$.select(fromRoot.getPrimaryIsLoaded)
        ),
        tap(([action, frameId, previewReady]) => previewReady && this.preview.add(action.block, frameId))
    ), { dispatch: false });

    scrollPreviewToObject$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.selectPageItem),
        filter(action => !!action.blockId),
        withLatestFrom(this.rootStore$.select(fromRoot.getPrimaryFrameId)),
        tap(([action, frameId]) => {
            this.preview.selectBlock(action.blockId, frameId);
        })
    ), { dispatch: false });

    deselectObject$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.completeEditPageItem),
        withLatestFrom(this.rootStore$.select(fromRoot.getPrimaryFrameId)),
        tap(([, frameId]) => {
            this.preview.selectBlock(0, frameId);
        })
    ), { dispatch: false });

    sendUpdatedBlockToStoreLoaded$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.updateBlockPreview),
        withLatestFrom(
            this.rootStore$.select(fromRoot.getPrimaryFrameId),
            this.rootStore$.select(fromRoot.getPrimaryIsLoaded),
            this.editorStore$.select(fromEditor.getCurrentSectionItem)
        ),
        filter(([, , previewReady]) => previewReady),
        map(([action, frameId, , currentItem]): [BlockValuesModel, string] => [
            <BlockValuesModel>{ ...currentItem, ...action.block },
            frameId
        ]),
        filter(([block]) => block.type !== 'settings'),
        debounceTime(500),
        distinctUntilChanged(),
        tap(([block, frameId]) => this.preview.update(block, frameId))
    ), { dispatch: false });

    sendBlocksOrderChanged$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.orderChanged),
        withLatestFrom(this.rootStore$.select(fromRoot.getPrimaryFrameId)),
        tap(([action, frameId]) =>
            this.preview.changeOrder(action.previousIndex, action.currentIndex, frameId))
    ), { dispatch: false });

    sendRemoveBlockToStoreLoaded$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.removePageItem),
        filter(action => action.block.type !== 'settings'),
        withLatestFrom(this.rootStore$.select(fromRoot.getPrimaryFrameId)),
        tap(([action, frameId]) => this.preview.removeBlock(action.block, frameId))
    ), { dispatch: false });

    toggleItemVisibility$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.toggleItemVisibility),
        withLatestFrom(this.rootStore$.select(fromRoot.getPrimaryFrameId)),
        tap(([action, frameId]) => {
            if (action.block.hidden) {
                this.preview.hide(action.block, frameId);
            } else {
                this.preview.show(action.block, frameId);
            }
        })
    ), { dispatch: false });

    reloadPageInBackground$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.loadPageSuccess),
        withLatestFrom(this.rootStore$.select(fromRoot.getSecondaryFrameId)),
        switchMap(([, frameId]) => of(rootActions.previewReady({ frameId })))
    ));

    timeoutToError$ = createEffect(() => this.actions$.pipe(
        ofType(rootActions.reloadPreview),
        switchMap(() => timer(AppSettings.previewTimeout).pipe(
            map(() => rootActions.checkPreviewLoadedOrError())
        )),
    ));

    sendPageToStore$ = createEffect(() => this.actions$.pipe(
        ofType(rootActions.previewReady),
        withLatestFrom(
            this.editorStore$.select(fromEditor.getPageForEdit),
            this.rootStore$.select(fromRoot.getPrimaryIsLoaded),
            this.rootStore$.select(fromRoot.getSecondaryIsLoaded),
            this.rootStore$.select(fromRoot.getSecondaryFrameId),
            this.themeStore$.select(fromTheme.getDraftUploaded),
            this.themeStore$.select(fromTheme.getPresetsNotLoaded)
        ),
        filter(([action, page, primaryLoaded, secondaryLoaded, secondaryFrameId, draftUploaded, themeNotLoaded]) =>
            primaryLoaded && secondaryLoaded
            && action.payload === secondaryFrameId
            && (draftUploaded || themeNotLoaded) && page != null),
        switchMap(([action, page]) => {
            this.preview.page(page.content, action.payload);
            return of(rootActions.previewLoading({ isLoading: true, msg: 'preview ready' }));
        })
    ));

    toggleFrames$ = createEffect(() => this.actions$.pipe(
        ofType(rootActions.toggleFrames),
        withLatestFrom(
            this.rootStore$.select(fromRoot.getPrimaryFrameId),
            this.rootStore$.select(fromRoot.getSecondaryFrameId),
        ),
        map(([, primaryFrameId, secondaryFrameId]): [string, string] => [
            secondaryFrameId || primaryFrameId,
            primaryFrameId
        ]),
        tap(([primary, secondary]) => this.preview.toggleFrames(primary, secondary))
    ), { dispatch: false });

    openBlockEditorForPreview$ = createEffect(() => fromEvent(window, 'message').pipe(
        map((event: MessageEvent) => event.data),
        filter(data => data.type === 'select'),
        withLatestFrom(
            this.themeStore$.select(fromTheme.getCurrentThemeSchemaItem),
            this.themeStore$.select(fromTheme.getShowPresetsEditor)
        ),
        filter(([, schemaItem, showPresets]) => !schemaItem && !showPresets),
        switchMap(([data]) => {
            return [editorActions.completeEditPageItem(), editorActions.selectPageItem({ blockId: data.id })];
        }),
    ));

    deselectBlockInPreview$ = createEffect(() => fromEvent(window, 'message').pipe(
        map((event: MessageEvent) => event.data),
        filter(data => data.type === 'select'),
        withLatestFrom(
            this.rootStore$.select(fromRoot.getPrimaryFrameId),
            this.themeStore$.select(fromTheme.getCurrentThemeSchemaItem),
            this.themeStore$.select(fromTheme.getShowPresetsEditor)
        ),
        filter(([, , schemaItem, showPresets]) => !!schemaItem || !!showPresets),
        tap(([, frameId]) => this.preview.selectBlock(0, frameId))
    ), { dispatch: false });

    reorderBlocksMessage$ = createEffect(() => fromEvent(window, 'message').pipe(
        map((event: MessageEvent) => event.data),
        filter(data => data.type === 'move'),
        map(data => {
            return editorActions.moveBlock({ previousIndex: data.oldIndex, currentIndex: data.newIndex });
        }),
    ));

    receiveSwapFrameMessage$ = createEffect(() => fromEvent(window, 'message').pipe(
        filter((event: MessageEvent) => event.data.type === 'render-complete'),
        withLatestFrom(
            this.rootStore$.select(fromRoot.getPrimaryFrameId),
            this.rootStore$.select(fromRoot.getSecondaryFrameId)
        ),
        map(([event, primaryFrameId, secondaryFrameId]): [Window, Window, string, string] => [
            (<HTMLIFrameElement>document.getElementById(primaryFrameId)).contentWindow,
            <Window>event.source,
            primaryFrameId, secondaryFrameId
        ]),
        map(([primary, source, primaryFrameId, secondaryFrameId]) => primary === source ? primaryFrameId : secondaryFrameId),
        tap(x => console.log('toggle frames', x)),
        switchMap(loadedFrameId => [
            rootActions.toggleFrames({ frameId: loadedFrameId }),
            rootActions.previewLoading({ isLoading: false, msg: 'swap frames' })
        ])
    ));

    receiveSwapBlocksMessage$ = createEffect(() => fromEvent(window, 'message').pipe(
        filter((event: MessageEvent) => event.data.type === 'swap'),
        map(event => editorActions.swapBlocks({
            currentIndex: event.data.content.newIndex,
            previousIndex: event.data.content.currentIndex
        }))
    ));

    receiveHoverElementMessage$ = createEffect(() => fromEvent(window, 'message').pipe(
        filter((event: MessageEvent) => event.data.type === 'hover'),
        map(event => editorActions.markSectionHoveredInPreview({ blockId: event.data.id }))
    ));

    sendCloneToPreview$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.clonePageItem),
        withLatestFrom(this.rootStore$.select(fromRoot.getPrimaryFrameId)),
        tap(([action, primaryFrameId]) => {
            this.preview.cloneBlock(action.originalBlock.id, action.newBlock.id, primaryFrameId);
        })
    ), { dispatch: false });
}
