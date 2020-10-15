import { Injectable } from '@angular/core';
import { Actions, ofType, createEffect } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { of, fromEvent, timer } from 'rxjs';
import {
    switchMapTo, debounceTime, distinctUntilChanged,
    withLatestFrom, tap, filter, map, switchMap, mapTo
} from 'rxjs/operators';
import { PreviewService, ApiUrlsService, AppSettings } from '@app/services';
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
        private appSettings: AppSettings,
        private store$: Store) { }

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
            this.store$.select(fromEditor.getPage),
            this.store$.select(fromEditor.getPageNotLoaded)
        ),
        filter(([, page, pageNotLoaded]) => !page || pageNotLoaded),
        mapTo(editorActions.loadBlocks())
    ));

    switchToLoadBlocks$ = createEffect(() => this.actions$.pipe(
        ofType(rootActions.loadData),
        withLatestFrom(
            this.store$.select(fromEditor.getBlocksSchema),
            this.store$.select(fromEditor.getSchemaNotLoaded)
        ),
        filter(([, blocksSchema, schemaNotLoaded]) => !blocksSchema || schemaNotLoaded),
        mapTo(editorActions.loadBlocksSchema())
    ));

    switchToLoadThemes$ = createEffect(() => this.actions$.pipe(
        ofType(rootActions.loadData),
        withLatestFrom(
            this.store$.select(fromTheme.getPresets),
            this.store$.select(fromTheme.getPresetsNotLoaded)
        ),
        filter(([, presets, presetsNotLoaded]) => !presets || presetsNotLoaded),
        mapTo(themeActions.loadDefaultThemes())
    ));

    switchToLoadThemeSchema$ = createEffect(() => this.actions$.pipe(
        ofType(rootActions.loadData),
        withLatestFrom(
            this.store$.select(fromTheme.getSchema),
            this.store$.select(fromTheme.getSchemaNotLoaded)
        ),
        filter(([, schema, schemaNotLoaded]) => !schema || schemaNotLoaded),
        mapTo(themeActions.loadSchema())
    ));

    saveData$ = createEffect(() => this.actions$.pipe(
        ofType(rootActions.saveData),
        switchMapTo([
            editorActions.saveBlocks(),
            themeActions.saveTheme()
        ])
    ));

    setPreviewUrl = createEffect(() => this.actions$.pipe(
        ofType(themeActions.updateDraftSuccess, editorActions.loadBlocksSuccess),
        withLatestFrom(
            this.store$.select(fromRoot.getPreviewUrl),
            this.store$.select(fromEditor.getPageNotLoaded),
            this.store$.select(fromEditor.getPageLayout),
            this.store$.select(fromTheme.getPresetsNotLoaded),
            this.store$.select(fromTheme.getDraftUploaded)
        ),
        filter(([, url, pageNotLoaded, , presetsNotLoaded, draftUploaded]) => !url && !pageNotLoaded && !presetsNotLoaded && draftUploaded),
        switchMap(([, , , layout]) => {
            if (!!this.appSettings.storeBaseUrl) {
                const result = this.urls.getStoreUrl(layout);
                return [rootActions.setPreviewUrl({ url: result }), rootActions.reloadPreview()];
            }
            return [rootActions.displayError({ error: 'Store url is not defined' })];
        })
    ));

    previewFailed$ = createEffect(() => this.actions$.pipe(
        ofType(rootActions.previewError),
        tap(action => {
            console.log(action.error);
        })
    ), { dispatch: false });

    previewFailedByTimeout$ = createEffect(() => this.actions$.pipe(
        ofType(rootActions.checkPreviewLoadedOrError),
        withLatestFrom(
            this.store$.select(fromRoot.getPrimaryIsLoaded),
            this.store$.select(fromRoot.getSecondaryIsLoaded),
            this.store$.select(fromRoot.getPreviewLoading)
        ),
        filter(([, primaryLoaded, secondaryLoaded, isLoadingStill]) => (!primaryLoaded && !secondaryLoaded) || isLoadingStill),
        map(() => rootActions.previewError({ error: 'timeoutError' }))
    ));

    // // themes

    uploadPreviewPreset$ = createEffect(() => this.actions$.pipe(
        ofType(themeActions.updateDraftSuccess),
        withLatestFrom(
            this.store$.select(fromRoot.getSecondaryFrameId),
            this.store$.select(fromRoot.getSecondaryIsLoaded)
        ),
        switchMap(([, frameId, previewReady]) => {
            if (previewReady) {
                this.preview.reload(frameId);
            }
            return of(rootActions.previewLoading({ isLoading: true, msg: 'update draft success' }));
        })
    ));

    settingsFromStorefront$ = createEffect(() => fromEvent(window, 'message').pipe(
        filter((event: MessageEvent) => event.data.type === 'settings'),
        map(event => themeActions.loadEffectiveThemeValuesSuccess({ values: event.data.model }))
    ));

    loadEffectiveThemeValues$ = createEffect(() => this.actions$.pipe(
        ofType(themeActions.loadEffectiveThemeValues, rootActions.previewReady),
        withLatestFrom(
            this.store$.select(fromTheme.getIsEffectiveValuesSkipped),
            this.store$.select(fromTheme.getEditablePreset),
            this.store$.select(fromTheme.getCurrentThemeValuesRequested),
            this.store$.select(fromRoot.getPrimaryFrameId),
            this.store$.select(fromRoot.getSecondaryFrameId)
        ),
        filter(([, skip, editableTheme, themeRequested, primaryFrameId, secondaryFrameId]) =>
            !skip && !themeRequested && !!editableTheme && (!!primaryFrameId || !!secondaryFrameId)),
        map(([, , , , primaryFrameId, secondaryFrameId]) => {
            this.preview.requestSettings(primaryFrameId || secondaryFrameId);
            return themeActions.loadEffectiveThemeValuesRequested();
        })
    ));

    loadEffectiveThemeValuesRequested$ = createEffect(() => this.actions$.pipe(
        ofType(themeActions.loadEffectiveThemeValuesRequested),
        switchMap(() => timer(this.appSettings.previewTimeout).pipe(
            withLatestFrom(this.store$.select(fromTheme.getIsEffectiveValuesSkipped)),
            filter(([, skipped]) => !skipped),
            map(() => themeActions.loadEffectiveThemeValuesSkippedByTimeout())
        )),
    ));

    // // editor

    sendHoverToPreview$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.highlightInPreview),
        withLatestFrom(
            this.store$.select(fromRoot.getPrimaryFrameId),
            this.store$.select(fromRoot.getPrimaryIsLoaded)
        ),
        tap(([action, frameId, previewReady]) => previewReady && this.preview.hover(action.block, frameId))
    ), { dispatch: false });

    sendPreviewPageItem$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.previewPageItem),
        withLatestFrom(
            this.store$.select(fromRoot.getPrimaryFrameId),
            this.store$.select(fromRoot.getPrimaryIsLoaded)
        ),
        tap(([action, frameId, previewReady]) => previewReady && this.preview.preview(action.block, frameId))
    ), { dispatch: false });

    sendNewBlockToStoreLoaded$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.addPageItem),
        withLatestFrom(
            this.store$.select(fromRoot.getPrimaryFrameId),
            this.store$.select(fromRoot.getPrimaryIsLoaded)
        ),
        tap(([action, frameId, previewReady]) => previewReady && this.preview.add(action.block, frameId))
    ), { dispatch: false });

    scrollPreviewToObject$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.selectPageItem),
        filter(action => !!action.blockId),
        withLatestFrom(this.store$.select(fromRoot.getPrimaryFrameId)),
        tap(([action, frameId]) => {
            this.preview.selectBlock(action.blockId, frameId);
        })
    ), { dispatch: false });

    deselectObject$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.completeEditPageItem),
        withLatestFrom(this.store$.select(fromRoot.getPrimaryFrameId)),
        tap(([, frameId]) => {
            this.preview.selectBlock(0, frameId);
        })
    ), { dispatch: false });

    sendUpdatedBlockToStoreLoaded$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.updateBlockPreview),
        withLatestFrom(
            this.store$.select(fromRoot.getPrimaryFrameId),
            this.store$.select(fromRoot.getPrimaryIsLoaded),
            this.store$.select(fromEditor.getCurrentSectionItem),
            this.store$.select(fromEditor.getBlocksSchema)
        ),
        filter(([, , previewReady, currentItem, schema]) => previewReady && !schema[currentItem.type].static),
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
        withLatestFrom(this.store$.select(fromRoot.getPrimaryFrameId)),
        tap(([action, frameId]) =>
            this.preview.changeOrder(action.previousIndex, action.currentIndex, frameId))
    ), { dispatch: false });

    sendRemoveBlockToStoreLoaded$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.removePageItem),
        filter(action => action.block.type !== 'settings'),
        withLatestFrom(this.store$.select(fromRoot.getPrimaryFrameId)),
        tap(([action, frameId]) => this.preview.removeBlock(action.block, frameId))
    ), { dispatch: false });

    toggleItemVisibility$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.toggleItemVisibility),
        withLatestFrom(
            this.store$.select(fromRoot.getPrimaryFrameId),
            this.store$.select(fromEditor.getPage)
        ),
        tap(([action, frameId, page]) => {
            const block = page.content.find(x => x.id === action.block.id);
            if (block.hidden) {
                this.preview.hide(action.block, frameId);
            } else {
                this.preview.show(action.block, frameId);
            }
        })
    ), { dispatch: false });

    reloadPageInBackground$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.loadBlocksSuccess),
        withLatestFrom(this.store$.select(fromRoot.getSecondaryFrameId)),
        switchMap(([, frameId]) => of(rootActions.previewReady({ frameId })))
    ));

    timeoutToError$ = createEffect(() => this.actions$.pipe(
        ofType(rootActions.reloadPreview),
        switchMap(() => timer(this.appSettings.previewTimeout).pipe(
            map(() => rootActions.checkPreviewLoadedOrError())
        )),
    ));

    sendPageToStore$ = createEffect(() => this.actions$.pipe(
        ofType(rootActions.previewReady),
        withLatestFrom(
            this.store$.select(fromEditor.getPageForEdit),
            this.store$.select(fromRoot.getPrimaryIsLoaded),
            this.store$.select(fromRoot.getSecondaryIsLoaded),
            this.store$.select(fromRoot.getSecondaryFrameId),
            this.store$.select(fromTheme.getDraftUploaded),
            this.store$.select(fromTheme.getPresetsNotLoaded)
        ),
        filter(([action, page, primaryLoaded, secondaryLoaded, secondaryFrameId, draftUploaded, themeNotLoaded]) =>
            primaryLoaded && secondaryLoaded
            && action.frameId === secondaryFrameId
            && (draftUploaded || themeNotLoaded) && page != null),
        switchMap(([action, page]) => {
            this.preview.page(page.content, action.frameId);
            return of(rootActions.previewLoading({ isLoading: true, msg: 'preview ready' }));
        })
    ));

    toggleFrames$ = createEffect(() => this.actions$.pipe(
        ofType(rootActions.toggleFrames),
        withLatestFrom(
            this.store$.select(fromRoot.getPrimaryFrameId),
            this.store$.select(fromRoot.getSecondaryFrameId),
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
            this.store$.select(fromTheme.getCurrentThemeSchemaItem),
            this.store$.select(fromTheme.getShowPresetsEditor)
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
            this.store$.select(fromRoot.getPrimaryFrameId),
            this.store$.select(fromTheme.getCurrentThemeSchemaItem),
            this.store$.select(fromTheme.getShowPresetsEditor)
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
            this.store$.select(fromRoot.getPrimaryFrameId),
            this.store$.select(fromRoot.getSecondaryFrameId)
        ),
        map(([event, primaryFrameId, secondaryFrameId]): [Window, Window, string, string] => [
            (<HTMLIFrameElement>document.getElementById(primaryFrameId)).contentWindow,
            <Window>event.source,
            primaryFrameId, secondaryFrameId
        ]),
        map(([primary, source, primaryFrameId, secondaryFrameId]) => primary === source ? primaryFrameId : secondaryFrameId),
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

    receiveRefreshFrameMessage$ = createEffect(() => fromEvent(window, 'message').pipe(
        filter((event: MessageEvent) => event.data.type === 'refresh'),
        withLatestFrom(this.store$.select(fromRoot.getSecondaryFrameId)),
        tap(([, frameId]) => {
            this.preview.reload(frameId);
        })
    ), { dispatch: false });

    receiveShowErrorFrameMessage$ = createEffect(() => fromEvent(window, 'message').pipe(
        filter((event: MessageEvent) => event.data.type === 'info'),
        map(event => rootActions.displayError({ error: event.data.msg }))
    ));

    sendCloneToPreview$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.clonePageItem),
        withLatestFrom(this.store$.select(fromRoot.getPrimaryFrameId)),
        tap(([action, primaryFrameId]) => {
            this.preview.cloneBlock(action.originalBlock.id, action.newBlock.id, primaryFrameId);
        })
    ), { dispatch: false });
}
