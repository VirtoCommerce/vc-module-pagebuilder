import { Injectable, inject } from "@angular/core";
import { Actions, createEffect, ofType } from "@ngrx/effects";
import { Store } from "@ngrx/store";
import { withLatestFrom, filter, tap, map, switchMap } from "rxjs/operators";

import { broadcastPreviewMessage } from '@shared/store/actions';
import * as routingActions from '@shared/routing/actions';
import * as sharedSelectors from '@shared/store/selectors';

// import * as routingSelectors from '@shared/routing'
import * as editorHelpers from '@editor/helpers/editor.helpers';

import * as sharedActions from '@shared/store/actions';

import { BuilderState } from "../state";
import * as actions from "../actions";
import * as selectors from "../selectors";

@Injectable({
    providedIn: 'root'
})
export class TemplateEditorUiEffects {
    private readonly store$ = inject(Store<BuilderState>);
    private readonly actions$ = inject(Actions);

    navigateToAddSection$ = createEffect(() => this.actions$.pipe(
        ofType(actions.showBlankSections),
        filter(x => !x.sectionId),
        map(({ positionIndex }) => routingActions.go({ path: ['/pages/add', positionIndex] }))
    ));

    navigateToAddBlock$ = createEffect(() => this.actions$.pipe(
        ofType(actions.showBlankSections),
        filter(x => !!x.sectionId),
        map(({ sectionId, positionIndex }) => routingActions.go({ path: ['/pages/add', sectionId, positionIndex] }))
    ));

    navigateToEditTemplate$ = createEffect(() => this.actions$.pipe(
        ofType(actions.closeAddItemPanel),
        switchMap(() => [
            routingActions.go({ path: ['/pages'] }),
            actions.resetGroupsState()
        ])
    ));

    navigateToEditSettings$ = createEffect(() => this.actions$.pipe(
        ofType(actions.editSettings),
        switchMap(({ schema }) => [
            routingActions.go({ path: ['/pages', 'settings', schema.type] })
        ])
    ));

    // this redirect is necessary when we edit other page, it happens after redirect from shared module
    templateChanged$ = createEffect(() => this.actions$.pipe(
        ofType(sharedActions.templateChanged),
        switchMap(({ templateType, path, pageId, parent }) => [
            routingActions.go({ path: ['/pages'], queryParams: { type: templateType, path, pageId, parent } })
        ])
    ));

    setWindowTitle$ = createEffect(() => this.actions$.pipe(
        ofType(actions.setWindowTitle),
        withLatestFrom(this.store$.select(sharedSelectors.selectCurrentTemplateEntry)),
        map(([, templateEntry]) => sharedActions.setWindowTitle({ title: templateEntry?.name || null }))
    ));

    completeEditSection$ = createEffect(() => this.actions$.pipe(
        ofType(actions.closeEditItemPanel),
        switchMap(() => [
            routingActions.go({ path: ['/pages'] })
        ])
    ));

    navigateToEditSection$ = createEffect(() => this.actions$.pipe(
        ofType(actions.editSectionAction, sharedActions.selectSection),
        switchMap(({ sectionId }) => [
            routingActions.go({ path: ['/pages', sectionId] })
        ])
    ));

    navigateToEditBlock$ = createEffect(() => this.actions$.pipe(
        ofType(actions.editBlockAction),
        switchMap(({ sectionId, blockId }) => [
            routingActions.go({ path: ['/pages', sectionId, blockId] })
        ])
    ));

    raiseUpdateTemplate$ = createEffect(() => this.actions$.pipe(
        ofType(
            actions.updateTemplateAction
        ),
        map(() => actions.templateContentChanged())
    ));

    templateContentChanged$ = createEffect(() => this.actions$.pipe(
        ofType(actions.templateContentChanged),
        // withLatestFrom(this.store$.select(selectors.selectCurrentItemForEdit)),
        // todo: should be only one section
        withLatestFrom(
            this.store$.select(selectors.selectCurrentTemplateModel),
            this.store$.select(sharedSelectors.selectCurrentTemplateEntry),
            this.store$.select(sharedSelectors.selectParentTemplateKey),
            this.store$.select(selectors.selectSectionModelFromRoute),
            this.store$.select(selectors.selectBlockModelFromRoute)
        ),
        switchMap(([, template, entry, parentKey, section, block]) => {
            const message = [broadcastPreviewMessage({
                msg: {
                    type: 'changed',
                    template, section, block,
                    sectionId: section?.id,
                    ...entry?.previewMessage
                }
            })];
            return [
                ...message,
                parentKey
                    ? sharedActions.setDirtyState({ parentKey: parentKey, templateKey: entry.key, dirty: true })
                    : sharedActions.setRootDirtyState({ templateKey: entry.key, dirty: true })
            ];
        })
    ));

    navigateToThemeSettings$ = createEffect(() => this.actions$.pipe(
        ofType(actions.executeToolbarAction),
        filter(x => x.action === 'theme-settings'),
        map(() => routingActions.jump({ path: ['/themes'] }))
    ));

    notifySuccessSave$ = createEffect(() => this.actions$.pipe(
        ofType(actions.saveTemplateSuccess),
        switchMap(({ templateKey, parentKey, template }) => [
            sharedActions.showNotification({ message: `Template ${template.settings['name']} saved successfully`, msgType: 'success', top: true }),
            parent
                ? sharedActions.setDirtyState({ parentKey, templateKey, dirty: false })
                : sharedActions.setRootDirtyState({ templateKey, dirty: false })
        ])
    ));

    notifyFailsSave$ = createEffect(() => this.actions$.pipe(
        ofType(actions.saveTemplateFails),
        tap(({ error }) => {
            console.log(error)
        }),
        map(() => sharedActions.showNotification({ message: 'Could not save template', msgType: 'error', top: true }))
    ));

    previewItem$ = createEffect(() => this.actions$.pipe(
        ofType(actions.previewItemAction),
        withLatestFrom(
            this.store$.select(selectors.selectCurrentTemplateModel),
            this.store$.select(sharedSelectors.selectCurrentTemplateEntry),
            this.store$.select(selectors.selectSectionModelFromRoute),
            this.store$.select(selectors.selectSharedSchemas),
            this.store$.select(selectors.selectObjectsSchemas)
        ),
        map(([{ item }, template, entry, section, shared, objects]) => {
            const sharedSchemaName = !!section ? "_blocks" : "_sections";
            const fullSchema = editorHelpers.prepareSchema(item, shared, objects, sharedSchemaName, entry?.controls);
            const model = editorHelpers.generatePreviewBySchema(fullSchema);
            return broadcastPreviewMessage({
                msg: {
                    type: 'preview',
                    template, section, model,
                    ...entry?.previewMessage
                }
            })
        })
    ));

    hoverSection$ = createEffect(() => this.actions$.pipe(
        ofType(actions.hoverSection),
        map(({ sectionId }) =>
            broadcastPreviewMessage({
                msg: {
                    type: 'hover',
                    sectionId
                }
            })
        )
    ));

    scrollToSectionInPreview$ = createEffect(() => this.actions$.pipe(
        ofType(actions.editSectionAction),
        withLatestFrom(
            this.store$.select(selectors.selectCurrentTemplateModel),
            this.store$.select(sharedSelectors.selectCurrentTemplateEntry)
        ),
        map(([{ sectionId }, template, entry]) =>
            broadcastPreviewMessage({
                msg: {
                    type: 'select',
                    template, sectionId,
                    section: template?.content.find(x => x.id == sectionId),
                    ...entry?.previewMessage
                }
            })
        )
    ));

    scrollToBlockInPreview$ = createEffect(() => this.actions$.pipe(
        ofType(actions.editBlockAction),
        withLatestFrom(
            this.store$.select(selectors.selectCurrentTemplateModel),
            this.store$.select(sharedSelectors.selectCurrentTemplateEntry),
            this.store$.select(selectors.selectSectionModelFromRoute)
        ),
        map(([{ sectionId, blockId }, template, entry, section]) =>
            broadcastPreviewMessage({
                msg: {
                    type: 'select',
                    template, section, sectionId, blockId,
                    ...entry?.previewMessage
                }
            })
        )
    ));

}
