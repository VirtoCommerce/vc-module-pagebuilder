import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Action, Store } from '@ngrx/store';
import { catchError, filter, map, Observable, of, switchMap, withLatestFrom } from 'rxjs';

import { ModalService } from '@core/services';
import {
  SharedComponentInsertModeComponent,
  SharedComponentInsertModeResult,
  SharedComponentNameComponent,
} from '@editor/dialogs';
import {
  detachSharedComponent,
  insertSharedComponentCopy,
  insertSharedComponentReference,
  replaceSectionsWithSharedComponent,
} from '@editor/helpers';
import { SharedComponent } from '@editor/models';
import { SharedComponentsService } from '@editor/services';
import { AppConfig } from '@integration/services';
import { SectionModel, TemplateModel } from '@models/document';
import * as routingSelectors from '@shared/routing/selectors';
import * as sharedActions from '@shared/store/actions';

import * as actions from '../../actions';
import * as selectors from '../../selectors';
import { BuilderState } from '../../state';
import {
  createInsertionAnchor,
  DocumentOperationContext,
  getSelectedSections,
  hasSharedComponentPlacement,
  isSameDocument,
  resolveInsertionAnchor,
  sameIds,
  sameSectionRevision,
} from '../helpers/document-operation.helpers';

@Injectable({ providedIn: 'root' })
export class SharedComponentsDomainEffects {
  private readonly actions$ = inject(Actions);
  private readonly store = inject(Store<BuilderState>);
  private readonly sharedComponents = inject(SharedComponentsService);
  private readonly modals = inject(ModalService);
  private readonly appConfig = inject(AppConfig);

  chooseInsertionMode$ = createEffect(() =>
    this.actions$.pipe(
      ofType(actions.chooseSharedComponentInsertionMode),
      filter(() => this.can('canInsertSharedComponents')),
      withLatestFrom(this.store.select(selectors.selectSharedComponents)),
      switchMap(([request, components]) => {
        const componentRequest = components[request.componentId]
          ? of(components[request.componentId])
          : this.sharedComponents.get(request.componentId);

        return componentRequest.pipe(
          withLatestFrom(this.store.select(routingSelectors.selectSharedComponentIdParameter)),
          switchMap(([component, sharedComponentId]) => {
            const sharedComponentDisabledReason = getSharedModeUnavailableReason(
              sharedComponentId,
              component.storeId,
              this.sharedComponents.storeId,
            );
            const allowShared = sharedComponentDisabledReason === null;
            return this.modals.show<SharedComponentInsertModeResult>(SharedComponentInsertModeComponent, {
              data: {
                name: component.name,
                defaultMode: allowShared ? request.defaultMode : 'copy',
                allowShared,
                sharedComponentDisabledReason: sharedComponentDisabledReason || undefined,
              },
              panelClass: 'shared-component-insert-dialog',
            });
          }),
          map((result) =>
            result?.accept === true && result.mode
              ? actions.insertSharedComponent({
                  componentId: request.componentId,
                  mode: result.mode,
                  insertIndex: request.insertIndex,
                })
              : sharedActions.empty(),
          ),
          catchError((error) =>
            of(
              sharedActions.showNotification({
                message: `Shared Component could not be copied: ${getErrorMessage(error)}`,
                msgType: 'error',
                top: true,
              }),
            ),
          ),
        );
      }),
    ),
  );

  insert$ = createEffect(() =>
    this.actions$.pipe(
      ofType(actions.insertSharedComponent),
      filter(() => this.can('canInsertSharedComponents')),
      withLatestFrom(
        this.store.select(selectors.selectCurrentTemplateModel),
        this.store.select(routingSelectors.selectTemplateKeyParameter),
        this.store.select(routingSelectors.selectSharedComponentIdParameter),
        this.store.select(selectors.selectSharedComponents),
      ),
      filter(([, template]) => !!template),
      switchMap(([{ componentId, mode, insertIndex }, template, templateKey, sharedComponentId, components]) => {
        const origin: DocumentOperationContext = { templateKey, sharedComponentId };
        const anchor = createInsertionAnchor(template!, insertIndex);
        let contentRequest: Observable<{ component: SharedComponent | null; content: TemplateModel | null }>;
        if (mode === 'copy') {
          contentRequest = this.sharedComponents
            .getContent(componentId)
            .pipe(map((content) => ({ component: null, content })));
        } else {
          const componentRequest = components[componentId]
            ? of(components[componentId])
            : this.sharedComponents.get(componentId);
          contentRequest = componentRequest.pipe(map((component) => ({ component, content: null })));
        }

        return contentRequest.pipe(
          withLatestFrom(
            this.store.select(selectors.selectCurrentTemplateModel),
            this.store.select(routingSelectors.selectTemplateKeyParameter),
            this.store.select(routingSelectors.selectSharedComponentIdParameter),
          ),
          switchMap(([loaded, latestTemplate, latestTemplateKey, latestSharedComponentId]) => {
            if (!latestTemplate || !isSameDocument(origin, latestTemplateKey, latestSharedComponentId)) {
              return [operationCancelled('inserted')];
            }

            if (mode === 'shared') {
              const unavailableReason = getSharedModeUnavailableReason(
                latestSharedComponentId,
                loaded.component?.storeId,
                this.sharedComponents.storeId,
              );
              if (unavailableReason) {
                return [sharedModeUnavailable(unavailableReason)];
              }
            }

            const latestInsertIndex = resolveInsertionAnchor(latestTemplate, anchor);
            if (latestInsertIndex === null) {
              return [operationCancelled('inserted')];
            }

            const updated =
              mode === 'copy'
                ? insertSharedComponentCopy(latestTemplate, loaded.content!, latestInsertIndex)
                : insertSharedComponentReference(latestTemplate, componentId, latestInsertIndex);
            const result: Action[] = [];
            if (loaded.content) {
              result.push(actions.cacheSharedComponentContent({ componentId, content: loaded.content }));
            }
            result.push(
              actions.updateTemplateAction({ template: updated, templateKey: latestTemplateKey }),
              actions.broadcastResolvedPreview({
                msg: { type: 'reload', template: updated },
              }),
              actions.closeAddItemPanel(),
            );
            return result;
          }),
          catchError((error) =>
            of(
              sharedActions.showNotification({
                message: `Shared Component could not be inserted: ${getErrorMessage(error)}`,
                msgType: 'error',
                top: true,
              }),
            ),
          ),
        );
      }),
    ),
  );

  saveSelection$ = createEffect(() =>
    this.actions$.pipe(
      ofType(actions.saveSelectionAsSharedComponent),
      filter(() => this.can('canCreateSharedComponents') && this.can('canInsertSharedComponents')),
      withLatestFrom(
        this.store.select(selectors.selectCheckedItems),
        this.store.select(selectors.selectCurrentTemplateModel),
        this.store.select(routingSelectors.selectTemplateKeyParameter),
        this.store.select(routingSelectors.selectSharedComponentIdParameter),
      ),
      filter(([, selectedIds, template]) => selectedIds.length > 0 && !!template),
      switchMap(([, selectedIds, template, templateKey, sharedComponentId]) => {
        if (sharedComponentId) {
          return of(
            sharedActions.showNotification({
              message: 'Nested Shared Components are not supported',
              msgType: 'warning',
              top: true,
            }),
          );
        }

        const initialSelection = getSelectedSections(template!, selectedIds);
        if (initialSelection.error) {
          return of(selectionInvalid(initialSelection.error));
        }

        const origin: DocumentOperationContext = { templateKey, sharedComponentId };
        const initialSelectedIds = [...selectedIds];

        return this.modals
          .show<{ accept: boolean; name: string }>(SharedComponentNameComponent, {
            panelClass: 'shared-component-name-dialog',
          })
          .pipe(
            filter((result) => !!result?.accept && !!result.name?.trim()),
            withLatestFrom(
              this.store.select(selectors.selectCheckedItems),
              this.store.select(selectors.selectCurrentTemplateModel),
              this.store.select(routingSelectors.selectTemplateKeyParameter),
              this.store.select(routingSelectors.selectSharedComponentIdParameter),
            ),
            switchMap(([result, latestSelectedIds, latestTemplate, latestTemplateKey, latestSharedComponentId]) => {
              if (
                !latestTemplate ||
                !isSameDocument(origin, latestTemplateKey, latestSharedComponentId) ||
                !sameIds(initialSelectedIds, latestSelectedIds)
              ) {
                return of(operationCancelled('created'));
              }

              const latestSelection = getSelectedSections(latestTemplate, latestSelectedIds);
              if (latestSelection.error) {
                return of(selectionInvalid(latestSelection.error));
              }

              const content = {
                settings: {} as SectionModel,
                content: latestSelection.sections,
              } as TemplateModel;
              const submittedSections = [...latestSelection.sections];
              const submittedSelectedIds = [...latestSelectedIds];
              return this.sharedComponents.create(result.name, content).pipe(
                withLatestFrom(
                  this.store.select(selectors.selectCheckedItems),
                  this.store.select(selectors.selectCurrentTemplateModel),
                  this.store.select(routingSelectors.selectTemplateKeyParameter),
                  this.store.select(routingSelectors.selectSharedComponentIdParameter),
                ),
                switchMap(([component, finalSelectedIds, finalTemplate, finalTemplateKey, finalSharedComponentId]) => {
                  const cache = actions.cacheSharedComponent({
                    component,
                    content,
                    addToSearchResults: true,
                  });
                  if (
                    !finalTemplate ||
                    !isSameDocument(origin, finalTemplateKey, finalSharedComponentId) ||
                    !sameIds(submittedSelectedIds, finalSelectedIds)
                  ) {
                    return [cache, componentCreatedButNotInserted(component)];
                  }

                  const finalSelection = getSelectedSections(finalTemplate, finalSelectedIds);
                  if (finalSelection.error || !sameSectionRevision(submittedSections, finalSelection.sections)) {
                    return [cache, componentCreatedButNotInserted(component)];
                  }

                  const updated = replaceSectionsWithSharedComponent(finalTemplate, finalSelectedIds, component.id);
                  return [
                    cache,
                    ...submittedSelectedIds.map((sectionId) =>
                      actions.sectionStateChangedAction({
                        sectionId,
                        templateKey: finalTemplateKey,
                        state: { selected: false },
                      }),
                    ),
                    actions.updateTemplateAction({ template: updated, templateKey: finalTemplateKey }),
                    actions.broadcastResolvedPreview({
                      msg: { type: 'reload', template: updated },
                    }),
                    sharedActions.showNotification({
                      message: `Shared Component “${component.name}” created`,
                      msgType: 'success',
                      top: true,
                    }),
                  ];
                }),
              );
            }),
            catchError((error) =>
              of(
                sharedActions.showNotification({
                  message: `Shared Component could not be created: ${getErrorMessage(error)}`,
                  msgType: 'error',
                  top: true,
                }),
              ),
            ),
          );
      }),
    ),
  );

  detach$ = createEffect(() =>
    this.actions$.pipe(
      ofType(actions.detachSharedComponent),
      filter(() => this.can('canInsertSharedComponents')),
      withLatestFrom(
        this.store.select(selectors.selectCurrentTemplateModel),
        this.store.select(routingSelectors.selectTemplateKeyParameter),
        this.store.select(routingSelectors.selectSharedComponentIdParameter),
      ),
      filter(([, template]) => !!template),
      switchMap(([{ sectionId, componentId }, template, templateKey, sharedComponentId]) => {
        const origin: DocumentOperationContext = { templateKey, sharedComponentId };
        if (!hasSharedComponentPlacement(template!, sectionId, componentId)) {
          return of(operationCancelled('detached'));
        }

        return this.sharedComponents.getContent(componentId).pipe(
          withLatestFrom(
            this.store.select(selectors.selectCurrentTemplateModel),
            this.store.select(routingSelectors.selectTemplateKeyParameter),
            this.store.select(routingSelectors.selectSharedComponentIdParameter),
          ),
          switchMap(([content, latestTemplate, latestTemplateKey, latestSharedComponentId]) => {
            if (
              !latestTemplate ||
              !isSameDocument(origin, latestTemplateKey, latestSharedComponentId) ||
              !hasSharedComponentPlacement(latestTemplate, sectionId, componentId)
            ) {
              return [operationCancelled('detached')];
            }

            const updated = detachSharedComponent(latestTemplate, sectionId, content);
            return [
              actions.cacheSharedComponentContent({ componentId, content }),
              actions.updateTemplateAction({ template: updated, templateKey: latestTemplateKey }),
              actions.broadcastResolvedPreview({
                msg: { type: 'reload', template: updated },
              }),
              sharedActions.showNotification({
                message: 'Shared Component detached. This copy is now independent.',
                msgType: 'info',
                top: true,
              }),
            ];
          }),
          catchError((error) =>
            of(
              sharedActions.showNotification({
                message: `Shared Component could not be detached: ${getErrorMessage(error)}`,
                msgType: 'error',
                top: true,
              }),
            ),
          ),
        );
      }),
    ),
  );

  private can(option: 'canInsertSharedComponents' | 'canCreateSharedComponents'): boolean {
    return this.appConfig.getValue(option) === true;
  }
}

function getSharedModeUnavailableReason(
  sharedComponentId: string,
  componentStoreId: string | undefined,
  currentStoreId: string,
): string | null {
  if (sharedComponentId) {
    return 'Shared instances cannot be nested inside another Shared Component. Create an independent copy instead.';
  }

  if (
    !componentStoreId ||
    !currentStoreId ||
    componentStoreId.trim().toLowerCase() !== currentStoreId.trim().toLowerCase()
  ) {
    return 'Shared instances can only be inserted into pages from the same store. Create an independent copy instead.';
  }

  return null;
}

function sharedModeUnavailable(message: string): Action {
  return sharedActions.showNotification({ message, msgType: 'warning', top: true });
}

function operationCancelled(operation: 'created' | 'detached' | 'inserted'): Action {
  return sharedActions.showNotification({
    message: `Shared Component could not be ${operation} because the document changed. Try again.`,
    msgType: 'warning',
    top: true,
  });
}

function componentCreatedButNotInserted(component: SharedComponent): Action {
  return sharedActions.showNotification({
    message: `Shared Component “${component.name}” was created but not inserted because the document changed.`,
    msgType: 'warning',
    top: true,
  });
}

function selectionInvalid(message: string): Action {
  return sharedActions.showNotification({ message, msgType: 'warning', top: true });
}

function getErrorMessage(error: unknown): string {
  if (error && typeof error === 'object' && 'message' in error) {
    return String((error as { message: unknown }).message);
  }
  return String(error || 'Unknown error');
}
