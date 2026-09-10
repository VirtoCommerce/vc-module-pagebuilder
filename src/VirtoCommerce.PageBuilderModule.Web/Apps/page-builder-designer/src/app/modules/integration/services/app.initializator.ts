import { ServerRequestDescriptor } from '@models/http';
import { inject, Injectable } from '@angular/core';

import { firstValueFrom, Observable, of } from 'rxjs';
import { catchError, map, switchMap, tap } from 'rxjs/operators';

import { BuilderHttpClient } from './builder-http.client';
import { AppConfig } from './app.config';
import { AuthService } from './auth.service';
import { JwtStorageService } from './jwt-storage.service';
import { SessionService } from './session.service';
import { DefaultConfig } from './app.default-config';

@Injectable({
    providedIn: 'root'
})
export class AppInitializator {

    private readonly config = inject(AppConfig);
    private readonly http = inject(BuilderHttpClient);
    private readonly auth = inject(AuthService);
    private readonly jwt = inject(JwtStorageService);
    private readonly session = inject(SessionService);

    init(): Promise<any> {
        // todo: dangerous! check that this is security
        console.log(this.config.getContext());
        const configUrl = this.config.getContext().location.params.configUrl || 'data/settings.json';
        return firstValueFrom(this.ensureBearerToken().pipe(
            switchMap(() => this.loadSettingsFrom(configUrl, null, DefaultConfig)),
        ).pipe(
            // tap(result => {
            //     console.log(result);
            //     this.config.initConfigWith(result);
            // }),
            switchMap((result) => {
                // override properties from config in theme
                const configInThemeUrl = result.rel ?? '/api/pagebuilder/settings?storeId={{location.params.storeId}}&theme={{config.themeName}}';
                return this.loadSettingsFrom(configInThemeUrl, result, {}).pipe(
                    map(configInTheme => ({ ...result, ...configInTheme })),
                    catchError(error => {
                        // The server's answer is what says where shipping goes: on the git flow it
                        // replaces the bundled blob urls with git ones. Falling back to the bundle
                        // would leave a git store's Unpublish renaming the live page into a
                        // ".page-draft" blob nothing serves — the exact failure the git flow exists
                        // to end. So the actions that ship are dropped instead of guessed at:
                        // editing and saving still work (the save endpoint decides the flow
                        // server-side), and buttons that are missing are noticed in a way a button
                        // pointing at the wrong endpoint is not.
                        console.error('Could not load the builder configuration from the server; publishing is unavailable for this session', error);
                        return of({ ...result, publish: null, history: null, externalPreview: null });
                    })
                );
            }),
            tap(result => {
                console.log(result);
                this.config.initConfigWith(result);
            }),
        ));
    }

    private ensureBearerToken(): Observable<void> {
        const info = this.jwt.getInfo();
        if (info?.token && info?.expiresAt && Date.now() < info.expiresAt) {
            return of(undefined);
        }
        return this.auth.obtainToken().pipe(
            tap(response => this.jwt.save(response)),
            map(() => undefined),
            catchError(error => {
                // Without a token every settings request fails and the configuration stays empty,
                // which used to break the designer in obscure ways (VCST-5847). Report the expired
                // session instead, so the shell can ask the user to sign in again.
                console.warn('Failed to obtain bearer token from cookie session:', error);
                // A stored refresh token is still worth trying, and the interceptor does exactly
                // that for the settings requests that follow - reporting the expiry itself when it
                // fails. Announcing it here would raise the sign-in prompt over a session that is
                // about to work again, which is what an overnight reload runs into.
                if (!info?.refreshToken) {
                    this.session.expire();
                }
                return of(undefined);
            })
        );
    }

    private loadSettingsFrom(url: string | ServerRequestDescriptor | ServerRequestDescriptor[], context: any = null, defaultConfig: any = null): Observable<any> {
        const request = this.http.generateRequest(url, null, { ...this.config.getContext(), settings: context }); // should context be as settings?
        return this.http.doRequest(request).pipe(
            switchMap(config => this.initConfigProperties(config).pipe(
                switchMap(c => {
                    if (c.ref) {
                        return this.loadSettingsFrom(c.ref, c);
                    }
                    return of(c);
                })
            )),
            catchError(() => of(defaultConfig)), // todo: not sure that it should be default config
        );
    }

    private initConfigProperties(config: any): Observable<any> {
        const keys = Object.keys(config).filter(x => config[x] && typeof config[x] === 'object' && config[x].init);
        if (keys.length > 0) {
            return this.initConfigProperty(config, keys.shift(), keys);
        }
        return of(config);
    }

    private initConfigProperty(config: any, key: string | undefined, tail: string[]): Observable<any> {
        if (key === undefined) {
            return of(config);
        }
        let requestDescriptor = config[key];
        const fallbackValue = requestDescriptor.fallbackValue || null;
        const initProperty = requestDescriptor.init;
        if (initProperty !== true) {
            requestDescriptor = requestDescriptor[initProperty];
        }
        const requests = this.http.generateRequest(requestDescriptor, null, { ...this.config.getContext(), settings: config });
        return this.http.doRequest(requests).pipe(
            tap(result => {
                config[key] = !result ? fallbackValue : result;
            }),
            catchError(error => {
                console.log(error);
                config[key] = null;
                return of(config);
            }),
            switchMap(() => this.initConfigProperty(config, tail.shift(), tail))
        );
    }
}
