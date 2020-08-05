import { BrowserModule } from '@angular/platform-browser';
import { NgModule, APP_INITIALIZER } from '@angular/core';
import { StoreModule, ActionReducer, MetaReducer } from '@ngrx/store';
import { StoreDevtoolsModule } from '@ngrx/store-devtools';
import { EffectsModule } from '@ngrx/effects';
import { BsDropdownModule } from 'ngx-bootstrap/dropdown';

import { CookieService } from 'ngx-cookie-service';

import { SharedModule } from '@shared/shared.module';
import { EditorModule } from '@editor/editor.module';
import { ThemeModule } from '@themes/theme.module';

import { AppComponent } from './app.component';
import { COMPONENTS } from './components';
// import { PreviewComponent } from './components/preview/preview.component';
// import { ToolbarComponent } from './components/toolbar/toolbar.component';

import { PlatformService } from 'src/app/services/platform.service';

import { ErrorsEffects } from './store/errors.effects';
import { RootEffects } from './store/root.effects';
import { reducer } from './store/root.reducer';
import { environment } from '../environments/environment';
import { HTTP_INTERCEPTORS, HttpClient } from '@angular/common/http';
import { APP_BASE_HREF } from '@angular/common';
import { ApiUrlsService, PreviewService, WindowRef } from '@app/services';
import { LoadingComponent } from './components/loading/loading.component';
import { RefreshTokenInterceptor } from './services/refresh-token.interceptor';
import { AppSettings } from './services/app.settings';

export function debug(actionReducer: ActionReducer<any>): ActionReducer<any> {
    return function (state, action) {
        console.log(state, action);
        return actionReducer(state, action);
    };
}

export const metaReducers: MetaReducer<any>[] = [debug];

@NgModule({
    declarations: [
        AppComponent,
        ...COMPONENTS,
        LoadingComponent
    ],
    imports: [
        BrowserModule,
        StoreModule.forRoot({
            'root': reducer
        }, { metaReducers }),
        StoreDevtoolsModule.instrument({
            name: 'CMS',
            maxAge: 25,
            logOnly: environment.production
        }),
        EffectsModule.forRoot([RootEffects, ErrorsEffects]),
        BsDropdownModule.forRoot({}),

        EditorModule,
        SharedModule,
        ThemeModule
    ],
    providers: [
        CookieService,
        // { provide: HTTP_INTERCEPTORS, useClass: AppHttpInterceptor, multi: true },
        { provide: HTTP_INTERCEPTORS, useClass: RefreshTokenInterceptor, multi: true },
        {
            provide: APP_INITIALIZER,
            useFactory: (platform: PlatformService) =>
                () => platform.initSettings(),
            deps: [PlatformService],
            multi: true
        },
        {
            provide: PlatformService,
            useFactory: (http: HttpClient, urls: ApiUrlsService) => {
                return new PlatformService(http, urls);
            },
            deps: [HttpClient, ApiUrlsService]
        },
        {
            provide: PreviewService,
            useFactory: () => {
                return new PreviewService();
            },
            deps: []
        },
        {
            provide: APP_BASE_HREF,
            useFactory: (windowRef: WindowRef) => {
                console.log(windowRef.nativeWindow.location);
                console.log(AppSettings);
            },
            deps: [WindowRef]
        }
    ],
    bootstrap: [AppComponent]
})
export class AppModule { }
