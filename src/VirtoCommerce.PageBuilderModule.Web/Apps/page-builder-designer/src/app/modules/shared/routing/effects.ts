import { withLatestFrom } from 'rxjs';
import { Injectable, inject } from '@angular/core';
import { Location } from '@angular/common';
import { Router } from '@angular/router';
import { filter, tap } from 'rxjs/operators';
import { Actions, ofType, createEffect } from '@ngrx/effects';
import * as actions from './actions';
import * as fromRoute from '.';
import { Store } from '@ngrx/store';
import { BuilderState } from '.';

@Injectable({
  providedIn: 'root'
})
export class RoutingEffects {

  private readonly actions$ = inject(Actions);
  private readonly store$ = inject(Store<BuilderState>);
  private readonly router = inject(Router);
  private readonly location = inject(Location);

  navigate$ = createEffect(() => this.actions$.pipe(
    ofType(actions.go),
    withLatestFrom(
      this.store$.select(fromRoute.selectPath),
      this.store$.select(fromRoute.selectQueryParams),
      this.store$.select(fromRoute.isEmpty)
    ),
    filter(([, , , isEmpty]) => !isEmpty),
    tap(([{ path, queryParams, extras }, currentPath, currentParams]) =>
      this.router.navigate(
        // todo: use default route from config
        path || [currentPath], // save current path if it isn't exists
        {
          queryParams: { ...currentParams, ...queryParams }, // save current query params too
          ...extras
        }
      )
    )
  ), { dispatch: false });

  navigateToOtherModule$ = createEffect(() => this.actions$.pipe(
    ofType(actions.jump),
    withLatestFrom(
      this.store$.select(fromRoute.selectTypeParameter),
      this.store$.select(fromRoute.selectPathParameter),
      this.store$.select(fromRoute.selectParentTemplateParameter),
      this.store$.select(fromRoute.selectPreviewModeParameter),
      this.store$.select(fromRoute.selectGroupIdParameter),
      this.store$.select(fromRoute.selectLinkedComponentIdParameter)
    ),
    tap(([{ path, queryParams, extras }, type, pathParameter, parent, previewMode, groupId, linkedComponentId]) =>
      this.router.navigate(path,
        {
          queryParams: {
            type: type || undefined,
            path: pathParameter || undefined,
            parent: parent || undefined,
            'preview-mode': previewMode || undefined,
            groupId: groupId || undefined,
            linkedComponentId: linkedComponentId || undefined,
            ...queryParams
          },
          ...extras
        }
      ))
  ), { dispatch: false });

  skipNavigate$ = createEffect(() => this.actions$.pipe(
    ofType(actions.skipNavigation),
    tap((reason) => console.log('Navigation was skipped', reason))
  ), { dispatch: false });

  navigateBack$ = createEffect(() => this.actions$.pipe(
    ofType(actions.back),
    tap(() => this.location.back())
  ), { dispatch: false });

  navigateForward$ = createEffect(() => this.actions$.pipe(
    ofType(actions.forward),
    tap(() => this.location.forward())
  ), { dispatch: false });
}
