import { Injectable, inject } from "@angular/core";
import { Actions, createEffect, ofType } from "@ngrx/effects";
import { Store } from "@ngrx/store";
import { withLatestFrom, filter, mapTo, map, switchMapTo } from "rxjs/operators";

import * as actions from "../actions";
import { BuilderState } from "../state";

import { broadcastPreviewMessage } from '@shared/store/actions';
import * as routingActions from '@shared/routing/actions';
import * as routingSelectors from '@shared/routing'

import * as domainSelectors from "../selectors";
import { ActivatedRouteSnapshot } from "@angular/router";

@Injectable({
    providedIn: 'root'
})
export class ThemeUiEffects {
    private readonly store$ = inject(Store<BuilderState>);
    private readonly actions$ = inject(Actions);

    gotoPresets$ = createEffect(() => this.actions$.pipe(
        ofType(actions.gotoPresets),
        mapTo(routingActions.go({ path: ['/themes/presets'] }))
    ));

    gotoPreviewPreset$ = createEffect(() => this.actions$.pipe(
        ofType(actions.previewPreset),
        map(({ preset }) => routingActions.go({ queryParams: { preset } }))
    ));

    redirectWhenNoPreset$ = createEffect(() => this.actions$.pipe(
        ofType(actions.loadSettingsDataSuccess),
        withLatestFrom(
            this.store$.select(routingSelectors.selectPresetParameter)
        ),
        filter(([, preset]) => !!preset),
        map(([action, preset]) => action.settingsData?.presets?.[preset]),
        filter(preset => !preset),
        map(() => actions.gotoPresets())
    ));

    exitPresets$ = createEffect(() => this.actions$.pipe(
        ofType(actions.exitPresets, actions.applyPreset),
        switchMapTo([
            routingActions.go({ path: ['/themes'], queryParams: { preset: undefined } }),
            actions.updateInPreview({ settings: null })
        ])
    ));

    exitSettings$ = createEffect(() => this.actions$.pipe(
        ofType(actions.exitSettings),
        mapTo(routingActions.jump({ path: ['/pages'] }))
    ));

    notifySettingsChanged$ = createEffect(() => this.actions$.pipe(
        ofType(actions.updateSettings),
        map(({ model }) => actions.updateInPreview({ settings: model }))
    ));
}
