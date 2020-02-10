import { Injectable } from '@angular/core';
import { of } from 'rxjs';
import { map, switchMapTo, tap } from 'rxjs/operators';
import {
    catchError,
    mergeMap,
    switchMap,
    withLatestFrom
} from 'rxjs/operators';
import { Store } from '@ngrx/store';
import { Actions, ofType, createEffect } from '@ngrx/effects';

import { MessageService } from '@shared/services';
import { BlockValuesModel } from '@shared/models';
import { PageModel } from '@editor/models';
import { BlocksService, PagesService } from '@editor/services';

import * as editorActions from './editor.actions';
import * as fromEditor from '.';

// import { CategoryModel } from '../models';

@Injectable()
export class EditorEffects {
    constructor(private pages: PagesService,
        // private catalog: CatalogService,
        private blocks: BlocksService,
        private messages: MessageService,
        private actions$: Actions, private store$: Store<fromEditor.State>) { }

    convertPageTypeToPreviewSection$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.previewPageItemOfType),
        map(action => {
            if (!!action.blockSchema) {
                const result = <BlockValuesModel>{};
                const schema = action.blockSchema;
                schema.settings.forEach(x => result[x.id] = x['preview'] || x['default'] || null);
                result.type = action.blockSchema.type;
                return result;
            }
            return null;
        }),
        mergeMap(item =>
            of(editorActions.previewPageItem({ block: item }))
        )
    ));

    closeEditors$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.closeEditors),
        switchMapTo([
            editorActions.completeEditPageItem(),
            editorActions.previewPageItemOfType({ blockSchema: null }),
            editorActions.toggleNewBlockPane({ display: false })
        ])
    ));

    copyBlock$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.copyPageItem),
        withLatestFrom(this.store$.select(fromEditor.getPage)),
        map(([action, page]) => {
            const block = { ...action.sourceBlock };
            block.id = page.content.reduce((v: number, b: BlockValuesModel) => Math.max(b.id, v), 0) + 1;
            return editorActions.clonePageItem({ originalBlock: action.sourceBlock, newBlock: block });
        })
    ));

    createPageItemModelByType$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.createPageItem),
        withLatestFrom(this.store$.select(fromEditor.getPage)),
        map(([action, page]) => {
            const block = <BlockValuesModel>{
                id: page.content.length ? Math.max(...page.content.map(v => v.id || 0)) + 1 : 1,
                type: action.newItemSchema.type
            };
            action.newItemSchema.settings.forEach(x => block[x.id] = x['default'] || null);
            return block;
        }),
        mergeMap(item =>
            of(editorActions.addPageItem({ block: item }))
        )
    ));

    loadBlockTypes$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.loadBlocksSchema),
        switchMap(() =>
            this.blocks.load().pipe(
                map(result => editorActions.blocksSchemaLoaded({ schema: result })),
                catchError(error => of(editorActions.blocksSchemaFail({ error })))
            )
        )
    ));

    loadPage$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.loadPage),
        switchMap(() =>
            this.pages.downloadPage().pipe(
                map(page => editorActions.loadPageSuccess({ page })),
                catchError(error => of(editorActions.loadPageFail({ error })))
            )
        )
    ));

    savePage$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.savePage),
        map(() => editorActions.reloadPage())
    ));

    reloadPage$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.reloadPage),
        switchMap(() => this.pages.downloadPage().pipe(
            map(page => editorActions.reloadPageSuccess({ page })),
            catchError(error => of(editorActions.reloadPageFail({ error })))
        ))
    ));

    uploadPage$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.reloadPageFail, editorActions.reloadPageSuccess),
        withLatestFrom(this.store$.select(fromEditor.getPage)),
        switchMap(([action, page]: [any, PageModel]) => {
            const settings = action.payload.settings || page.settings;
            const data = [settings, ...page.content];
            return this.pages.uploadPage(data).pipe(
                map(() => editorActions.savePageSuccess()),
                catchError(error => of(editorActions.savePageFail({ error })))
            );
        })
    ));

    pageSaved$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.savePageSuccess),
        tap(() => {
            this.messages.displayMessage('Page saved successfully');
        })
    ), { dispatch: true });

    pageSaveFailed$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.savePageFail),
        tap((action) => {
            this.messages.displayError('Couldn\'t save page', action.error);
        })
    ), { dispatch: false });
}
