import { Injectable, inject } from "@angular/core";
import { Actions, createEffect, ofType } from "@ngrx/effects";
import { Store } from "@ngrx/store";
import { withLatestFrom, filter, switchMap, map, tap } from "rxjs/operators";

import * as actions from "../actions";
import { BuilderState } from "../state";

import * as sharedActions from '@shared/store/actions';

import * as routingActions from '@shared/routing/actions';
import * as routingSelectors from '@shared/routing'

import * as domainSelectors from "../selectors";

@Injectable({
    providedIn: 'root'
})
export class ThemeDomainEffects {
    private readonly store$ = inject(Store<BuilderState>);
    private readonly actions$ = inject(Actions);

    toggleGroup$ = createEffect(() => this.actions$.pipe(
        ofType(actions.toggleGroup),
        withLatestFrom(
            this.store$.select(domainSelectors.selectOpenedGroups)
        ),
        map(([{ group }, groups]) => {
            // const route = this.getAllRouteParameters(currentRoute);
            const newGroups = groups.indexOf(group.name) === -1 ? [...groups.filter(x => !!x), group.name] : [...groups.filter(g => g !== group.name)];
            return routingActions.go({ queryParams: { groups: newGroups.join(',') || undefined } })
        })
    ));

    previewPresetAfterLoading$ = createEffect(() => this.actions$.pipe(
        ofType(actions.loadSettingsDataSuccess),
        withLatestFrom(
            this.store$.select(routingSelectors.selectPresetParameter)
        ),
        filter(([, preset]) => !!preset),
        map(([action, preset]) => action.settingsData?.presets?.[preset]),
        filter(preset => !!preset),
        map(preset => actions.updateInPreview({ settings: preset }))
    ));

    previewPreset$ = createEffect(() => this.actions$.pipe(
        ofType(actions.previewPreset),
        withLatestFrom(
            this.store$.select(domainSelectors.selectPresets)
        ),
        map(([{ preset }, presets]) => presets?.[preset]),
        filter(preset => !!preset),
        map(preset => actions.updateInPreview({ settings: preset }))
    ));

    presetApplied$ = createEffect(() => this.actions$.pipe(
        ofType(actions.applyPreset),
        map(() => sharedActions.showNotification({ message: 'Preset applied', msgType: 'success' }))
    ));

    saveSettingsSuccess$ = createEffect(() => this.actions$.pipe(
        ofType(actions.saveSettingsSuccess),
        map(() => sharedActions.showNotification({ message: 'Settings were successfully saved', msgType: 'success', top: true }))
    ));

    saveSettingsFail$ = createEffect(() => this.actions$.pipe(
        ofType(actions.saveSettingsFail),
        tap((error) => console.log(error)),
        map(() => sharedActions.showNotification({ message: 'Could not save settings', msgType: 'error', top: true }))
    ));

    cancelAction$ = createEffect(() => this.actions$.pipe(
        ofType(actions.executeAction),
        filter(({ action }) => action === 'cancel'),
        switchMap(() => [
            actions.revertChanges(),
            actions.exitSettings(),
            actions.updateInPreview({ settings: null })
        ])
    ));

    revertChanges$ = createEffect(() => this.actions$.pipe(
        ofType(actions.revertChanges),
        map(() => sharedActions.showNotification({ message: 'Changes were reverted', msgType: 'success', top: true }))
    ));

    applyAction$ = createEffect(() => this.actions$.pipe(
        ofType(actions.executeAction),
        filter(({ action }) => action === 'save'),
        switchMap(() => [
            actions.saveSettings()
        ])
    ));

    successfullSaved$ = createEffect(() => this.actions$.pipe(
        ofType(actions.saveSettingsSuccess),
        switchMap(() => [
            actions.applyChanges(),
            actions.exitSettings()
        ])
    ));

    updateInPreview$ = createEffect(() => this.actions$.pipe(
        ofType(actions.updateInPreview),
        withLatestFrom(
            this.store$.select(domainSelectors.selectCurrentSettings)
        ),
        map(([{ settings }, currentSettings]) =>
            sharedActions.broadcastPreviewMessage({ msg: { type: 'settings', settings: settings || currentSettings } })
        )
    ));
}
