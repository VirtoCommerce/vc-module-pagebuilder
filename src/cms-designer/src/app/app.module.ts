import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { NgModule, APP_INITIALIZER } from '@angular/core';
import { StoreModule } from '@ngrx/store';
import { StoreDevtoolsModule } from '@ngrx/store-devtools';
import { EffectsModule } from '@ngrx/effects';
import { BsDropdownModule } from "ngx-bootstrap/dropdown";

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
import { ApiUrlsService, PreviewService } from '@app/services';
import { LoadingComponent } from './components/loading/loading.component';
import { RefreshTokenInterceptor } from './services/refresh-token.interceptor';

import { metaReducers, actionsToIgonre } from './debug';

@NgModule({
    declarations: [
        AppComponent,
        ...COMPONENTS,
        LoadingComponent
    ],
    imports: [
        BrowserModule,
        BrowserAnimationsModule,
        StoreModule.forRoot({
            root: reducer
        }, { metaReducers }),
        StoreDevtoolsModule.instrument({
            name: 'PageBuilder',
            maxAge: 25,
            logOnly: environment.production,
            actionsBlocklist: [
                ...actionsToIgonre
            ]
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
        }
    ],
    bootstrap: [AppComponent]
})
export class AppModule { }
