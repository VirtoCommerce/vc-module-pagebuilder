import { ROUTER_NAVIGATED } from '@ngrx/router-store';
import { Injectable, inject } from "@angular/core";
import { Actions, createEffect, ofType } from "@ngrx/effects";
import { Action, Store } from "@ngrx/store";
import { catchError, switchMap, map, of, withLatestFrom, filter, tap, fromEvent } from "rxjs";

import { EventsBusService, NotificationsService } from "@core/services";
import { TemplatesService, MetaDataService } from '@shared/services';
import { AppConfig, ImpersonateService } from '../../integration/services';

import { BuilderState } from "./state";
import * as actions from "./actions";
import * as fromRoute from '@shared/routing';
import * as router from "@shared/routing/actions";
import * as fromState from "@shared/store/selectors";

@Injectable({
    providedIn: "root"
})
export class SharedEffects {
    private readonly store$ = inject(Store<BuilderState>);
    private readonly actions$ = inject(Actions);
    private readonly templatesService = inject(TemplatesService);
    private readonly eventsBus = inject(EventsBusService);
    private readonly notification = inject(NotificationsService);
    private readonly metaDataService = inject(MetaDataService);
    private readonly impersonateService = inject(ImpersonateService);
    private readonly appConfig = inject(AppConfig);

    raiseInitModule$ = createEffect(() => this.actions$.pipe(
        ofType(ROUTER_NAVIGATED),
        withLatestFrom(this.store$.select(fromState.isAppInitialized)),
        filter(([, init]) => !init),
        switchMap(() => [
            actions.initShared()
        ])
    ));

    selectDefaultTemplate$ = createEffect(() => this.actions$.pipe(
        ofType(ROUTER_NAVIGATED),
        withLatestFrom(
            this.store$.select(fromRoute.selectTemplateKeyParameter)
        ),
        filter(([, template]) => !template),
        switchMap(() => [
            actions.selectDefaultTemplate()
        ])
    ));

    initShared$ = createEffect(() => this.actions$.pipe(
        ofType(actions.initShared),
        switchMap(() => [
            actions.loadTemplateEntries()
        ])
    ));

    loadTemplateEntries$ = createEffect(() => this.actions$.pipe(
        ofType(actions.loadTemplateEntries),
        switchMap(() => this.templatesService.getTemplatesList().pipe(
            map(templatesEntries => actions.loadTemplateEntriesSuccess({ templatesEntries: templatesEntries || {} })),
            catchError(error => of(actions.loadTemplateEntriesFails({ error })))
        )),
    ));

    mergeServerTemplateEntries$ = createEffect(() => this.actions$.pipe(
        ofType(actions.loadTemplateEntriesSuccess),
        withLatestFrom(
            this.store$.select(fromState.selectTemplatesEntries)
        ),
        map(([{ templatesEntries }, currentEntries]) => {
            const result = { ...templatesEntries, ...currentEntries };
            return actions.useTemplateEntries({ templatesEntries: result });
        })
    ));

    mergeCustomTemplateEntries$ = createEffect(() => this.actions$.pipe(
        ofType(actions.updateCustomSchemas),
        withLatestFrom(
            this.store$.select(fromState.selectTemplatesEntries)
        ),
        map(([{ schemas }, currentEntries]) => {
            const result = { ...currentEntries, ...schemas?.templates };
            return actions.useTemplateEntries({ templatesEntries: result });
        })
    ));

    raiseInitApp$ = createEffect(() => this.actions$.pipe(
        ofType(actions.loadTemplateEntriesSuccess),
        withLatestFrom(this.store$.select(fromRoute.selectParentTemplateParameter)),
        filter(([, parent]) => !parent),
        switchMap(() => [
            actions.initApp(),
            actions.setLivePreviewUrl()
        ])
    ));

    loadChildrenOnStartApp$ = createEffect(() => this.actions$.pipe(
        ofType(actions.loadTemplateEntriesSuccess),
        withLatestFrom(this.store$.select(fromRoute.selectParentTemplateParameter)),
        filter(([, parent]) => !!parent),
        switchMap(() => [
            actions.raiseLoadChildrenTemplates() // todo: here must be a call to load specific template
        ])
    ));

    raiseLoadChildrenTemplates$ = createEffect(() => this.actions$.pipe(
        ofType(actions.raiseLoadChildrenTemplates),
        withLatestFrom(
            this.store$.select(fromRoute.selectParentTemplateParameter)
        ),
        switchMap(([, parent]) => [
            actions.loadChildrenTemplates({ templateKey: parent, onInit: true })
        ])
    ));

    redirectToDefaultTemplate$ = createEffect(() => this.actions$.pipe(
        ofType(actions.selectDefaultTemplate),
        withLatestFrom(
            this.store$.select(fromState.selectTemplatesEntries),
            this.store$.select(fromState.selectTemplatesEntriesAsList),
            this.store$.select(fromRoute.selectTemplateKeyParameter)
        ),
        filter(([, entriesAsObject, templatesEntriesAsList, templateKey]) => !templateKey && !!templatesEntriesAsList.length || !entriesAsObject[templateKey]),
        map(([, , templatesEntries, templateKey]) => {
            const entry = templatesEntries.find(item => !!item.isDefault) || templatesEntries[0];
            return actions.selectTemplate({
                templateType: entry?.type || '',
                path: entry?.path,
                pageId: entry?.pageId,
                templateKey
            })
        })
    ));

    selectTemplate$ = createEffect(() => this.actions$.pipe(
        ofType(actions.selectTemplate),
        withLatestFrom(
            this.store$.select(fromState.selectParentTemplateKey),
            this.store$.select(fromRoute.selectTypeParameter),
            this.store$.select(fromRoute.selectPathParameter),
            this.store$.select(fromRoute.selectParentTemplateParameter),
            this.store$.select(fromRoute.isEmpty)
        ),
        filter(([{ path, templateType }, parentTemplateKey, typeParameter, pathParameter, parentTemplateParameter, isEmpty]) =>
            !isEmpty // if route is not initialized yet
            && (
                path !== pathParameter // and template entry was changed
                || parentTemplateKey !== parentTemplateParameter // or parent template was changed
                || templateType !== typeParameter
            )
            || !pathParameter), // or template parameter from route is empty
        switchMap(([{ templateType, path, pageId }, parentTemplateKey]) => [
            router.go({ queryParams: { templateType, path, pageId, parent: parentTemplateKey } }),
            // this action allows to inform other modules to do some stuff, i.e. editor module do the other redirect
            actions.templateChanged({ templateType, path, pageId, parent: parentTemplateKey })
        ])
    ));

    currentFilterChanged$ = createEffect(() => this.actions$.pipe(
        ofType(actions.filterTemplates),
        withLatestFrom(
            this.store$.select(fromState.selectParentTemplate),
            this.store$.select(fromState.selectParentTemplateKey)
        ),
        filter(([, parent]) => !!parent?.request),
        map(([, , parentKey]) => actions.loadChildrenTemplates({ templateKey: parentKey || '', onInit: false }))
    ));

    switchToChildrenTemplates$ = createEffect(() => this.actions$.pipe(
        ofType(actions.switchToChildrenTemplates),
        map(({ templateKey }) => actions.loadChildrenTemplates({ templateKey, onInit: false }))
    ));

    loadChildrenTemplates$ = createEffect(() => this.actions$.pipe(
        ofType(actions.loadChildrenTemplates),
        withLatestFrom(
            this.store$.select(fromState.selectTemplatesEntries),
            this.store$.select(fromState.selectCurrentFilter)
        ),
        filter(([{ templateKey }]) => !!templateKey),
        map(([{ templateKey, onInit }, entries, currentFilter]) => ({ templateEntry: entries[templateKey], onInit, entries, currentFilter, templateKey })),
        filter(({ templateEntry }) => !!templateEntry),
        switchMap(({ templateEntry, onInit, entries, currentFilter, templateKey }) => {
            const context = { item: templateEntry, filter: currentFilter, templates: entries };
            return this.templatesService.getChildrenTemplates(templateEntry!, context).pipe(
                switchMap(childrenEntries => {
                    const result: Action[] = [actions.loadChildrenTemplatesSuccess({ childrenEntries, parentTemplate: templateKey })];
                    if (onInit) {
                        result.push(actions.initApp());
                        result.push(actions.setLivePreviewUrl());
                    }
                    return result;
                }),
                catchError(error => of(actions.loadChildrenTemplatesFails({ error, parentTemplate: templateKey })))
            );
        })
    ));

    changePreviewMode$ = createEffect(() => this.actions$.pipe(
        ofType(actions.changePreviewMode),
        map(({ mode }) => router.go({ queryParams: { 'preview-mode': mode } }))
    ));

    onStartPreviewUrl$ = createEffect(() => this.actions$.pipe(
        ofType(actions.setLivePreviewUrl),
        withLatestFrom(
            this.store$.select(fromState.selectCurrentTemplateEntry)
        ),
        filter(([, template]) => !!template?.previewUrl),
        tap(([, template]) => {
            this.eventsBus.emit({
                target: 'preview',
                payload: {
                    type: 'navigate',
                    url: template!.previewUrl // || this.appConfig.getContext().location.params.path || this.appConfig.getValue('startPreviewPath') || '/'
                }
            });
        })
    ), { dispatch: false });

    broadcastNavigation$ = createEffect(() => this.actions$.pipe(
        ofType(actions.selectTemplate),
        withLatestFrom(
            this.store$.select(fromState.selectCurrentTemplatesEntries)
        ),
        tap(([{ templateKey }, templates]) => templates?.[templateKey]?.previewUrl && this.eventsBus.emit({
            target: 'preview',
            payload: {
                type: 'navigate',
                url: templates?.[templateKey]?.previewUrl
            }
        }))
    ), { dispatch: false });

    broadcastPreviewMessage$ = createEffect(() => this.actions$.pipe(
        ofType(actions.broadcastPreviewMessage),
        tap(({ msg }) => this.eventsBus.emit({ target: 'preview', payload: msg }))
    ), { dispatch: false });

    broadcastPlatformMessage$ = createEffect(() => this.actions$.pipe(
        ofType(actions.broadcastPlatformMessage),
        tap(({ msg }) => this.eventsBus.emit({ target: 'platform', payload: msg }))
    ), { dispatch: false });

    showNotification$ = createEffect(() => this.actions$.pipe(
        ofType(actions.showNotification),
        tap(({ message, msgType, top }) => this.notification.show(message, msgType, top ? 'tr' : 'bl'))
    ), { dispatch: false });

    previewLoadedMessage$ = createEffect(() => fromEvent<MessageEvent>(window, 'message').pipe(
        filter((event: MessageEvent) => event.data.source === 'preview' && event.data.type === 'loaded'),
        tap(event => console.log(event.data)),
        switchMap(event => {
            const result: Action[] = [actions.previewLoaded()];
            if (event.data.data) {
                result.push(actions.updateCustomSchemas({ schemas: event.data.data }));
            }
            return result;
        }),
    ));

    selectSectionMessage$ = createEffect(() => fromEvent<MessageEvent>(window, 'message').pipe(
        filter((event: MessageEvent) => event.data.source === 'preview' && event.data.type === 'select'),
        map(({ data }) => actions.selectSection({ sectionId: data.sectionId }))
    ));

    hoverSectionMessage$ = createEffect(() => fromEvent<MessageEvent>(window, 'message').pipe(
        filter((event: MessageEvent) => event.data.source === 'preview' && event.data.type === 'hover'),
        map(({ data }) => actions.previewSectionHovered({ sectionId: data.sectionId }))
    ));

    previewLoaded$ = createEffect(() => this.actions$.pipe(
        ofType(actions.previewLoaded),
        tap(() => this.eventsBus.emit({ target: 'preview', payload: { type: 'preview-loaded' } })),
        map(() => actions.setLivePreviewUrl())
    ));

    sendPreviewAuth$ = createEffect(() => this.actions$.pipe(
        ofType(actions.previewLoaded),
        switchMap(() => {
            const accounts = this.appConfig.getValue('previewImpersonation' as any);
            const userId = Array.isArray(accounts) ? accounts[0] : accounts;
            if (!userId) {
                return of(actions.empty());
            }
            return this.impersonateService.getImpersonateToken(userId).pipe(
                tap(tokenResponse => {
                    this.eventsBus.emit({
                        target: 'preview',
                        payload: {
                            type: 'auth',
                            token: tokenResponse
                        }
                    });
                }),
                map(() => actions.sendPreviewAuthSuccess()),
                catchError(error => {
                    console.error('Failed to get impersonate token for preview:', error);
                    return of(actions.sendPreviewAuthFailed({ error }));
                })
            );
        })
    ));

    setWindowTitle$ = createEffect(() => this.actions$.pipe(
        ofType(actions.setWindowTitle),
        tap(({ title }) => this.metaDataService.setTitle(title))
    ), { dispatch: false });
}
