import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Action, Store } from '@ngrx/store';
import { catchError, filter, map, Observable, of, switchMap, withLatestFrom } from 'rxjs';

import { ModalService } from '@core/services';
import {
  LinkedComponentInsertModeComponent,
  LinkedComponentInsertModeResult,
  LinkedComponentNameComponent,
} from '@editor/dialogs';
import {
  detachLinkedComponent,
  insertLinkedComponentCopy,
  insertLinkedComponentReference,
  replaceSectionsWithLinkedComponent,
} from '@editor/helpers';
import { LinkedComponent } from '@editor/models';
import { LinkedComponentsService } from '@editor/services';
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
  hasLinkedPlacement,
  isSameDocument,
  resolveInsertionAnchor,
  sameIds,
  sameSectionRevision,
} from '../helpers/document-operation.helpers';

@Injectable({ providedIn: 'root' })
export class LinkedComponentsDomainEffects {
  private readonly actions$ = inject(Actions);
  private readonly store = inject(Store<BuilderState>);
  private readonly linkedComponents = inject(LinkedComponentsService);
  private readonly modals = inject(ModalService);
  private readonly appConfig = inject(AppConfig);

  chooseInsertionMode$ = createEffect(() =>
    this.actions$.pipe(
      ofType(actions.chooseLinkedComponentInsertionMode),
      filter(() => this.can('canInsertLinkedComponents')),
      withLatestFrom(this.store.select(selectors.selectLinkedComponents)),
      switchMap(([request, components]) => {
        const componentRequest = components[request.componentId]
          ? of(components[request.componentId])
          : this.linkedComponents.get(request.componentId);

        return componentRequest.pipe(
          withLatestFrom(this.store.select(routingSelectors.selectLinkedComponentIdParameter)),
          switchMap(([component, linkedComponentId]) => {
            const linkedDisabledReason = getLinkedModeUnavailableReason(
              linkedComponentId,
              component.storeId,
              this.linkedComponents.storeId,
            );
            const allowLinked = linkedDisabledReason === null;
            return this.modals.show<LinkedComponentInsertModeResult>(LinkedComponentInsertModeComponent, {
              data: {
                name: component.name,
                defaultMode: allowLinked ? request.defaultMode : 'copy',
                allowLinked,
                linkedDisabledReason: linkedDisabledReason || undefined,
              },
              panelClass: 'linked-component-insert-dialog',
            });
          }),
          map((result) =>
            result?.accept === true && result.mode
              ? actions.insertLinkedComponent({
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
      ofType(actions.insertLinkedComponent),
      filter(() => this.can('canInsertLinkedComponents')),
      withLatestFrom(
        this.store.select(selectors.selectCurrentTemplateModel),
        this.store.select(routingSelectors.selectTemplateKeyParameter),
        this.store.select(routingSelectors.selectLinkedComponentIdParameter),
        this.store.select(selectors.selectLinkedComponents),
      ),
      filter(([, template]) => !!template),
      switchMap(([{ componentId, mode, insertIndex }, template, templateKey, linkedComponentId, components]) => {
        const origin: DocumentOperationContext = { templateKey, linkedComponentId };
        const anchor = createInsertionAnchor(template!, insertIndex);
        let contentRequest: Observable<{ component: LinkedComponent | null; content: TemplateModel | null }>;
        if (mode === 'copy') {
          contentRequest = this.linkedComponents
            .getContent(componentId)
            .pipe(map((content) => ({ component: null, content })));
        } else {
          const componentRequest = components[componentId]
            ? of(components[componentId])
            : this.linkedComponents.get(componentId);
          contentRequest = componentRequest.pipe(map((component) => ({ component, content: null })));
        }

        return contentRequest.pipe(
          withLatestFrom(
            this.store.select(selectors.selectCurrentTemplateModel),
            this.store.select(routingSelectors.selectTemplateKeyParameter),
            this.store.select(routingSelectors.selectLinkedComponentIdParameter),
          ),
          switchMap(([loaded, latestTemplate, latestTemplateKey, latestLinkedComponentId]) => {
            if (!latestTemplate || !isSameDocument(origin, latestTemplateKey, latestLinkedComponentId)) {
              return [operationCancelled('inserted')];
            }

            if (mode === 'linked') {
              const unavailableReason = getLinkedModeUnavailableReason(
                latestLinkedComponentId,
                loaded.component?.storeId,
                this.linkedComponents.storeId,
              );
              if (unavailableReason) {
                return [linkedModeUnavailable(unavailableReason)];
              }
            }

            const latestInsertIndex = resolveInsertionAnchor(latestTemplate, anchor);
            if (latestInsertIndex === null) {
              return [operationCancelled('inserted')];
            }

            const updated =
              mode === 'copy'
                ? insertLinkedComponentCopy(latestTemplate, loaded.content!, latestInsertIndex)
                : insertLinkedComponentReference(latestTemplate, componentId, latestInsertIndex);
            const result: Action[] = [];
            if (loaded.content) {
              result.push(actions.cacheLinkedComponentContent({ componentId, content: loaded.content }));
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
      ofType(actions.saveSelectionAsLinkedComponent),
      filter(() => this.can('canCreateLinkedComponents') && this.can('canInsertLinkedComponents')),
      withLatestFrom(
        this.store.select(selectors.selectCheckedItems),
        this.store.select(selectors.selectCurrentTemplateModel),
        this.store.select(routingSelectors.selectTemplateKeyParameter),
        this.store.select(routingSelectors.selectLinkedComponentIdParameter),
      ),
      filter(([, selectedIds, template]) => selectedIds.length > 0 && !!template),
      switchMap(([, selectedIds, template, templateKey, linkedComponentId]) => {
        if (linkedComponentId) {
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

        const origin: DocumentOperationContext = { templateKey, linkedComponentId };
        const initialSelectedIds = [...selectedIds];

        return this.modals
          .show<{ accept: boolean; name: string }>(LinkedComponentNameComponent, {
            panelClass: 'linked-component-name-dialog',
          })
          .pipe(
            filter((result) => !!result?.accept && !!result.name?.trim()),
            withLatestFrom(
              this.store.select(selectors.selectCheckedItems),
              this.store.select(selectors.selectCurrentTemplateModel),
              this.store.select(routingSelectors.selectTemplateKeyParameter),
              this.store.select(routingSelectors.selectLinkedComponentIdParameter),
            ),
            switchMap(([result, latestSelectedIds, latestTemplate, latestTemplateKey, latestLinkedComponentId]) => {
              if (
                !latestTemplate ||
                !isSameDocument(origin, latestTemplateKey, latestLinkedComponentId) ||
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
              return this.linkedComponents.create(result.name, content).pipe(
                withLatestFrom(
                  this.store.select(selectors.selectCheckedItems),
                  this.store.select(selectors.selectCurrentTemplateModel),
                  this.store.select(routingSelectors.selectTemplateKeyParameter),
                  this.store.select(routingSelectors.selectLinkedComponentIdParameter),
                ),
                switchMap(([component, finalSelectedIds, finalTemplate, finalTemplateKey, finalLinkedComponentId]) => {
                  const cache = actions.cacheLinkedComponent({ component, content });
                  if (
                    !finalTemplate ||
                    !isSameDocument(origin, finalTemplateKey, finalLinkedComponentId) ||
                    !sameIds(submittedSelectedIds, finalSelectedIds)
                  ) {
                    return [cache, componentCreatedButNotInserted(component)];
                  }

                  const finalSelection = getSelectedSections(finalTemplate, finalSelectedIds);
                  if (finalSelection.error || !sameSectionRevision(submittedSections, finalSelection.sections)) {
                    return [cache, componentCreatedButNotInserted(component)];
                  }

                  const updated = replaceSectionsWithLinkedComponent(finalTemplate, finalSelectedIds, component.id);
                  return [
                    cache,
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
      ofType(actions.detachLinkedComponent),
      filter(() => this.can('canInsertLinkedComponents')),
      withLatestFrom(
        this.store.select(selectors.selectCurrentTemplateModel),
        this.store.select(routingSelectors.selectTemplateKeyParameter),
        this.store.select(routingSelectors.selectLinkedComponentIdParameter),
      ),
      filter(([, template]) => !!template),
      switchMap(([{ sectionId, componentId }, template, templateKey, linkedComponentId]) => {
        const origin: DocumentOperationContext = { templateKey, linkedComponentId };
        if (!hasLinkedPlacement(template!, sectionId, componentId)) {
          return of(operationCancelled('detached'));
        }

        return this.linkedComponents.getContent(componentId).pipe(
          withLatestFrom(
            this.store.select(selectors.selectCurrentTemplateModel),
            this.store.select(routingSelectors.selectTemplateKeyParameter),
            this.store.select(routingSelectors.selectLinkedComponentIdParameter),
          ),
          switchMap(([content, latestTemplate, latestTemplateKey, latestLinkedComponentId]) => {
            if (
              !latestTemplate ||
              !isSameDocument(origin, latestTemplateKey, latestLinkedComponentId) ||
              !hasLinkedPlacement(latestTemplate, sectionId, componentId)
            ) {
              return [operationCancelled('detached')];
            }

            const updated = detachLinkedComponent(latestTemplate, sectionId, content);
            return [
              actions.cacheLinkedComponentContent({ componentId, content }),
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

  private can(option: 'canInsertLinkedComponents' | 'canCreateLinkedComponents'): boolean {
    return this.appConfig.getValue(option) === true;
  }
}

function getLinkedModeUnavailableReason(
  linkedComponentId: string,
  componentStoreId: string | undefined,
  currentStoreId: string,
): string | null {
  if (linkedComponentId) {
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

function linkedModeUnavailable(message: string): Action {
  return sharedActions.showNotification({ message, msgType: 'warning', top: true });
}

function operationCancelled(operation: 'created' | 'detached' | 'inserted'): Action {
  return sharedActions.showNotification({
    message: `Shared Component could not be ${operation} because the document changed. Try again.`,
    msgType: 'warning',
    top: true,
  });
}

function componentCreatedButNotInserted(component: LinkedComponent): Action {
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
