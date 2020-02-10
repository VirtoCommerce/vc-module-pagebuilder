import { HttpErrorResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Actions, Effect, ofType, createEffect } from '@ngrx/effects';
import { tap, filter, map } from 'rxjs/operators';

import { MessageService } from '@shared/services';

import * as themeActions from '@themes/store/theme.actions';
import * as editorActions from '@editor/store/editor.actions';

@Injectable()
export class ErrorsEffects {
    constructor(private actions$: Actions, private errors: MessageService) { }

    loadBlocksSchemaFail$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.blocksSchemaFail),
        map((action: any) => <HttpErrorResponse>action.payload),
        filter(response => response.status >= 400),
        tap(response => this.errors.displayError('Couldn\'t load blocks schema', response))
    ), { dispatch: false });

    loadPageFail$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.loadPageFail),
        map((action: any) => <HttpErrorResponse>action.payload),
        filter(response => response.status >= 400),
        tap(response => this.errors.displayError('Couldn\'t load page', response))
    ), { dispatch: false });

    loadThemesFail$ = createEffect(() => this.actions$.pipe(
        ofType(themeActions.loadThemesFail),
        map((action: any) => <HttpErrorResponse>action.payload),
        filter(response => response.status >= 400),
        tap(response => this.errors.displayError('Couldn\'t load theme settings', response))
    ), { dispatch: false });

    loadThemeSchemaFail$ = createEffect(() => this.actions$.pipe(
        ofType(themeActions.loadSchemaFail),
        map((action: any) => <HttpErrorResponse>action.error),
        filter(response => response.status >= 400),
        tap(response => this.errors.displayError('Couldn\'t load theme schema', response))
    ), { dispatch: false });
}
