import { Injectable } from '@angular/core';
import { Action, Store } from '@ngrx/store';
import { Actions, Effect, ofType, createEffect } from '@ngrx/effects';
import { Observable, of } from 'rxjs';
import {
    mergeMap,
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

    loadPresets$ = createEffect(() => this.actions$.pipe(
        ofType(themeActions.loadThemes),
        switchMap(() =>
            this.themeService.loadPresets().pipe(
                map(presets => themeActions.loadThemesSuccess({ presets })),
                catchError(error => of(themeActions.loadThemesFail({ error })))
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

    uploadPresets$ = createEffect(() => this.actions$.pipe(
        ofType(themeActions.saveTheme),
        withLatestFrom(
            this.store$.select(fromTheme.getPresets),
            this.store$.select(fromTheme.getPresetsNotLoaded)
        ),
        filter(([, , themeNotLoaded]) => !themeNotLoaded),
        switchMap(([, theme]) =>
            this.themeService.uploadPresets(theme).pipe(
                map(() => themeActions.saveThemeSuccess()),
                catchError(error => of(themeActions.saveThemeFail({ error })))
            )
        )
    ));

    uploadPreviewPreset$ = createEffect(() => this.actions$.pipe(
        ofType(
            themeActions.updateTheme,
            themeActions.selectPreset,
            themeActions.loadThemesSuccess,
            themeActions.clearThemeChanges,
            themeActions.cancelPreset,
            themeActions.applyPreset),
        debounceTime(2000),
        distinctUntilChanged(),
        switchMapTo([themeActions.updateDraft()])
    ));

    updateDraft$ = createEffect(() => this.actions$.pipe(
        ofType(themeActions.updateDraft),
        withLatestFrom(
            this.store$.select(fromTheme.getPresets),
            this.store$.select(fromTheme.getPresetsNotLoaded)
        ),
        filter(([, , themeNotLoaded]) => !themeNotLoaded),
        switchMap(([, theme]) =>
            this.themeService.uploadDraft(theme).pipe(
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
