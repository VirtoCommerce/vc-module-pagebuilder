import { SchemasList } from './../../models/schemas.model';
import { validateItemUnderEdit, useSchemasAction } from './../actions/data';
import { ModalService } from '@core/services';
import { ConfirmComponent } from '@core/dialogs';
import { Injectable, inject } from "@angular/core";

import { defer, forkJoin, of } from "rxjs";
import { withLatestFrom, filter, map, catchError, switchMap, exhaustMap, tap, distinctUntilChanged } from "rxjs/operators";

import { Store } from "@ngrx/store";
import { Actions, createEffect, ofType } from "@ngrx/effects";

import { RouterStateUrl } from '@shared/routing';

import { SaveTemplateComponent } from '@shared/dialogs';
import { PageHistoryComponent } from '@editor/dialogs';

import { BuilderState } from "../state";
import { canEditSharedComponentOriginal, helpers as editorHelpers } from '@editor/helpers';
import * as actions from "../actions";
import * as shared from '@shared/store/actions';
import * as routerActions from '@shared/routing/actions';
import { RouterNavigatedAction, ROUTER_NAVIGATED } from "@ngrx/router-store";
import * as selectors from "../selectors";
import * as fromRoute from '@shared/routing';
import * as fromShared from '@shared/store/selectors';

import { EditorModuleInfo } from "@models/modules";

import { PublishStatus, SharedComponentsService, SchemasService, TemplatesService } from "@editor/services";
import { SharedComponent } from '@editor/models';
import { AppConfig } from '@integration/services';
import { TemplateEntry } from '@shared/models';

/** What a toolbar action needs to know about the open page — the shape selectRunActionContext answers with. */
interface RunActionContext {
    templateKey: string;
    entry: TemplateEntry;
    path: string;
    type: string;
    groupId: string;
}

/**
 * A refused publish with a way out: dev changed the same page since this draft began, git cannot
 * merge the two, and the server says the draft may be published on top of the current page instead.
 * Duck-typed on purpose — an HttpErrorResponse carries the body as `error`, and a test can throw a
 * plain object shaped the same way.
 */
const isConflictWithAWayOut = (error: any): boolean =>
    error?.status === 409 && !!error?.error?.canRebase;

@Injectable({
    providedIn: 'root'
})
export class TemplateEditorDataEffects {
    private readonly store$ = inject(Store<BuilderState>);
    private readonly actions$ = inject(Actions);
    private readonly schemas = inject(SchemasService);
    private readonly templates = inject(TemplatesService);
    private readonly sharedComponents = inject(SharedComponentsService);
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

    broadcastCachedTemplate$ = createEffect(() => this.actions$.pipe(
        ofType(actions.raiseLoadData),
        withLatestFrom(
            this.store$.select(fromRoute.selectTemplateKeyParameter),
            this.store$.select(fromRoute.selectCultureNameParameter),
            this.store$.select(selectors.selectCurrentTemplateModel),
            this.store$.select(selectors.selectCurrentTemplateState),
            this.store$.select(fromShared.selectCurrentTemplateEntry),
            this.store$.select(fromRoute.selectSectionIdParameter),
        ),
        // Child routes (section/block editors) raise load data too. Re-send only
        // when the active document or its culture actually changes.
        distinctUntilChanged((previous, current) =>
            previous[1] === current[1] && previous[2] === current[2]),
        filter(([, , , template, state, entry]) => !!template && !!state && !!entry),
        map(([, , cultureName, template, , entry, sectionId]) => actions.broadcastResolvedPreview({
            msg: {
                type: 'page',
                template: template!,
                cultureName: cultureName || undefined,
                sectionId,
                ...entry?.previewMessage,
            },
        })),
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
        // defer, so that a configuration that cannot be resolved fails the stream instead of
        // throwing out of the effect and leaving the schemas loading forever (VCST-5847)
        exhaustMap(() => defer(() => this.schemas.getSchemas()).pipe(
            // the http client reports a failed request as an empty result. Dropping it left the
            // schemas marked as loading forever, and with them the fullscreen loader (VCST-5847).
            map(schemas => schemas
                ? actions.loadTemplateSchemasSuccess({ schemas })
                : actions.loadTemplateSchemasFails({ error: new Error('Section schemas are not available') })),
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
            this.store$.select(fromRoute.selectSharedComponentIdParameter),
        ),
        switchMap(([{ templateKey }, templateEntry, path, type, groupId, sectionId, cultureName, sharedComponentId]) => {
            // Capture synchronous configuration errors for both page and shared component requests.
            const request = defer(() => sharedComponentId
                ? forkJoin({
                    template: this.sharedComponents.getContent(sharedComponentId),
                    component: this.sharedComponents.get(sharedComponentId),
                })
                : this.templates.getTemplate(path, type, templateEntry, groupId).pipe(
                    map(template => ({ template, component: null as SharedComponent | null })),
                ));

            return request.pipe(
                switchMap(({ template: loadedTemplate, component }) => {
                    if (!loadedTemplate) {
                        return [actions.loadTemplateModelFails({ error: new Error('Template is not available'), templateKey })];
                    }
                    const template = editorHelpers.prepareTemplate(loadedTemplate);
                    return [
                        sharedComponentId && component
                            ? actions.cacheSharedComponent({ component, content: template })
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
                    ];
                }),
                catchError(error => [
                    actions.loadTemplateModelFails({ error, templateKey }),
                    shared.showNotification({
                        message: sharedComponentId ? 'Could not load Shared Component' : 'Could not load template',
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
            this.store$.select(fromRoute.selectSharedComponentIdParameter),
        ),
        filter(([, , , , , sharedComponentId]) => !sharedComponentId),
        switchMap(([{ templateKey }, entry, path, type, groupId]) => this.templates.getTemplatePublishStatus(path, type, entry || {}, groupId).pipe(
            filter((status): status is PublishStatus => !!status),
            map(({ hasChanges, published, pending, awaitingMerge, production }) => actions.getTemplatePublishStatusSuccess({ templateKey, hasChanges, published, pending, awaitingMerge, production })),
            catchError(error => of(actions.getTemplatePublishStatusFails({ error, templateKey })))
        ))
    ));


    publishTemplate$ = createEffect(() => this.actions$.pipe(
        ofType(actions.executeToolbarAction),
        filter(({ action }) => action === 'publish'),
        withLatestFrom(
            this.store$.select(selectors.selectRunActionContext),
        ),
        // Ask the server what happened instead of assuming it went well. Publishing a page can end up
        // waiting on a CI check, or refuse outright because the page changed in production while this
        // draft was being written — reporting "published" for either would send the editor away
        // believing the page is live.
        switchMap(([, context]) => this.publish(context).pipe(
            catchError(error => isConflictWithAWayOut(error)
                ? this.offerToPublishOverTheConflict(context, error)
                : of(actions.getTemplatePublishStatusFails({ error, templateKey: context.templateKey })))
        ))
    ));

    /** Publish, then ask the server where the page stands — one pipeline for the first attempt and for the retry over a conflict. */
    private publish(context: RunActionContext, options: { rebase?: boolean } = {}) {
        const { templateKey, entry, path, type, groupId } = context;
        return this.templates.publishTemplate(path, type, entry, groupId, options).pipe(
            switchMap(() => this.templates.getTemplatePublishStatus(path, type, entry, groupId)),
            filter((status): status is PublishStatus => !!status),
            switchMap(({ hasChanges, published, pending, awaitingMerge, production }) => [
                actions.getTemplatePublishStatusSuccess({ templateKey, hasChanges, published, pending, awaitingMerge, production }),
                shared.broadcastPlatformMessage({
                    msg: {
                        hasChanges,
                        published,
                        source: 'builder',
                        relativeUrl: path,
                        contentType: type,
                        template: entry,
                    }
                }),
            ]),
        );
    }

    /**
     * The conflict used to be a dead end: every Publish reused the same pull request and met the same
     * refusal, and restoring a version committed onto the same branch. The server now offers to
     * publish the draft on top of the current page — which replaces somebody's edit, so it is asked
     * for in so many words and never done by default. Declining publishes nothing and says so.
     */
    private offerToPublishOverTheConflict(context: RunActionContext, error: any) {
        const { templateKey } = context;
        const reason = error?.error?.error ?? 'The page changed while this draft was being written.';

        return this.modals.show<boolean>(ConfirmComponent, {
            data: {
                title: `${reason} Publish your version of the page anyway?`,
                icon: 'error',
                confirmText: 'Publish my version',
                declineText: 'Cancel',
            },
            panelClass: 'confirm-dialog',
        }).pipe(
            switchMap(confirmed => confirmed
                ? this.publish(context, { rebase: true }).pipe(
                    catchError(retryError => of(
                        actions.getTemplatePublishStatusFails({ error: retryError, templateKey }),
                        shared.showNotification({
                            message: `Could not publish: ${retryError?.error?.error ?? retryError?.message ?? 'request failed'}`,
                            msgType: 'error',
                            top: true,
                        }),
                    )))
                : of(
                    actions.getTemplatePublishStatusFails({ error, templateKey }),
                    shared.showNotification({
                        message: 'Nothing was published. Reload the page to see what changed, then apply your edit again.',
                        msgType: 'info',
                        top: true,
                    }),
                )),
        );
    }

    unpublishTemplate$ = createEffect(() => this.actions$.pipe(
        ofType(actions.executeToolbarAction),
        filter(({ action }) => action === 'unpublish'),
        withLatestFrom(
            this.store$.select(selectors.selectRunActionContext),
        ),
        switchMap(([, { templateKey, entry, path, type, groupId }]) => this.templates.unpublishTemplate(path, type, entry, groupId).pipe(
            switchMap(() => this.templates.getTemplatePublishStatus(path, type, entry, groupId)),
            filter((status): status is PublishStatus => !!status),
            switchMap(({ hasChanges, published, pending, awaitingMerge, production }) => [
                actions.getTemplatePublishStatusSuccess({ templateKey, hasChanges, published, pending, awaitingMerge, production }),
                shared.broadcastPlatformMessage({
                    msg: {
                        hasChanges,
                        published,
                        source: 'builder',
                        relativeUrl: path,
                        contentType: type,
                        template: entry,
                    }
                }),
            ]),
            catchError(error => of(actions.getTemplatePublishStatusFails({ error, templateKey })))
        ))
    ));

    // The second step. It ships like a publish — a commit, then a merge that can land straight
    // away or wait on CI checks — so the state comes from the server afterwards rather than from
    // an assumption that production is now up to date.
    promoteTemplate$ = createEffect(() => this.actions$.pipe(
        ofType(actions.executeToolbarAction),
        filter(({ action }) => action === 'promote'),
        withLatestFrom(
            this.store$.select(selectors.selectRunActionContext),
        ),
        switchMap(([, { templateKey, entry, path, type, groupId }]) => this.templates.promoteTemplate(path, type, entry, groupId).pipe(
            switchMap(() => this.templates.getTemplatePublishStatus(path, type, entry, groupId)),
            filter((status): status is PublishStatus => !!status),
            map(({ hasChanges, published, pending, awaitingMerge, production }) =>
                actions.getTemplatePublishStatusSuccess({ templateKey, hasChanges, published, pending, awaitingMerge, production })),
            // No platform broadcast here: promotion does not change whether the page is published
            // or has changes, which is all the admin blade listens for on that channel.
            catchError(error => of(actions.getTemplatePublishStatusFails({ error, templateKey })))
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
            this.store$.select(fromRoute.selectSharedComponentIdParameter),
        ),
        filter(([, changedTemplates, groupId, sharedComponentId]) => changedTemplates.length === 1 && !groupId && !sharedComponentId),
        map(([, changedTemplates]) => actions.saveTemplates({ templates: changedTemplates }))
    ));

    saveSharedComponent$ = createEffect(() => this.actions$.pipe(
        ofType(actions.executeToolbarAction),
        filter(({ action }) => action === 'save'),
        withLatestFrom(
            this.store$.select(fromRoute.selectSharedComponentIdParameter),
            this.store$.select(fromRoute.selectTemplateKeyParameter),
            this.store$.select(selectors.selectCurrentTemplateModel),
        ),
        filter(([, sharedComponentId, , template]) =>
            !!sharedComponentId
            && !!template
            && canEditSharedComponentOriginal(this.appConfig)),
        exhaustMap(([, sharedComponentId, templateKey, template]) =>
            this.sharedComponents.updateContent(sharedComponentId, template!).pipe(
                withLatestFrom(this.store$.select(selectors.selectLoadedTemplates)),
                switchMap(([, loadedTemplates]) => [
                    actions.cacheSharedComponentContent({ componentId: sharedComponentId, content: template! }),
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
            this.store$.select(fromRoute.selectSharedComponentIdParameter),
        ),
        filter(([, changedTemplates, groupId, sharedComponentId]) =>
            changedTemplates.length > 0 && !!groupId && !sharedComponentId),
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
            this.store$.select(fromRoute.selectSharedComponentIdParameter),
        ),
        filter(([, changedTemplates, groupId, sharedComponentId]) => changedTemplates.length > 1 && !groupId && !sharedComponentId),
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
            // defer for the same reason as in loadTemplate$: a synchronous failure has to reach
            // catchError, otherwise the save never ends and the loader stays up (VCST-5847)
            return defer(() => this.templates.saveTemplates(templatesToSave)).pipe(
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


    // One request per opened page, so the toolbar can say that unpublished versions exist somewhere —
    // the case this feature exists for is an edit made outside the builder that used to stay invisible
    // until it was published. The versions themselves are only read when the panel is opened.
    loadPageHistoryWithTemplate$ = createEffect(() => this.actions$.pipe(
        ofType(actions.loadTemplateModelSuccess),
        map(({ templateKey }) => actions.loadPageHistory({ templateKey }))
    ));

    loadPageHistory$ = createEffect(() => this.actions$.pipe(
        ofType(actions.loadPageHistory),
        withLatestFrom(
            this.store$.select(fromShared.selectCurrentTemplateEntry),
            this.store$.select(fromRoute.selectPathParameter),
            this.store$.select(fromRoute.selectTypeParameter),
            this.store$.select(fromRoute.selectGroupIdParameter),
        ),
        switchMap(([{ templateKey, after }, entry, path, type, groupId]) => this.templates.getPageHistory(path, type, entry || {}, groupId, after).pipe(
            // no descriptor, no history: a store outside the git flow keeps no versions, and that is an
            // answer rather than an error
            filter(history => !!history),
            map(history => actions.loadPageHistorySuccess({ templateKey, history: history!, after })),
            catchError(error => of(actions.loadPageHistoryFails({ error, templateKey })))
        ))
    ));

    openPageHistory$ = createEffect(() => this.actions$.pipe(
        ofType(actions.executeToolbarAction),
        filter(({ action }) => action === 'history'),
        withLatestFrom(this.store$.select(fromRoute.selectTemplateKeyParameter)),
        switchMap(([, templateKey]) => [
            // reloaded on every open: somebody else may have pushed a version since the page was opened
            actions.loadPageHistory({ templateKey }),
            actions.showPageHistoryPanel({ templateKey }),
        ])
    ));

    showPageHistoryPanel$ = createEffect(() => this.actions$.pipe(
        ofType(actions.showPageHistoryPanel),
        exhaustMap(({ templateKey }) => this.modals.show<void>(PageHistoryComponent, {
            data: { templateKey },
            panelClass: 'page-history-dialog',
            autoFocus: false,
        })),
        map(() => shared.empty())
    ));

    previewVersion$ = createEffect(() => this.actions$.pipe(
        ofType(actions.previewVersion),
        withLatestFrom(this.store$.select(selectors.selectRunActionContext)),
        tap(([{ sha }, { entry, path, type, groupId }]) => this.templates.previewVersion(path, type, entry, groupId, sha))
    ), { dispatch: false });

    restoreVersion$ = createEffect(() => this.actions$.pipe(
        ofType(actions.restoreVersion),
        withLatestFrom(this.store$.select(selectors.selectRunActionContext)),
        // The restore appends a commit to my own branch, so afterwards the editor has to be shown what it
        // now holds: the page is re-read, the publish status recomputed, and the version list reloaded so
        // the restore itself appears in it.
        switchMap(([{ templateKey, sha }, { entry, path, type, groupId }]) => this.templates.restoreVersion(path, type, entry, groupId, sha).pipe(
            switchMap(result => [
                actions.restoreVersionSuccess({ templateKey, sha, branch: result?.branch ?? '', commitSha: result?.commitSha ?? '' }),
                actions.reloadTemplateModel({ templateKey }),
                actions.getTemplatePublishStatus({ templateKey }),
                actions.loadPageHistory({ templateKey }),
                shared.showNotification({ message: `Continuing from version ${sha.substring(0, 7)}`, msgType: 'success', top: true }),
            ]),
            catchError(error => of(
                actions.restoreVersionFails({ error, templateKey, sha }),
                shared.showNotification({ message: `Could not continue from ${sha.substring(0, 7)}: ${error?.message ?? 'request failed'}`, msgType: 'error', top: true })
            ))
        ))
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
            this.store$.select(fromRoute.selectSharedComponentIdParameter),
        ),
        switchMap(([{ templateKey }, entry, path, type, groupId, sharedComponentId]) => (sharedComponentId
            ? this.sharedComponents.getContent(sharedComponentId)
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
