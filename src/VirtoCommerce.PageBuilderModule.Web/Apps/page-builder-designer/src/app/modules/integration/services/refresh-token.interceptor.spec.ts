import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';

import { CookieService } from 'ngx-cookie-service';

import { AppConfig } from './app.config';
import { EnvironmentRef } from './environment.ref';
import { EvaluatorService } from './evaluator.service';
import { isPlatformRequest, refreshTokenInterceptor } from './refresh-token.interceptor';

const STORAGE_KEY = 'ls.authenticationData';
const LOCATION = { search: '', href: 'https://platform.example/apps/designer/', origin: 'https://platform.example' };

describe('refreshTokenInterceptor', () => {
    let http: HttpClient;
    let httpController: HttpTestingController;

    beforeEach(() => {
        TestBed.configureTestingModule({
            providers: [
                provideHttpClient(withInterceptors([refreshTokenInterceptor])),
                provideHttpClientTesting(),
                { provide: EnvironmentRef, useValue: { nativeWindow: { location: LOCATION } } },
                { provide: CookieService, useValue: {} },
                EvaluatorService,
                AppConfig
            ]
        });

        http = TestBed.inject(HttpClient);
        httpController = TestBed.inject(HttpTestingController);
    });

    afterEach(() => {
        localStorage.removeItem(STORAGE_KEY);
        httpController.verify();
    });

    it('does not send the token to another origin', () => {
        localStorage.setItem(STORAGE_KEY, JSON.stringify({ token: 'live-token', expiresAt: Date.now() + 60_000 }));

        http.get('https://elsewhere.example/schema.json').subscribe();
        http.get('/api/stores').subscribe();

        const external = httpController.expectOne('https://elsewhere.example/schema.json');
        expect(external.request.headers.has('Authorization')).toBe(false);
        const platform = httpController.expectOne('/api/stores');
        expect(platform.request.headers.get('Authorization')).toBe('Bearer live-token');

        external.flush({});
        platform.flush({});
    });

    it('does not refresh the token for a request to another origin', () => {
        localStorage.setItem(STORAGE_KEY, JSON.stringify({ token: 'expired-token', refreshToken: 'refresh-token', expiresAt: Date.now() - 1000 }));

        http.get('https://elsewhere.example/schema.json').subscribe();

        httpController.expectNone('/connect/token');
        const external = httpController.expectOne('https://elsewhere.example/schema.json');
        expect(external.request.headers.has('Authorization')).toBe(false);
        external.flush({});
    });

    it('refreshes an expired token for a platform request', () => {
        localStorage.setItem(STORAGE_KEY, JSON.stringify({ token: 'expired-token', refreshToken: 'refresh-token', expiresAt: Date.now() - 1000 }));

        http.get('/api/stores').subscribe();

        httpController.expectOne('/connect/token').flush({ access_token: 'fresh-token', refresh_token: 'next', expires_in: 3600 });
        const platform = httpController.expectOne('/api/stores');
        expect(platform.request.headers.get('Authorization')).toBe('Bearer fresh-token');
        platform.flush({});
    });

    [
        ['/api/stores', true],
        ['api/stores', true],
        ['https://platform.example/api/stores', true],
        ['https://elsewhere.example/api/stores', false],
        ['//elsewhere.example/api/stores', false],
        ['https://platform.example:8443/api/stores', false],
    ].forEach(([url, expected]) => {
        it(`tells whether ${url} goes to the platform`, () => {
            expect(isPlatformRequest(url as string, LOCATION)).toBe(expected);
        });
    });
});
