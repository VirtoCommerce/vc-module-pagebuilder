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

import { isSharedComponentReference, resolveSharedComponents } from '@editor/helpers';
import { SharedComponent } from '@editor/models';
import { SHARED_COMPONENTS_PAGE_SIZE, SharedComponentsService } from '@editor/services';
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
export class SharedComponentsDataEffects {
  private readonly actions$ = inject(Actions);
  private readonly store = inject(Store<BuilderState>);
  private readonly sharedComponents = inject(SharedComponentsService);
  private readonly appConfig = inject(AppConfig);

  search$ = createEffect(() =>
    this.actions$.pipe(
      ofType(
        actions.searchSharedComponents,
        actions.retrySharedComponentsSearch,
        actions.refreshSharedComponentsSearch,
      ),
      filter(() => this.canInsertSharedComponents()),
      filter((action) => !('skip' in action) || !(action.skip ?? 0)),
      switchMap((action) => {
        const keyword = action.keyword.trim();
        const rebase = action.type === actions.refreshSharedComponentsSearch.type;
        return action.type === actions.searchSharedComponents.type
          ? timer(250).pipe(switchMap(() => this.searchRequest(keyword, 0, rebase)))
          : this.searchRequest(keyword, 0, rebase);
      }),
    ),
  );

  refreshSearchAfterCreate$ = createEffect(() =>
    this.actions$.pipe(
      ofType(actions.cacheSharedComponent),
      filter(({ addToSearchResults }) => addToSearchResults === true),
      withLatestFrom(this.store.select(selectors.selectSharedComponentsSearchState)),
      filter(([{ component }, search]) => matchesKeyword(component.name, search.keyword)),
      map(([, search]) => actions.refreshSharedComponentsSearch({ keyword: search.keyword })),
    ),
  );

  loadMore$ = createEffect(() =>
    this.actions$.pipe(
      ofType(actions.searchSharedComponents, actions.retrySharedComponentsSearch),
      filter(() => this.canInsertSharedComponents()),
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
      ofType(actions.loadSharedComponentDetails, actions.clearSharedComponentDetails),
      switchMap((action) =>
        'componentId' in action
          ? this.sharedComponents.get(action.componentId).pipe(
              map((component) => actions.loadSharedComponentDetailsSuccess({ component })),
              catchError((error) =>
                of(
                  actions.loadSharedComponentDetailsFailed({
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
      withLatestFrom(this.store.select(selectors.selectSharedComponentUsageRefreshIdsByTemplate)),
      concatMap(([{ template, templateKey }, refreshIdsByTemplate]) => {
        const componentIds = [
          ...new Set([
            ...template.content.filter(isSharedComponentReference).map((reference) => reference.componentRef),
            ...(refreshIdsByTemplate[templateKey] || []),
          ]),
        ];
        if (componentIds.length === 0) {
          return of(sharedActions.empty());
        }

        return forkJoin(componentIds.map((componentId) => toLoadResult(this.sharedComponents.get(componentId)))).pipe(
          switchMap((results) => {
            const outgoingActions: Action[] = results.flatMap((result) =>
              result.value ? [actions.cacheSharedComponent({ component: result.value })] : [],
            );
            if (results.every((result) => result.value !== null)) {
              outgoingActions.push(actions.clearSharedComponentUsageRefresh({ templateKey }));
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
            this.store.select(selectors.selectSharedComponents),
            this.store.select(selectors.selectSharedComponentContents),
            this.store.select(selectors.selectSharedComponentErrors),
          ),
          switchMap(([msg, metadataCache, contentCache, errors]) => {
            const template = msg['template'] as TemplateModel | null | undefined;
            if (!template) {
              return of(sharedActions.broadcastPreviewMessage({ msg }));
            }

            const componentIds = [
              ...new Set(
                template.content.filter(isSharedComponentReference).map((reference) => reference.componentRef),
              ),
            ];
            const forceRefresh = msg.type === 'page' || msg.type === 'reload';

            if (componentIds.length === 0) {
              return of(sharedActions.broadcastPreviewMessage({ msg }));
            }

            const requests = componentIds.map((componentId) => {
              const componentRequest =
                !forceRefresh && metadataCache[componentId]
                  ? of<LoadResult<SharedComponent>>({ value: metadataCache[componentId], error: null })
                  : toLoadResult(this.sharedComponents.get(componentId));

              let contentRequest: Observable<LoadResult<TemplateModel>>;
              if (!forceRefresh && contentCache[componentId]) {
                contentRequest = of({ value: contentCache[componentId], error: null });
              } else if (!forceRefresh && errors[componentId]) {
                contentRequest = of({ value: null, error: errors[componentId] });
              } else {
                contentRequest = toLoadResult(this.sharedComponents.getContent(componentId));
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
                    outgoingActions.push(actions.cacheSharedComponent({ component: result.component.value }));
                  }
                  if (result.content.value) {
                    mergedContents[result.componentId] = result.content.value;
                    outgoingActions.push(
                      actions.cacheSharedComponentContent({
                        componentId: result.componentId,
                        content: result.content.value,
                      }),
                    );
                  } else if (result.content.error && (forceRefresh || !errors[result.componentId])) {
                    outgoingActions.push(
                      actions.sharedComponentLoadFailed({
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

                const resolved = resolveSharedComponents(template, mergedContents);
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
                      sharedComponentBoundaries: boundaries,
                      missingSharedComponentIds: resolved.missingComponentIds,
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

  private canInsertSharedComponents(): boolean {
    return this.appConfig.getValue('canInsertSharedComponents') === true;
  }

  private nextFirstPageRequest() {
    return this.actions$.pipe(
      ofType(
        actions.searchSharedComponents,
        actions.retrySharedComponentsSearch,
        actions.refreshSharedComponentsSearch,
      ),
      filter((action) => !('skip' in action) || !(action.skip ?? 0)),
    );
  }

  private searchRequest(keyword: string, skip: number, rebase = false) {
    return this.sharedComponents.search(keyword, skip, SHARED_COMPONENTS_PAGE_SIZE).pipe(
      map((result) =>
        actions.searchSharedComponentsSuccess({
          keyword,
          result,
          append: skip > 0,
          ...(rebase ? { rebase: true } : {}),
        }),
      ),
      catchError((error) =>
        of(
          actions.searchSharedComponentsFailed({
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
