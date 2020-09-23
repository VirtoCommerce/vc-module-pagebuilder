import { HttpErrorResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Actions, Effect, ofType, createEffect } from '@ngrx/effects';
import { tap, filter, map } from 'rxjs/operators';

import { MessageService } from '@shared/services';

import * as themeActions from '@themes/store/theme.actions';
import * as editorActions from '@editor/store/editor.actions';
import * as rootActions from '@app/store/root.actions';

@Injectable()
export class ErrorsEffects {
    constructor(private actions$: Actions, private errors: MessageService) { }

    loadBlocksSchemaFail$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.blocksSchemaFail),
        map((action: any) => <HttpErrorResponse>action.error),
        tap(response => this.errors.displayError(this.checkResponse('Couldn\'t load blocks schema.', response), response))
    ), { dispatch: false });

    loadPageFail$ = createEffect(() => this.actions$.pipe(
        ofType(editorActions.loadBlocksFail),
        map((action: any) => <HttpErrorResponse>action.error),
        tap(response => this.errors.displayError(this.checkResponse('Couldn\'t load page.', response), response))
    ), { dispatch: false });

    loadThemesFail$ = createEffect(() => this.actions$.pipe(
        ofType(themeActions.loadDefaultThemesFail),
        map((action: any) => <HttpErrorResponse>action.error),
        tap(response => this.errors.displayError(this.checkResponse('Couldn\'t load theme settings.', response), response))
    ), { dispatch: false });

    loadThemeSchemaFail$ = createEffect(() => this.actions$.pipe(
        ofType(themeActions.loadSchemaFail),
        map((action: any) => <HttpErrorResponse>action.error),
        tap(response => this.errors.displayError(this.checkResponse('Couldn\'t load theme schema.', response), response))
    ), { dispatch: false });

    displayError$ = createEffect(() => this.actions$.pipe(
        ofType(rootActions.displayError),
        tap(({ error }) => this.errors.displayError(error, {}))
    ), { dispatch: false });

    private checkResponse(error: string, response: HttpErrorResponse): string {
        if (response.status < 400) {
            return error + ' May be file has a wrong format.'
        }
        return error;
    }
}
