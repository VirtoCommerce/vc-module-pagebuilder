import { ApplicationConfig, importProvidersFrom, inject, isDevMode, provideAppInitializer } from '@angular/core';
import { provideRouter, withHashLocation } from '@angular/router';
import { provideHttpClient, withInterceptorsFromDi, HTTP_INTERCEPTORS } from '@angular/common/http';

import { provideStore, provideState } from '@ngrx/store';
import { provideEffects } from '@ngrx/effects';
import { provideRouterStore, routerReducer } from '@ngrx/router-store';
import { provideStoreDevtools } from '@ngrx/store-devtools';

import { MatDialogModule } from '@angular/material/dialog';
import { MatNativeDateModule } from 'ngv-datepicker';
import { ToastrModule } from 'ngx-toastr';

import { APP_ROUTES } from './app.routes';
import { initialState as initialRoute } from '@shared/routing';
import { RoutingEffects } from '@shared/routing/effects';
import { RouterSerializer } from '@shared/routing/serializer';
import { SharedEffects } from '@shared/store/effects';
import { sharedReducers } from '@shared/store/reducers';

import { RefreshTokenInterceptor } from '@integration/services';
import { AppInitializator } from '@integration/services/app.initializator';
import { registerControls } from '@core/controls/controls-register';

export const appConfig: ApplicationConfig = {
    providers: [
        provideRouter(APP_ROUTES, withHashLocation()),
        provideHttpClient(withInterceptorsFromDi()),

        provideStore({ router: routerReducer }, {
            initialState: { router: initialRoute }
        }),
        provideRouterStore({ serializer: RouterSerializer }),
        provideEffects([RoutingEffects]),
        provideStoreDevtools({
            name: 'Builder',
            maxAge: 25,
            logOnly: !isDevMode(),
            actionsBlocklist: [
                '[shared] broadcast preview message',
                '[template editor] hover section',
            ]
        }),

        provideState('shared', sharedReducers),
        provideEffects([SharedEffects]),

        {
            provide: HTTP_INTERCEPTORS,
            useClass: RefreshTokenInterceptor,
            multi: true
        },
        provideAppInitializer(() => {
        const initializerFn = ((config: AppInitializator) => () => config.init())(inject(AppInitializator));
        return initializerFn();
      }),
        provideAppInitializer(() => {
        const initializerFn = (registerControls)();
        return initializerFn();
      }),

        importProvidersFrom(
            MatDialogModule,
            MatNativeDateModule,
            ToastrModule.forRoot()
        )
    ]
};
