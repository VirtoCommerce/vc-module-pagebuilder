import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';

import { CookieService } from 'ngx-cookie-service';

import { AppConfig } from './app.config';
import { EnvironmentRef } from './environment.ref';
import { EvaluatorService } from './evaluator.service';
import { BuilderHttpClient } from './builder-http.client';

const storeRequest = { url: '/api/stores/store1', method: 'GET', body: null, cacheable: true, options: {} };

describe('BuilderHttpClient cache', () => {
    let http: BuilderHttpClient;
    let httpController: HttpTestingController;

    beforeEach(() => {
        TestBed.configureTestingModule({
            providers: [
                provideHttpClient(),
                provideHttpClientTesting(),
                { provide: EnvironmentRef, useValue: { nativeWindow: { location: { search: '' } } } },
                { provide: CookieService, useValue: {} },
                EvaluatorService,
                AppConfig
            ]
        });

        http = TestBed.inject(BuilderHttpClient);
        httpController = TestBed.inject(HttpTestingController);
    });

    it('serves a cacheable request from the cache', () => {
        const results: any[] = [];

        http.doRequest(<any>storeRequest).subscribe(result => results.push(result));
        httpController.expectOne('/api/stores/store1').flush({ url: 'https://broken' });

        http.doRequest(<any>storeRequest).subscribe(result => results.push(result));

        httpController.expectNone('/api/stores/store1');
        expect(results).toEqual([{ url: 'https://broken' }, { url: 'https://broken' }]);
    });

    it('asks the server again once the cache has been cleared', () => {
        const results: any[] = [];

        http.doRequest(<any>storeRequest).subscribe(result => results.push(result));
        httpController.expectOne('/api/stores/store1').flush({ url: 'https://broken' });

        // the store URL has been corrected in the Manager in the meantime
        http.clearCache();

        http.doRequest(<any>storeRequest).subscribe(result => results.push(result));
        httpController.expectOne('/api/stores/store1').flush({ url: 'https://fixed' });

        expect(results).toEqual([{ url: 'https://broken' }, { url: 'https://fixed' }]);
    });
});
