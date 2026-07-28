import { SchemasList } from './../../models/schemas.model';
import { validateItemUnderEdit, useSchemasAction } from './../actions/data';
import { ModalService } from '@core/services';
import { Injectable, inject } from "@angular/core";

import { forkJoin, of } from "rxjs";
import { withLatestFrom, filter, map, catchError, switchMap, exhaustMap, tap } from "rxjs/operators";

import { Store } from "@ngrx/store";
import { Actions, createEffect, ofType } from "@ngrx/effects";

import { RouterStateUrl } from '@shared/routing';

import { SaveTemplateComponent } from '@shared/dialogs';

import { BuilderState } from "../state";
import { canEditLinkedComponentOriginal, helpers as editorHelpers } from '@editor/helpers';
import * as actions from "../actions";
import * as shared from '@shared/store/actions';
import * as routerActions from '@shared/routing/actions';
import { RouterNavigatedAction, ROUTER_NAVIGATED } from "@ngrx/router-store";
import * as selectors from "../selectors";
import * as fromRoute from '@shared/routing';
import * as fromShared from '@shared/store/selectors';

import { EditorModuleInfo } from "@models/modules";

import { LinkedComponentsService, SchemasService, TemplatesService } from "@editor/services";
import { LinkedComponent } from '@editor/models';
import { TemplateModel } from '@models/document';
import { AppConfig } from '@integration/services';

@Injectable({
    providedIn: 'root'
})
export class TemplateEditorDataEffects {
    private readonly store$ = inject(Store<BuilderState>);
    private readonly actions$ = inject(Actions);
    private readonly schemas = inject(SchemasService);
    private readonly templates = inject(TemplatesService);
    private readonly linkedComponents = inject(LinkedComponentsService);
    private readonly modals = inject(ModalService);
    private readonly appConfig = inject(AppConfig);

    loadTemplateData$ = createEffect(() => this.actions$.pipe(
        ofType(ROUTER_NAVIGATED),
        filter((action: RouterNavigatedAction<RouterStateUrl>) => !!action?.payload?.routerState?.data),
        map((action: RouterNavigatedAction<RouterStateUrl>) => action.payload.routerState.data),
        withLatestFrom(this.store$.select(fromRoute.isEmpty)),
        filter(([data, isEmpty]) => data?.['module'] === EditorModuleInfo.name && !isEmpty),
        switchMap(() => [
            actions.raiseLoadData(),
            actions.setWindowTitle()
        ])
    ));

    loadTemplateDataOnInit$ = createEffect(() => this.actions$.pipe(
        ofType(shared.initApp),
        switchMap(() => [
            actions.raiseLoadData(),
            actions.setWindowTitle()
        ])
    ));

    raiseLoadTemplateModel$ = createEffect(() => this.actions$.pipe(
        ofType(actions.raiseLoadData),
        withLatestFrom(
            this.store$.select(selectors.selectCurrentTemplateModel),
            this.store$.select(selectors.selectCurrentTemplateState),
            this.store$.select(fromShared.selectCurrentTemplateEntry),
            this.store$.select(fromRoute.selectTemplateKeyParameter),
        ),
        // load when template still is not loaded or hasn't been changed yet
        filter(([, template, state, entry]) => !template || !state || !entry),
        switchMap(([, , , , templateKey]) => [
            actions.loadTemplateModel({ templateKey })
        ])
    ));

    raiseLoadTemplateSchemas$ = createEffect(() => this.actions$.pipe(
        ofType(actions.raiseLoadData),
        withLatestFrom(
            this.store$.select(selectors.isSchemasLoaded)
        ),
        filter(([, schemasLoaded]) => !schemasLoaded),
        switchMap(() => [actions.loadTemplateSchemas()])
    ));

    loadSchemas$ = createEffect(() => this.actions$.pipe(
        ofType(actions.loadTemplateSchemas),
        exhaustMap(() => this.schemas.getSchemas().pipe(
            filter(schemas => !!schemas),
            map(schemas => actions.loadTemplateSchemasSuccess({ schemas })),
            catchError(error => of(actions.loadTemplateSchemasFails({ error })))
        ))
    ));

    // schemas are loaded from the server. apply it to the current state
    mergeServerSchemas$ = createEffect(() => this.actions$.pipe(
        ofType(actions.loadTemplateSchemasSuccess),
        withLatestFrom(
            this.store$.select(selectors.selectAllSchemas),
        ),
        filter(([{ schemas }]) => !!schemas),
        map(([{ schemas }, allSchemas]) => {
            // custom schemas have been loaded before
            // and they have higher priority
            const result = editorHelpers.mergeSchemas(schemas || {} as SchemasList, allSchemas)
            return useSchemasAction({ schemas: result });
        }),
    ));

    mergeCustomSchemas$ = createEffect(() => this.actions$.pipe(
        ofType(shared.updateCustomSchemas),
        withLatestFrom(
            this.store$.select(selectors.selectAllSchemas),
        ),
        filter(([{ schemas }]) => !!schemas),
        map(([{ schemas }, allSchemas]) => {

            // server schemas have been loaded before
            // but custom schemas have higher priority
            const result = editorHelpers.mergeSchemas(allSchemas, schemas || {} as SchemasList)
            return useSchemasAction({ schemas: result });
        })
    ));

    loadTemplate$ = createEffect(() => this.actions$.pipe(
        ofType(actions.loadTemplateModel),
        withLatestFrom(
            this.store$.select(fromShared.selectCurrentTemplateEntry),
            this.store$.select(fromRoute.selectPathParameter),
            this.store$.select(fromRoute.selectTypeParameter),
            this.store$.select(fromRoute.selectGroupIdParameter),
            this.store$.select(fromRoute.selectSectionIdParameter),
            this.store$.select(fromRoute.selectCultureNameParameter),
            this.store$.select(fromRoute.selectLinkedComponentIdParameter),
        ),
        switchMap(([{ templateKey }, templateEntry, path, type, groupId, sectionId, cultureName, linkedComponentId]) => {
            const request = linkedComponentId
                ? forkJoin({
                    template: this.linkedComponents.getContent(linkedComponentId),
                    component: this.linkedComponents.get(linkedComponentId),
                })
                : this.templates.getTemplate(path, type, templateEntry, groupId).pipe(
                    map(template => ({ template, component: null as LinkedComponent | null })),
                );

            return request.pipe(
                filter((result): result is { template: TemplateModel; component: LinkedComponent | null } => !!result.template),
                map(result => ({ ...result, template: editorHelpers.prepareTemplate(result.template) })),
                switchMap(({ template, component }) => [
                    linkedComponentId && component
                        ? actions.cacheLinkedComponent({ component, content: template })
                        : actions.getTemplatePublishStatus({ templateKey }),
                    actions.loadTemplateModelSuccess({ template, templateKey }),
                    actions.validateItemUnderEdit(),
                    actions.broadcastResolvedPreview({
                        msg: {
                            type: 'page',
                            template,
                            // Pass the edited page's language (from the designer URL) to the storefront
                            // preview so it renders in that language instead of the store default (VCST-5219).
                            // Omit when empty so it never overrides an already-applied preview language.
                            cultureName: cultureName || undefined,
                            sectionId,
                            ...templateEntry?.previewMessage
                        }
                    })
                ]),
                catchError(error => [
                    actions.loadTemplateModelFails({ error, templateKey }),
                    shared.showNotification({
                        message: linkedComponentId ? 'Could not load Shared Component' : 'Could not load template',
                        msgType: 'error',
                        top: true
                    }),
                ])
            );
        })
    ));

    validateItemUnderEdit$ = createEffect(() => this.actions$.pipe(
        ofType(validateItemUnderEdit),
        withLatestFrom(
            this.store$.select(selectors.selectCurrentItemForEdit),
            this.store$.select(fromRoute.selectSectionIdParameter),
            this.store$.select(fromRoute.selectBlockIdParameter),
        ),
        filter(([_, item, sectionId, blockId]) => (!!sectionId || !!blockId) && !item),
        switchMap(() => [
            routerActions.go({ path: ['/pages'] }),
        ])
    ));

    resendTemplateOnAccountChange$ = createEffect(() => this.actions$.pipe(
        ofType(shared.sendPreviewAuthSuccess, shared.previewLoaded),
        withLatestFrom(
            this.store$.select(selectors.changeTemplateContext),
            this.store$.select(fromRoute.selectCultureNameParameter),
        ),
        filter(([, { template }]) => !!template),
        map(([, { template, templateEntry, sectionId }, cultureName]) => actions.broadcastResolvedPreview({
            msg: {
                type: 'page',
                template,
                // Keep the page language on resend (iframe reload / preview auth) so the storefront
                // preview does not revert to the store default language (VCST-5219).
                // Omit when empty so it never overrides an already-applied preview language.
                cultureName: cultureName || undefined,
                sectionId,
                ...templateEntry?.previewMessage
            }
        }))
    ));

    getTemplatePublishStatus$ = createEffect(() => this.actions$.pipe(
        ofType(actions.getTemplatePublishStatus),
        withLatestFrom(
            this.store$.select(fromShared.selectCurrentTemplateEntry),
            this.store$.select(fromRoute.selectPathParameter),
            this.store$.select(fromRoute.selectTypeParameter),
            this.store$.select(fromRoute.selectGroupIdParameter),
            this.store$.select(fromRoute.selectLinkedComponentIdParameter),
        ),
        filter(([, , , , , linkedComponentId]) => !linkedComponentId),
        switchMap(([{ templateKey }, entry, path, type, groupId]) => this.templates.getTemplatePublishStatus(path, type, entry || {}, groupId).pipe(
            filter(status => !!status),
            map(({ hasChanges, published }) => actions.getTemplatePublishStatusSuccess({ templateKey, hasChanges, published })),
            catchError(error => of(actions.getTemplatePublishStatusFails({ error, templateKey })))
        ))
    ));


    publishTemplate$ = createEffect(() => this.actions$.pipe(
        ofType(actions.executeToolbarAction),
        filter(({ action }) => action === 'publish'),
        withLatestFrom(
            this.store$.select(selectors.selectRunActionContext),
        ),
        switchMap(([, { templateKey, entry, path, type, groupId }]) => this.templates.publishTemplate(path, type, entry, groupId).pipe(
            switchMap(() => [
                actions.getTemplatePublishStatusSuccess({ templateKey, hasChanges: false, published: true }),
                shared.broadcastPlatformMessage({
                    msg: {
                        hasChanges: false,
                        published: true,
                        source: 'builder',
                        relativeUrl: path,
                        contentType: type,
                        template: entry,
                    }
                }),
            ]),
        ))
    ));

    unpublishTemplate$ = createEffect(() => this.actions$.pipe(
        ofType(actions.executeToolbarAction),
        filter(({ action }) => action === 'unpublish'),
        withLatestFrom(
            this.store$.select(selectors.selectRunActionContext),
        ),
        switchMap(([, { templateKey, entry, path, type, groupId }]) => this.templates.unpublishTemplate(path, type, entry, groupId).pipe(
            switchMap(() => [
                actions.getTemplatePublishStatusSuccess({ templateKey, hasChanges: true, published: false }),
                shared.broadcastPlatformMessage({
                    msg: {
                        hasChanges: true,
                        published: false,
                        source: 'builder',
                        relativeUrl: path,
                        contentType: type,
                        template: entry,
                    }
                }),
            ]),
        ))
    ));

    externalPreviewAction$ = createEffect(() => this.actions$.pipe(
        ofType(actions.executeToolbarAction),
        filter(({ action }) => action === 'external-preview'),
        withLatestFrom(
            this.store$.select(selectors.selectRunActionContext),
        ),
        tap(([, { entry, path, type, groupId }]) => this.templates.externalPreview(path, type, entry, groupId))
    ), { dispatch: false });

    saveTemplate$ = createEffect(() => this.actions$.pipe(
        ofType(actions.executeToolbarAction),
        filter(({ action }) => action === 'save'),
        withLatestFrom(
            this.store$.select(selectors.selectChangedTemplates),
            this.store$.select(fromRoute.selectGroupIdParameter),
            this.store$.select(fromRoute.selectLinkedComponentIdParameter),
        ),
        filter(([, changedTemplates, groupId, linkedComponentId]) => changedTemplates.length === 1 && !groupId && !linkedComponentId),
        map(([, changedTemplates]) => actions.saveTemplates({ templates: changedTemplates }))
    ));

    saveLinkedComponent$ = createEffect(() => this.actions$.pipe(
        ofType(actions.executeToolbarAction),
        filter(({ action }) => action === 'save'),
        withLatestFrom(
            this.store$.select(fromRoute.selectLinkedComponentIdParameter),
            this.store$.select(fromRoute.selectTemplateKeyParameter),
            this.store$.select(selectors.selectCurrentTemplateModel),
        ),
        filter(([, linkedComponentId, , template]) =>
            !!linkedComponentId
            && !!template
            && canEditLinkedComponentOriginal(this.appConfig)),
        exhaustMap(([, linkedComponentId, templateKey, template]) =>
            this.linkedComponents.updateContent(linkedComponentId, template!).pipe(
                withLatestFrom(this.store$.select(selectors.selectLoadedTemplates)),
                switchMap(([, loadedTemplates]) => [
                    actions.cacheLinkedComponentContent({ componentId: linkedComponentId, content: template! }),
                    actions.saveTemplateSuccess({
                        templateKey,
                        template: template!,
                        clearDirty: loadedTemplates[templateKey] === template,
                    }),
                ]),
                catchError(error => of(actions.saveTemplateFails({ error }))),
            )
        ),
    ));

    // should be changed to universal approach
    saveGroupedPage$ = createEffect(() => this.actions$.pipe(
        ofType(actions.executeToolbarAction),
        filter(({ action }) => action === 'save'),
        withLatestFrom(
            this.store$.select(selectors.selectChangedTemplates),
            this.store$.select(fromRoute.selectGroupIdParameter),
            this.store$.select(fromRoute.selectLinkedComponentIdParameter),
        ),
        filter(([, , groupId, linkedComponentId]) => !!groupId && !linkedComponentId),
        switchMap(([, changedTemplates, groupId]) => {
            const groupedPageContent = changedTemplates[0].content;
            return this.templates.saveGroupedPage(groupId!, groupedPageContent).pipe(
                switchMap(() => [
                    actions.saveTemplateSuccess({
                        templateKey: changedTemplates[0].info.key,
                        parentKey: changedTemplates[0].info.parent,
                        template: changedTemplates[0].content
                    }),
                    actions.getTemplatePublishStatusSuccess({ templateKey: changedTemplates[0].info.key, hasChanges: true, published: false }),
                    shared.broadcastPlatformMessage({
                        msg: {
                            hasChanges: true,
                            relativeUrl: changedTemplates[0].entry.path,
                            contentType: changedTemplates[0].entry.type,
                            template: changedTemplates[0].content,
                            published: false,
                            source: 'builder'
                        }
                    }),
                    changedTemplates.length > 1
                        ? shared.showNotification({
                            message: 'Only current page was saved',
                            msgType: 'warning',
                            top: true
                        })
                        : shared.empty()
                ]),
                catchError(error => of(actions.saveTemplateFails({ error })))
            );
        })
    ));

    showSaveDialog$ = createEffect(() => this.actions$.pipe(
        ofType(actions.executeToolbarAction),
        filter(({ action }) => action === 'save'),
        withLatestFrom(
            this.store$.select(selectors.selectChangedTemplates),
            this.store$.select(fromRoute.selectGroupIdParameter),
            this.store$.select(fromRoute.selectLinkedComponentIdParameter),
        ),
        filter(([, changedTemplates, groupId, linkedComponentId]) => changedTemplates.length > 1 && !groupId && !linkedComponentId),
        switchMap(([, changedTemplates]) => this.modals.show<{ accept: boolean, entries: string[] }>(SaveTemplateComponent, {
            data: {
                entries: changedTemplates.map(x => x.info)
            }
        }).pipe(
            map((result) => result?.accept
                ? actions.saveTemplates({
                    templates: result.entries.map(x => changedTemplates.find(y => y.info.key === x)!).filter(x => !!x.content)
                })
                : shared.empty()
            )
        ))
    ));

    sendTemplateToServer$ = createEffect(() => this.actions$.pipe(
        ofType(actions.saveTemplates),
        withLatestFrom(
            this.store$.select(selectors.selectCurrentTemplateState),
        ),
        switchMap(([{ templates }, state]) => {
            const templatesToSave = templates.filter(x => !!x.content);
            return this.templates.saveTemplates(templatesToSave).pipe(
                switchMap(() => templates.map(x => [
                    actions.saveTemplateSuccess({ templateKey: x.info.key, parentKey: x.info.parent, template: x.content }),
                    actions.getTemplatePublishStatusSuccess({ templateKey: x.info.key, hasChanges: true, published: false }),
                    shared.broadcastPlatformMessage({
                        msg: {
                            hasChanges: true,
                            relativeUrl: x.entry.path,
                            contentType: x.entry.type,
                            template: x.content,
                            published: state?.published || false,
                            source: 'builder'
                        }
                    }),
                ]).flatMap(x => x)),
                catchError(error => of(actions.saveTemplateFails({ error })))
            );
        })
    ));


    resetTemplate$ = createEffect(() => this.actions$.pipe(
        ofType(actions.executeContextMenuAction),
        filter(x => x.action === 'reset-template'),
        withLatestFrom(this.store$.select(fromRoute.selectTemplateKeyParameter)),
        map(([, templateKey]) => actions.reloadTemplateModel({ templateKey }))
    ));

    reloadTemplate$ = createEffect(() => this.actions$.pipe(
        ofType(actions.reloadTemplateModel),
        withLatestFrom(
            this.store$.select(fromShared.selectCurrentTemplateEntry),
            this.store$.select(fromRoute.selectPathParameter),
            this.store$.select(fromRoute.selectTypeParameter),
            this.store$.select(fromRoute.selectGroupIdParameter),
            this.store$.select(fromRoute.selectLinkedComponentIdParameter),
        ),
        switchMap(([{ templateKey }, entry, path, type, groupId, linkedComponentId]) => (linkedComponentId
            ? this.linkedComponents.getContent(linkedComponentId)
            : this.templates.getTemplate(path, type, entry, groupId)).pipe(
            filter(template => !!template),
            map(template => editorHelpers.prepareTemplate(template!)),
            switchMap((template) => [
                actions.reloadTemplateModelSuccess({ templateKey, template }),
                actions.refreshPreview(),
            ]),
            catchError(error => of(actions.reloadTemplateModelFails({ error, templateKey })))
        ))
    ));
}
