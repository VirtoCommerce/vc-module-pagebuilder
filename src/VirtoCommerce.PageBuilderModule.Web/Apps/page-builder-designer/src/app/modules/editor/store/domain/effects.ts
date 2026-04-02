import { Injectable, inject } from "@angular/core";
import { Actions, createEffect, ofType } from "@ngrx/effects";
import { Store } from "@ngrx/store";
import { from } from 'rxjs';
import {
    withLatestFrom,
    filter,
    map,
    switchMap,
    tap
} from "rxjs/operators";

import { ModalService, EventsBusService, ClipboardService } from '@core/services';
import { appHelpers } from "@integration/helpers";
import * as sharedActions from "@shared/store/actions";
import * as sharedSelectors from "@shared/store/selectors";

import { PasteContentComponent } from '@editor/dialogs';
import { helpers as editorHelpers, clipboardHelpers } from '@editor/helpers';

import { BuilderState } from "../state";
import * as actions from "../actions";
import * as selectors from "../selectors";
import { EvaluatorService } from "@app/modules/integration/services";
import { SectionModel } from "@app/modules/models";

@Injectable({
    providedIn: 'root'
})
export class TemplateEditorDomainEffects {
    private readonly store$ = inject(Store<BuilderState>);
    private readonly actions$ = inject(Actions);
    private readonly clipboard = inject(ClipboardService);
    private readonly modals = inject(ModalService);
    private readonly eventsBus = inject(EventsBusService);
    private readonly evaluator = inject(EvaluatorService);

    addItem$ = createEffect(() => this.actions$.pipe(
        ofType(actions.addItemAction),
        withLatestFrom(
            this.store$.select(selectors.changeTemplateContext),
            this.store$.select(selectors.selectSharedSchemas),
            this.store$.select(selectors.selectObjectsSchemas)
        ),
        filter(([, { template }]) => !!template),
        switchMap(([{ schema }, { template, section, templateKey, insertIndex, templateEntry }, shared, objects]) => {

            const sharedSchemaName = !!section ? "_blocks" : "_sections";

            const fullSchema = editorHelpers.prepareSchema(schema, shared, objects, sharedSchemaName, templateEntry?.controls);
            const result = editorHelpers.addItemToTemplate(fullSchema, template!, section || null, insertIndex); // section can be null
            const message = sharedActions.broadcastPreviewMessage({
                msg: {
                    type: 'add',
                    template: result.template,
                    section: result.template.content.find(x => x.id === result.sectionId),
                    sectionId: result.sectionId,
                    index: insertIndex
                }
            });
            return [
                actions.updateTemplateAction({
                    template: result.template,
                    templateKey
                }),
                actions.closeAddItemPanel(),
                result.blockId
                    ? actions.editBlockAction({ blockId: result.blockId, sectionId: result.sectionId })
                    : actions.editSectionAction({ sectionId: result.sectionId }),
                message,
            ]
        })
    ));

    updateEditableModel$ = createEffect(() => this.actions$.pipe(
        ofType(actions.sectionChangedAction),
        withLatestFrom(this.store$.select(selectors.changeTemplateContext)),
        filter(([, { template, sectionId }]) => !!template && !!sectionId),
        switchMap(([{ changes }, { template, templateKey, sectionId, blockId }]) => [
            actions.updateTemplateAction({
                template: blockId
                    ? editorHelpers.applyBlockChanges(template!, changes, sectionId, blockId)
                    : editorHelpers.applySectionChanges(template!, changes, sectionId),
                templateKey
            }),
        ])
    ));

    updateTemplateSettings$ = createEffect(() => this.actions$.pipe(
        ofType(actions.sectionChangedAction),
        withLatestFrom(this.store$.select(selectors.changeTemplateContext)),
        filter(([, { template, sectionId }]) => !!template && !sectionId),
        switchMap(([{ changes }, { template, templateKey }]) => [
            actions.updateTemplateAction({
                template: editorHelpers.applySettingsChanges(template!, changes),
                templateKey
            }),
        ])
    ));

    refreshPreviewAction$ = createEffect(() => this.actions$.pipe(
        ofType(actions.executeContextMenuAction),
        filter(x => x.action === 'refresh-preview'),
        map(() => actions.refreshPreview())
    ));

    refreshPreview$ = createEffect(() => this.actions$.pipe(
        ofType(actions.refreshPreview),
        withLatestFrom(this.store$.select(selectors.changeTemplateContext)),
        filter(([, { template }]) => !!template),
        switchMap(([, { template }]) => [
            sharedActions.broadcastPreviewMessage({
                msg: {
                    type: 'reload',
                    template: template
                }
            }),
        ])
    ));

    showItem$ = createEffect(() => this.actions$.pipe(
        ofType(actions.executeContextMenuAction),
        filter(x => x.action === 'show' || x.action === 'hide'),
        withLatestFrom(this.store$.select(selectors.changeTemplateContext)),
        filter(([, { template }]) => !!template),
        map(([{ action, section, block }, { template, templateKey, sectionId, blockId }]) => [
            action, template, templateKey, sectionId || section?.id, blockId || block?.id
        ]),
        switchMap(([action, template, templateKey, sectionId, blockId]) => [
            actions.updateTemplateAction({
                template: blockId
                    ? editorHelpers.applyBlockChanges(template!, { hidden: action === 'hide' }, sectionId, blockId)
                    : editorHelpers.applySectionChanges(template!, { hidden: action === 'hide' }, sectionId),
                templateKey
            }),
            sharedActions.broadcastPreviewMessage({
                msg: {
                    type: action,
                    template: template!,
                    section: template?.content.find((x: SectionModel) => x.id == sectionId),
                    sectionId
                }
            })
        ])
    ));

    duplicateItem$ = createEffect(() => this.actions$.pipe(
        ofType(actions.executeContextMenuAction),
        filter(x => x.action === 'duplicate'),
        withLatestFrom(this.store$.select(selectors.changeTemplateContext)),
        filter(([, { template }]) => !!template),
        map(([{ section, block, source }, { template, templateKey, sectionId, blockId }]) => [
            source, template, templateKey, sectionId || section?.id, blockId || block?.id
        ]),
        switchMap(([source, template, templateKey, sectionId, blockId]) => {
            const changedTemplate = blockId
                ? editorHelpers.duplicateBlock(template!, sectionId, blockId)
                : editorHelpers.duplicateSection(template!, sectionId);
            const message = sharedActions.broadcastPreviewMessage({
                msg: blockId
                    ? {
                        type: 'changed',
                        template: changedTemplate.template,
                        section: changedTemplate.template.content.find(x => x.id === changedTemplate.sectionId),
                        sectionId: changedTemplate.sectionId
                    }
                    : {
                        type: 'add',
                        template: changedTemplate.template,
                        section: changedTemplate.template.content.find(x => x.id === changedTemplate.sectionId),
                        sectionId: changedTemplate.sectionId,
                        index: changedTemplate.template.content.findIndex(x => x.id === changedTemplate.sectionId),
                    }
            });
            return [
                message,
                actions.updateTemplateAction({
                    template: changedTemplate.template,
                    templateKey
                }),
                sharedActions.showNotification({
                    message: changedTemplate.blockId ? 'Block duplicated' : 'Section duplicated',
                    msgType: 'info'
                }),
                ...source === 'editor' ? [
                    changedTemplate.blockId
                        ? actions.editBlockAction({ sectionId: changedTemplate.sectionId, blockId: changedTemplate.blockId })
                        : actions.editSectionAction({ sectionId: changedTemplate.sectionId })
                ] : []
            ]
        })
    ));

    copyItemToClipboard$ = createEffect(() => this.actions$.pipe(
        ofType(actions.executeContextMenuAction),
        filter(x => x.action === 'copy'),
        tap(({ section, block }) => {
            this.clipboard.copy({
                content: { ...(block || section) },
                type: block ? 'block' : 'section'
            });
        }),
        map(() => sharedActions.showNotification({
            message: 'Copied to clipboard',
            msgType: 'info'
        }))
    ));

    pasteItemFromClipboardAction$ = createEffect(() => this.actions$.pipe(
        ofType(actions.executeContextMenuAction),
        filter(x => x.action === 'paste-after'
            || x.action === 'paste-before'
            || x.action === 'paste-block'
            || x.action === 'paste-section'),
        withLatestFrom(this.store$.select(selectors.changeTemplateContext)),
        filter(([, { template }]) => !!template),
        switchMap(([{ section, block, action, source }]) =>
            from(this.clipboard.getData()).pipe(
                filter(x => !!x),
                map(data => actions.pasteFromClipboard({ value: data!, section, block, action, source }))
            )
        )
    ));

    pasteFromClipboardAction$ = createEffect(() => this.actions$.pipe(
        ofType(actions.pasteFromClipboard),
        withLatestFrom(this.store$.select(selectors.changeTemplateContext)),
        switchMap(([action, context]) => {
            return clipboardHelpers.pasteDataIntoTemplate(action, context);
        })
    ));

    showClipboardModal$ = createEffect(() => this.actions$.pipe(
        ofType(actions.showClipboardModal),
        switchMap(action => {
            return this.modals.show<{ accept: boolean, value: string }>(PasteContentComponent, {
                data: {
                    clipboardData: action.value.sourceContent
                }
            }).pipe(
                map((result) => result?.accept
                    ? actions.pasteFromClipboard({
                        ...action,
                        value: {
                            ...action.value,
                            wrongData: undefined,
                            sourceContent: result.value,
                            ...appHelpers.tryParseJson(result.value)
                        }
                    })
                    : sharedActions.empty()
                )
            );
        })
    ));

    deleteSectionOrBlock$ = createEffect(() => this.actions$.pipe(
        ofType(actions.executeContextMenuAction),
        filter(({ action }) => action === 'delete'),
        withLatestFrom(this.store$.select(selectors.changeTemplateContext)),
        filter(([, template]) => !!template),
        map(([{ action, section, block }, { template, templateKey, sectionId, blockId }]) => [
            action, template, templateKey, sectionId || section?.id, blockId || block?.id
        ]),
        switchMap(([, template, templateKey, sectionId, blockId]) =>
            this.modals.confirm('Are you sure you want to delete this item?').pipe(
                switchMap(confirmed => {
                    const newTemplate = blockId
                        ? editorHelpers.removeBlock(template!, sectionId, blockId)
                        : editorHelpers.removeSection(template!, sectionId);
                    return confirmed
                        ? [
                            actions.updateTemplateAction({
                                template: newTemplate,
                                templateKey
                            }),
                            sharedActions.broadcastPreviewMessage({
                                msg: {
                                    type: blockId ? 'update' : 'remove',
                                    sectionId: sectionId,
                                    section: newTemplate.content.find(x => x.id == sectionId),
                                    template: newTemplate,
                                }
                            }),
                            actions.closeAddItemPanel()
                        ]
                        : [sharedActions.empty()]
                })
            ))
    ));

    deleteSelected$ = createEffect(() => this.actions$.pipe(
        ofType(actions.executeContextMenuAction),
        filter(({ action }) => action === 'delete-selected'),
        withLatestFrom(
            this.store$.select(selectors.changeTemplateContext),
            this.store$.select(selectors.selectCheckedItems)
        ),
        filter(([, { template }, checkedItems]) => !!template && checkedItems.length > 0),
        switchMap(([, { template, templateKey }, checkedItems]) =>
            this.modals.confirm(`Are you sure you want to delete ${checkedItems.length} selected item(s)?`).pipe(
                switchMap(confirmed => {
                    if (!confirmed) {
                        return [sharedActions.empty()];
                    }
                    const newTemplate = editorHelpers.removeSections(template!, checkedItems);
                    return [
                        actions.updateTemplateAction({
                            template: newTemplate,
                            templateKey
                        }),
                        sharedActions.broadcastPreviewMessage({
                            msg: {
                                type: 'reload',
                                template: newTemplate
                            }
                        }),
                        actions.closeAddItemPanel()
                    ];
                })
            ))
    ));

    orderSections$ = createEffect(() => this.actions$.pipe(
        ofType(actions.sortItems),
        filter(({ options }) => !options.parent),
        withLatestFrom(
            this.store$.select(selectors.changeTemplateContext),
            this.store$.select(selectors.selectCheckedItems)
        ),
        filter(([, template]) => !!template),
        switchMap(([{ options }, { template, templateKey }, checkedItems]) => {
            const newTemplate = editorHelpers.reorderSections(template!, options.currentIndex, options.previousIndex, checkedItems); // section can be null
            return [
                actions.updateTemplateAction({
                    template: newTemplate,
                    templateKey
                }),
                sharedActions.broadcastPreviewMessage({
                    msg: {
                        type: 'swap',
                        template: newTemplate,
                        currentIndex: options.previousIndex,
                        newIndex: options.currentIndex,
                        sectionIds: checkedItems
                    }
                })
            ];
        })
    ));

    orderBlocks$ = createEffect(() => this.actions$.pipe(
        ofType(actions.sortItems),
        filter(({ options }) => !!options.parent),
        withLatestFrom(
            this.store$.select(selectors.changeTemplateContext),
            this.store$.select(selectors.selectCheckedItems)
        ),
        filter(([, template]) => !!template),
        switchMap(([{ options }, { template, templateKey }, checkedItems]) => [
            actions.updateTemplateAction({
                template: editorHelpers.reorderBlocks(template!, options.parent!, options.currentIndex, options.previousIndex, checkedItems), // section can be null
                templateKey
            }),
        ])
    ));

    onStartPreviewUrl$ = createEffect(() => this.actions$.pipe(
        ofType(sharedActions.setLivePreviewUrl, actions.loadTemplateModelSuccess),
        withLatestFrom(
            this.store$.select(sharedSelectors.selectCurrentTemplateEntry),
            this.store$.select(selectors.selectCurrentTemplateModel)
        ),
        filter(([, entry, template]) => !!template && !(entry?.previewUrl) && !!(entry?.previewRule)),
        tap(([, entry, template]) => {
            const url = this.evaluator.evaluate(entry.previewRule, { item: template });
            url && this.eventsBus.emit({
                target: 'preview',
                payload: {
                    type: 'navigate',
                    url: url
                }
            });
        })
    ), { dispatch: false });
}
