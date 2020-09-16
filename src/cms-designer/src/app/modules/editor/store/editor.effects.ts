import { Injectable } from '@angular/core';
import { of } from 'rxjs';
import { map, switchMapTo, tap, filter, exhaustMap } from 'rxjs/operators';
import {
    catchError,
    mergeMap,
    switchMap,
    withLatestFrom
} from 'rxjs/operators';
import { Store } from '@ngrx/store';
import { Actions, ofType, createEffect } from '@ngrx/effects';

import { MessageService, ClipboardService } from '@shared/services';
import { BlockValuesModel } from '@shared/models';
import { BlocksService, PagesService } from '@editor/services';

import * as editorActions from './editor.actions';
import * as fromEditor from '.';
import * as rootActions from '@app/store/root.actions';
import { generateUniqueString, onlyLettersAndDigits } from '@app/services/utils';
import { AppSettings } from '@app/services';

// import { CategoryModel } from '../models';

@Injectable()
export class EditorEffects {
    constructor(private pages: PagesService,
        // private catalog: CatalogService,
        private blocks: BlocksService,
        private messages: MessageService,
        private clipboard: ClipboardService,
        private appSettings: AppSettings,
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
            block.__id = this.generateBlockId(block);
            action.newItemSchema.settings.forEach(x => block[x.id] = x['default'] || null);
            return block;
        }),
        mergeMap(item =>
            of(editorActions.addPageItem({ block: item }))
        )
    ));

    loadBlockTypes$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.loadBlocksSchema),
        filter(() => !!this.appSettings.path),
        switchMap(() =>
            this.blocks.load().pipe(
                map(result => editorActions.blocksSchemaLoaded({ schema: result })),
                catchError(error => of(editorActions.blocksSchemaFail({ error })))
            )
        )
    ));

    loadBlocks$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.loadBlocks),
        filter(() => !!this.appSettings.path),
        switchMap(() =>
            this.pages.downloadPage().pipe(
                tap(blocks => blocks.forEach(b => {
                    b.__id = this.generateBlockId(b);
                })),
                map(blocks => editorActions.loadBlocksSuccess({ blocks })),
                catchError(error => of(editorActions.loadBlocksFail({ error })))
            )
        )
    ));

    saveBlocks$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.saveBlocks),
        withLatestFrom(this.store$.select(fromEditor.getIsDirty)),
        filter(([, dirty]) => dirty),
        map(() => editorActions.reloadBlocks())
    ));

    reloadBlocks$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.reloadBlocks),
        switchMap(() => this.pages.downloadPage().pipe(
            map(blocks => editorActions.reloadBlocksSuccess({ blocks })),
            catchError(error => of(editorActions.reloadBlocksFail({ error })))
        ))
    ));

    uploadPage$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.reloadBlocksSuccess),
        withLatestFrom(
            this.store$.select(fromEditor.getPage),
            this.store$.select(fromEditor.getBlocksSchema)
        ),
        switchMap(([{ blocks }, page, schema]) => {
            const settings = { ...(blocks.find(x => x.type === 'settings') || page.settings) };
            Object.keys(schema)
                .filter(key => schema[key].static && (typeof schema[key].static === 'string'))
                .forEach(key => {
                    schema[key].settings.forEach(s => {
                        settings[s.id] = page.settings[s.id];
                    });
                })
            const data = [settings, ...page.content];
            return this.pages.uploadPage(data).pipe(
                map(() => editorActions.saveBlocksSuccess()),
                catchError(error => of(editorActions.saveBlocksFail({ error })))
            );
        })
    ));

    pageSaved$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.saveBlocksSuccess),
        tap(() => {
            this.messages.displayMessage('Page saved successfully');
        })
    ), { dispatch: false });

    pageSaveFailed$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.saveBlocksFail),
        tap((action) => {
            this.messages.displayError('Couldn\'t save page', action.error);
        })
    ), { dispatch: false });

    copyToClipboard$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.copyToClipboard),
        tap(({ block }) => {
            const value = {...block, __id: null};
            this.clipboard.copyTo(value);
        })
    ), { dispatch: false });

    pasteFromClipboard$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.tryPasteFromClipboard),
        switchMap(() => this.clipboard.pasteFrom()),
        map(pasteResult => {
            if (pasteResult.success) {
                return editorActions.tryPasteFromString({ value: pasteResult.data });
            } else {
                return editorActions.showPastePopup();
            }
        })
    ));

    showPastePopup$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.showPastePopup),
        exhaustMap(() => {
            return this.clipboard.pasteThroughPopup();
        }),
        filter(result => result.success),
        map(result => editorActions.tryPasteFromString({ value: result.data }))
    ));

    tryPasteFromString$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.tryPasteFromString),
        withLatestFrom(
            this.store$.select(fromEditor.getPage),
            this.store$.select(fromEditor.getBlocksSchema)
        ),
        map(([{ value }, page, schema]) => {
            if (!value) {
                return rootActions.emptyAction();
            }
            try {
                const block = JSON.parse(value);
                if (!schema[block.type] || schema[block.type].static) {
                    this.messages.displayError('Unknown or unsupported block type', {});
                    return editorActions.showPastePopup();
                }
                block.id = page.content.reduce((v: number, b: BlockValuesModel) => Math.max(b.id, v), 0) + 1;
                block.__id = null;
                block.__id = this.generateBlockId(block);
                return editorActions.addPageItem({ block });
            } catch (error) {
                this.messages.displayError('Parse data error', error);
                return editorActions.showPastePopup();
            }
        })
    ));

    private generateBlockId(block: BlockValuesModel): string {
        if (block.__id) {
            return block.__id;
        }
        return onlyLettersAndDigits(`${block.type}${generateUniqueString(4)}`);
    }
}
