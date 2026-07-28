import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Action, Store } from '@ngrx/store';
import { catchError, concatMap, EMPTY, filter, forkJoin, map, Observable, of, switchMap, withLatestFrom } from 'rxjs';

import { isLinkedComponentReference, resolveLinkedComponents } from '@editor/helpers';
import { LinkedComponent } from '@editor/models';
import { LINKED_COMPONENTS_PAGE_SIZE, LinkedComponentsService } from '@editor/services';
import { AppConfig } from '@integration/services';
import { TemplateModel } from '@models/document';
import * as sharedActions from '@shared/store/actions';

import * as actions from '../../actions';
import * as selectors from '../../selectors';
import { BuilderState } from '../../state';

interface LoadResult<T> {
  value: T | null;
  error: string | null;
}

@Injectable({ providedIn: 'root' })
export class LinkedComponentsDataEffects {
  private readonly actions$ = inject(Actions);
  private readonly store = inject(Store<BuilderState>);
  private readonly linkedComponents = inject(LinkedComponentsService);
  private readonly appConfig = inject(AppConfig);

  search$ = createEffect(() =>
    this.actions$.pipe(
      ofType(actions.searchLinkedComponents),
      filter(() => this.canInsertLinkedComponents()),
      switchMap(({ keyword, skip = 0 }) =>
        this.linkedComponents.search(keyword, skip, LINKED_COMPONENTS_PAGE_SIZE).pipe(
          map((result) => actions.searchLinkedComponentsSuccess({ keyword, result, append: skip > 0 })),
          catchError((error) =>
            of(
              actions.searchLinkedComponentsFailed({
                keyword,
                error: getErrorMessage(error),
              }),
            ),
          ),
        ),
      ),
    ),
  );

  loadDetails$ = createEffect(() =>
    this.actions$.pipe(
      ofType(actions.loadLinkedComponentDetails, actions.clearLinkedComponentDetails),
      switchMap((action) =>
        'componentId' in action
          ? this.linkedComponents.get(action.componentId).pipe(
              map((component) => actions.loadLinkedComponentDetailsSuccess({ component })),
              catchError((error) =>
                of(
                  actions.loadLinkedComponentDetailsFailed({
                    componentId: action.componentId,
                    error: getErrorMessage(error),
                  }),
                ),
              ),
            )
          : EMPTY,
      ),
    ),
  );

  refreshUsageAfterSave$ = createEffect(() =>
    this.actions$.pipe(
      ofType(actions.saveTemplateSuccess),
      switchMap(({ template }) => {
        const componentIds = [
          ...new Set(template.content.filter(isLinkedComponentReference).map((reference) => reference.componentRef)),
        ];
        if (componentIds.length === 0) {
          return of(sharedActions.empty());
        }

        return forkJoin(componentIds.map((componentId) => toLoadResult(this.linkedComponents.get(componentId)))).pipe(
          switchMap((results) =>
            results.flatMap((result) =>
              result.value ? [actions.cacheLinkedComponent({ component: result.value })] : [],
            ),
          ),
        );
      }),
    ),
  );

  resolvePreview$ = createEffect(() =>
    this.actions$.pipe(
      ofType(actions.broadcastResolvedPreview),
      concatMap(({ msg: previewMessage }) =>
        of(previewMessage).pipe(
          withLatestFrom(
            this.store.select(selectors.selectLinkedComponents),
            this.store.select(selectors.selectLinkedComponentContents),
            this.store.select(selectors.selectLinkedComponentErrors),
          ),
          switchMap(([msg, metadataCache, contentCache, errors]) => {
            const template = msg['template'] as TemplateModel | null | undefined;
            if (!template) {
              return of(sharedActions.broadcastPreviewMessage({ msg }));
            }

            const componentIds = [
              ...new Set(
                template.content.filter(isLinkedComponentReference).map((reference) => reference.componentRef),
              ),
            ];
            const forceRefresh = msg.type === 'page' || msg.type === 'reload';

            if (componentIds.length === 0) {
              return of(sharedActions.broadcastPreviewMessage({ msg }));
            }

            const requests = componentIds.map((componentId) => {
              const componentRequest = !forceRefresh && metadataCache[componentId]
                ? of<LoadResult<LinkedComponent>>({ value: metadataCache[componentId], error: null })
                : toLoadResult(this.linkedComponents.get(componentId));

              let contentRequest: Observable<LoadResult<TemplateModel>>;
              if (!forceRefresh && contentCache[componentId]) {
                contentRequest = of({ value: contentCache[componentId], error: null });
              } else if (!forceRefresh && errors[componentId]) {
                contentRequest = of({ value: null, error: errors[componentId] });
              } else {
                contentRequest = toLoadResult(this.linkedComponents.getContent(componentId));
              }

              return forkJoin({
                componentId: of(componentId),
                component: componentRequest,
                content: contentRequest,
              });
            });

            return forkJoin(requests).pipe(
              switchMap((results) => {
                const mergedContents = { ...contentCache };
                const mergedMetadata = { ...metadataCache };
                const outgoingActions: Action[] = [];

                if (forceRefresh) {
                  componentIds.forEach((componentId) => delete mergedContents[componentId]);
                }

                results.forEach((result) => {
                  if (result.component.value) {
                    mergedMetadata[result.componentId] = result.component.value;
                    outgoingActions.push(actions.cacheLinkedComponent({ component: result.component.value }));
                  }
                  if (result.content.value) {
                    mergedContents[result.componentId] = result.content.value;
                    outgoingActions.push(
                      actions.cacheLinkedComponentContent({
                        componentId: result.componentId,
                        content: result.content.value,
                      }),
                    );
                  } else if (result.content.error && (forceRefresh || !errors[result.componentId])) {
                    outgoingActions.push(
                      actions.linkedComponentLoadFailed({
                        componentId: result.componentId,
                        error: result.content.error,
                      }),
                      sharedActions.showNotification({
                        message: `Shared Component ${result.componentId} could not be loaded`,
                        msgType: 'error',
                        top: true,
                      }),
                    );
                  }
                });

                const resolved = resolveLinkedComponents(template, mergedContents);
                const boundaries = resolved.boundaries.map((boundary) => {
                  const component = mergedMetadata[boundary.componentRef];
                  return component ? { ...boundary, name: component.name, usageCount: component.usageCount } : boundary;
                });
                const resolvedMessage = STRUCTURAL_PREVIEW_MESSAGE_TYPES.has(msg.type)
                  ? { type: 'reload', template: resolved.template }
                  : { ...msg, template: resolved.template };
                outgoingActions.push(
                  sharedActions.broadcastPreviewMessage({
                    msg: {
                      ...resolvedMessage,
                      linkedComponentBoundaries: boundaries,
                      missingLinkedComponentIds: resolved.missingComponentIds,
                    },
                  }),
                );
                return outgoingActions;
              }),
            );
          }),
        ),
      ),
    ),
  );

  private canInsertLinkedComponents(): boolean {
    return this.appConfig.getValue('canInsertLinkedComponents') === true;
  }
}

const STRUCTURAL_PREVIEW_MESSAGE_TYPES = new Set(['add', 'remove', 'swap', 'update']);

function toLoadResult<T>(request: Observable<T>): Observable<LoadResult<T>> {
  return request.pipe(
    map((value) => ({ value, error: null })),
    catchError((error) => of({ value: null, error: getErrorMessage(error) })),
  );
}

function getErrorMessage(error: unknown): string {
  if (error && typeof error === 'object' && 'message' in error) {
    return String((error as { message: unknown }).message);
  }
  return String(error || 'Unknown error');
}
