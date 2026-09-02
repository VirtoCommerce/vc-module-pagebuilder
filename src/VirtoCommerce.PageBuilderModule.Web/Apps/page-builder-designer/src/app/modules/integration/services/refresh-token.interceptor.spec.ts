import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';

import { CookieService } from 'ngx-cookie-service';

import { AppConfig } from './app.config';
import { EnvironmentRef } from './environment.ref';
import { EvaluatorService } from './evaluator.service';
import { JwtStorageService } from './jwt-storage.service';
import { SessionService } from './session.service';
import { refreshTokenInterceptor } from './refresh-token.interceptor';

const STORAGE_KEY = 'ls.authenticationData';

describe('refreshTokenInterceptor', () => {
    let http: HttpClient;
    let httpController: HttpTestingController;
    let session: SessionService;
    let jwt: JwtStorageService;

    beforeEach(() => {
        // an expired access token with a refresh token is what makes the interceptor refresh
        localStorage.setItem(STORAGE_KEY, JSON.stringify({
            token: 'expired-token',
            refreshToken: 'refresh-token',
            expiresAt: Date.now() - 1000
        }));

        TestBed.configureTestingModule({
            providers: [
                provideHttpClient(withInterceptors([refreshTokenInterceptor])),
                provideHttpClientTesting(),
                { provide: EnvironmentRef, useValue: { nativeWindow: { location: { search: '' } } } },
                { provide: CookieService, useValue: {} },
                EvaluatorService,
                AppConfig
            ]
        });

        http = TestBed.inject(HttpClient);
        httpController = TestBed.inject(HttpTestingController);
        session = TestBed.inject(SessionService);
        jwt = TestBed.inject(JwtStorageService);
    });

    afterEach(() => {
        localStorage.removeItem(STORAGE_KEY);
    });

    it('refreshes once for requests that run into the expired token together', () => {
        const results: string[] = [];

        http.get('/api/first').subscribe(() => results.push('first'));
        http.get('/api/second').subscribe(() => results.push('second'));

        // both requests wait for the same refresh instead of asking for a token each
        const refresh = httpController.expectOne('/connect/token');
        expect(refresh.request.method).toBe('POST');
        refresh.flush({ access_token: 'fresh-token', refresh_token: 'next-refresh-token', expires_in: 3600 });

        // the refreshed token must already be stored by the time the waiting requests are released
        expect(jwt.getInfo().token).toBe('fresh-token');

        const first = httpController.expectOne('/api/first');
        const second = httpController.expectOne('/api/second');
        expect(first.request.headers.get('Authorization')).toBe('Bearer fresh-token');
        expect(second.request.headers.get('Authorization')).toBe('Bearer fresh-token');

        first.flush({});
        second.flush({});

        expect(results).toEqual(['first', 'second']);
        expect(session.expired()).toBe(false);
    });

    it('reports a failed refresh to every waiting request', () => {
        const errors: number[] = [];

        http.get('/api/first').subscribe({ error: error => errors.push(error.status) });
        http.get('/api/second').subscribe({ error: error => errors.push(error.status) });

        const refresh = httpController.expectOne('/connect/token');
        refresh.flush('', { status: 400, statusText: 'Bad Request' });

        // neither request may be left hanging: the store has to learn that the load failed,
        // otherwise its loading flag - and the fullscreen loader with it - stays on forever
        expect(errors.length).toBe(2);
        httpController.expectNone('/api/first');
        httpController.expectNone('/api/second');
        expect(session.expired()).toBe(true);
    });

    it('starts a new refresh for a request that arrives after a failed one', () => {
        http.get('/api/first').subscribe({ error: () => undefined });
        httpController.expectOne('/connect/token').flush('', { status: 400, statusText: 'Bad Request' });

        http.get('/api/second').subscribe({ error: () => undefined });

        httpController.expectOne('/connect/token').flush('', { status: 400, statusText: 'Bad Request' });
    });

    it('reports an expired session when the api rejects the token it was given', () => {
        localStorage.setItem(STORAGE_KEY, JSON.stringify({ token: 'live-token', expiresAt: Date.now() + 60000 }));

        http.get('/api/first').subscribe({ error: () => undefined });

        const request = httpController.expectOne('/api/first');
        expect(request.request.headers.get('Authorization')).toBe('Bearer live-token');
        request.flush('', { status: 401, statusText: 'Unauthorized' });

        expect(session.expired()).toBe(true);
    });
});
