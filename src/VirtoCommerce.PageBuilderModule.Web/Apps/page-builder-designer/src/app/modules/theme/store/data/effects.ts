import { Injectable, inject } from "@angular/core";
import { of } from "rxjs";
import { withLatestFrom, filter, switchMap, mergeMap, map, catchError } from "rxjs/operators";
import { Actions, createEffect, ofType } from "@ngrx/effects";
import { Store } from "@ngrx/store";
import { ROUTER_NAVIGATED, RouterNavigatedAction } from "@ngrx/router-store";

import * as fromRoute from '@shared/routing';
import { RouterStateUrl } from '@shared/routing';

import { ThemeSettingsService } from '@theme/services';

import * as shared from '@shared/store/actions';
import * as actions from "../actions";
import * as selectors from "../selectors";
import { BuilderState } from "../state";

@Injectable({
    providedIn: 'root'
})
export class ThemeDataEffects {
    private readonly store$ = inject(Store<BuilderState>);
    private readonly actions$ = inject(Actions);
    private readonly service = inject(ThemeSettingsService);

    // flow: navigation complete -> check correct module
    // raise load data
    // load data
    // load complete

    initModule$ = createEffect(() => this.actions$.pipe(
        ofType(ROUTER_NAVIGATED),
        filter((action: RouterNavigatedAction<RouterStateUrl>) => !!action?.payload?.routerState?.data),
        map((action: RouterNavigatedAction<RouterStateUrl>) => action.payload.routerState.data),
        filter((data: any) => data.module === 'theme'),
        switchMap(() => [
            actions.raiseLoadData()
        ])
    ));

    raiseLoadData$ = createEffect(() => this.actions$.pipe(
        ofType(actions.raiseLoadData),
        withLatestFrom(
            this.store$.select(selectors.selectCurrentSettings),
        ),
        filter(([, settings]) => settings === null),
        switchMap(() => [actions.loadSettingsData(), actions.loadSettingsSchema()])
    ));

    // The module is entered on navigation only, and the route does not change when an expired
    // session is restored - so the requests that were dropped along with the session have to be
    // repeated explicitly, or the theme editor stays empty until the user reloads (VCST-5847).
    reloadAfterSignIn$ = createEffect(() => this.actions$.pipe(
        ofType(shared.initShared),
        withLatestFrom(
            this.store$.select(fromRoute.selectDataParams),
            this.store$.select(selectors.selectCurrentSettings),
            this.store$.select(selectors.selectLoadedSettingsSchema),
        ),
        filter(([, routeData]) => routeData?.['module'] === 'theme'),
        mergeMap(([, , settings, schema]) => [
            ...(settings ? [] : [actions.loadSettingsData()]),
            ...(schema ? [] : [actions.loadSettingsSchema()]),
        ])
    ));

    loadSettingsData$ = createEffect(() => this.actions$.pipe(
        ofType(actions.loadSettingsData),
        switchMap(() => this.service.loadSettingsData().pipe(
            map(settingsData => actions.loadSettingsDataSuccess({ settingsData })),
            catchError(error => of(actions.loadSettingsDataFail({ error })))
        ))
    ));

    loadSettingsSchema$ = createEffect(() => this.actions$.pipe(
        ofType(actions.loadSettingsSchema),
        switchMap(() => this.service.loadSettingsSchema().pipe(
            map(schema => actions.loadSettingsSchemaSuccess({ schema })),
            catchError(error => of(actions.loadSettingsSchemaFail({ error })))
        ))
    ));

    mergeServerSettingsSchema$ = createEffect(() => this.actions$.pipe(
        ofType(actions.loadSettingsSchemaSuccess),
      withLatestFrom(this.store$.select(selectors.selectCurrentSettingsDataModel)),
        map(([{ schema }]) => actions.useSettingsSchema({ schema }))
    ));

    mergeCustomSettingsSchema$ = createEffect(() => this.actions$.pipe(
        ofType(shared.updateCustomSchemas),
        filter(({ schemas }) => !!schemas?.settingsSchema?.settings_schema),
        map(({ schemas }) => actions.useSettingsSchema({ schema: schemas.settingsSchema.settings_schema }))
    ));

    saveSettings$ = createEffect(() => this.actions$.pipe(
        ofType(actions.saveSettings),
        withLatestFrom(this.store$.select(selectors.selectCurrentSettingsDataModel)),
        switchMap(([, settings]) => this.service.saveSettings(settings!).pipe(
            map(() => actions.saveSettingsSuccess()),
            catchError(error => of(actions.saveSettingsFail({ error })))
        ))
    ));
}
