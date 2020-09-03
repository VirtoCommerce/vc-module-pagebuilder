import { Injectable } from '@angular/core';
import { Store } from '@ngrx/store';
import { Actions, ofType, createEffect } from '@ngrx/effects';
import { of } from 'rxjs';
import {
    map,
    catchError,
    withLatestFrom,
    switchMap,
    debounceTime,
    distinctUntilChanged,
    switchMapTo,
    tap,
    filter
} from 'rxjs/operators';

import { AppSettings } from '@app/services';
import { MessageService } from '@shared/services';
import { ThemeService } from '@themes/services';
import * as themeActions from './theme.actions';
import * as fromTheme from '.';

@Injectable()
export class ThemeEffects {
    constructor(private themeService: ThemeService,
        private actions$: Actions,
        private messages: MessageService,
        private store$: Store<fromTheme.State>) { }

    loadDefaultThemes$ = createEffect(() => this.actions$.pipe(
        ofType(themeActions.loadPresets),
        switchMap(() =>
            this.themeService.loadPresets().pipe(
                map(presets => themeActions.loadPresetsSuccess(presets)),
                catchError(error => of(themeActions.loadPresetsFail({ error })))
            )
        )
    ));

    loadSchema$ = createEffect(() => this.actions$.pipe(
        ofType(themeActions.loadSchema),
        switchMap(() =>
            this.themeService.loadSchema().pipe(
                map(schema => themeActions.loadSchemaSuccess({ schema })),
                catchError(error => of(themeActions.loadSchemaFail({ error })))
            )
        )
    ));

    uploadPreviewPreset$ = createEffect(() => this.actions$.pipe(
        ofType(
            themeActions.previewPreset,
            themeActions.updateTheme,
            themeActions.loadPresetsSuccess,
            themeActions.clearThemeChanges
        ),
        debounceTime(2000),
        distinctUntilChanged(),
        switchMapTo([themeActions.updateDraft()])
    ));

    cancelPresetEditing$ = createEffect(() => this.actions$.pipe(
        ofType(
            themeActions.cancelPreset,
            themeActions.closeEditors
        ),
        withLatestFrom(this.store$.select(fromTheme.getPresetChanged)),
        filter(([, changed]) => changed),
        switchMapTo([
            themeActions.updateDraft(),
            themeActions.cancelPresetComplete()
        ])
    ));

    uploadPresets$ = createEffect(() => this.actions$.pipe(
        ofType(themeActions.saveTheme),
        withLatestFrom(
            this.store$.select(fromTheme.getEditablePreset),
            this.store$.select(fromTheme.getPresets),
            this.store$.select(fromTheme.getBasePresets),
            this.store$.select(fromTheme.getSchema),
            this.store$.select(fromTheme.getPresetsNotLoaded),
            this.store$.select(fromTheme.getIsDirty)
        ),
        filter(([, , , , , themeNotLoaded, hasChanges]) => !themeNotLoaded && hasChanges),
        switchMap(([, values, presets, basePresets, schema]) => {
            return this.themeService.uploadPresets(values, presets, basePresets, schema).pipe(
                map(() => themeActions.saveThemeSuccess({ values })),
                catchError(error => of(themeActions.saveThemeFail({ error })))
            );
        })
    ));

    updateDraft$ = createEffect(() => this.actions$.pipe(
        ofType(themeActions.updateDraft),
        withLatestFrom(
            this.store$.select(fromTheme.getEditablePreset),
            this.store$.select(fromTheme.getPresetsNotLoaded)
        ),
        filter(([, , themeNotLoaded]) => !themeNotLoaded),
        switchMap(([, values]) =>
            this.themeService.uploadDraft(values).pipe(
                map(() => themeActions.updateDraftSuccess()),
                catchError(error => of(themeActions.updateDraftFail({ error })))
            )
        )
    ));

    uploadError$ = createEffect(() => this.actions$.pipe(
        ofType(themeActions.saveThemeFail),
        tap((action) => {
            this.messages.displayError('Couldn\'t save theme', action.error);
        })
    ), { dispatch: false });

    uploadDraftError$ = createEffect(() => this.actions$.pipe(
        ofType(themeActions.updateDraftFail),
        tap((action) => {
            this.messages.displayError('Couldn\'t connect server', action.error);
        })
    ), { dispatch: false });

    uploadDraftSuccess$ = createEffect(() => this.actions$.pipe(
        ofType(themeActions.saveThemeSuccess),
        tap(() => {
            this.messages.displayMessage('Theme saved successfully');
        })
    ), { dispatch: false });
}
