import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Action, Store } from '@ngrx/store';
import {
  catchError,
  concatMap,
  EMPTY,
  exhaustMap,
  filter,
  forkJoin,
  map,
  Observable,
  of,
  switchMap,
  takeUntil,
  timer,
  withLatestFrom,
} from 'rxjs';

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
      ofType(
        actions.searchLinkedComponents,
        actions.retryLinkedComponentsSearch,
        actions.refreshLinkedComponentsSearch,
      ),
      filter(() => this.canInsertLinkedComponents()),
      filter((action) => !('skip' in action) || !(action.skip ?? 0)),
      switchMap((action) => {
        const keyword = action.keyword.trim();
        const rebase = action.type === actions.refreshLinkedComponentsSearch.type;
        return action.type === actions.searchLinkedComponents.type
          ? timer(250).pipe(switchMap(() => this.searchRequest(keyword, 0, rebase)))
          : this.searchRequest(keyword, 0, rebase);
      }),
    ),
  );

  refreshSearchAfterCreate$ = createEffect(() =>
    this.actions$.pipe(
      ofType(actions.cacheLinkedComponent),
      filter(({ addToSearchResults }) => addToSearchResults === true),
      withLatestFrom(this.store.select(selectors.selectLinkedComponentsSearchState)),
      filter(([{ component }, search]) => matchesKeyword(component.name, search.keyword)),
      map(([, search]) => actions.refreshLinkedComponentsSearch({ keyword: search.keyword })),
    ),
  );

  loadMore$ = createEffect(() =>
    this.actions$.pipe(
      ofType(actions.searchLinkedComponents, actions.retryLinkedComponentsSearch),
      filter(() => this.canInsertLinkedComponents()),
      filter(({ skip = 0 }) => skip > 0),
      exhaustMap(({ keyword, skip = 0 }) =>
        this.searchRequest(keyword.trim(), skip).pipe(
          takeUntil(this.nextFirstPageRequest()),
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
      withLatestFrom(this.store.select(selectors.selectLinkedComponentUsageRefreshIdsByTemplate)),
      concatMap(([{ template, templateKey }, refreshIdsByTemplate]) => {
        const componentIds = [
          ...new Set([
            ...template.content.filter(isLinkedComponentReference).map((reference) => reference.componentRef),
            ...(refreshIdsByTemplate[templateKey] || []),
          ]),
        ];
        if (componentIds.length === 0) {
          return of(sharedActions.empty());
        }

        return forkJoin(componentIds.map((componentId) => toLoadResult(this.linkedComponents.get(componentId)))).pipe(
          switchMap((results) => {
            const outgoingActions: Action[] = results.flatMap((result) =>
              result.value ? [actions.cacheLinkedComponent({ component: result.value })] : [],
            );
            if (results.every((result) => result.value !== null)) {
              outgoingActions.push(actions.clearLinkedComponentUsageRefresh({ templateKey }));
            }
            return outgoingActions;
          }),
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
              const componentRequest =
                !forceRefresh && metadataCache[componentId]
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
                  return component
                    ? {
                        ...boundary,
                        label: getBoundaryLabel(component.usageCount),
                      }
                    : { ...boundary, label: 'Shared' };
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

  private nextFirstPageRequest() {
    return this.actions$.pipe(
      ofType(
        actions.searchLinkedComponents,
        actions.retryLinkedComponentsSearch,
        actions.refreshLinkedComponentsSearch,
      ),
      filter((action) => !('skip' in action) || !(action.skip ?? 0)),
    );
  }

  private searchRequest(keyword: string, skip: number, rebase = false) {
    return this.linkedComponents.search(keyword, skip, LINKED_COMPONENTS_PAGE_SIZE).pipe(
      map((result) =>
        actions.searchLinkedComponentsSuccess({
          keyword,
          result,
          append: skip > 0,
          ...(rebase ? { rebase: true } : {}),
        }),
      ),
      catchError((error) =>
        of(
          actions.searchLinkedComponentsFailed({
            keyword,
            error: getErrorMessage(error),
          }),
        ),
      ),
    );
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

function getBoundaryLabel(usageCount: number): string {
  return `Shared · Used on ${usageCount} page${usageCount === 1 ? '' : 's'}`;
}

function matchesKeyword(name: string, keyword: string): boolean {
  return name.toLocaleLowerCase().includes(keyword.trim().toLocaleLowerCase());
}
