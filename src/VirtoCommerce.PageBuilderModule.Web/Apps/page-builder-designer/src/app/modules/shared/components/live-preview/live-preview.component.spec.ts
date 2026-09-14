import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideMockStore } from '@ngrx/store/testing';

import { CookieService } from 'ngx-cookie-service';

import { AppConfig, EnvironmentRef } from '@integration/services';
import { AppInitializator } from '@integration/services/app.initializator';
import { EvaluatorService } from '@integration/services/evaluator.service';

import { LivePreviewComponent } from './live-preview.component';

const STORAGE_KEY = 'ls.authenticationData';

// Only the two settings the preview address is built from. Built fresh for every response:
// the initializator resolves the `init` descriptors in place, so a shared object would only
// carry them on the first load.
const configResponse = () => ({
    storefrontUrl: {
        init: 'requests',
        requests: [{
            url: '/api/stores/{{location.params.storeId}}',
            method: 'GET',
            cacheable: true,
            response: { result: '$.url', isArray: false }
        }]
    },
    fullPreviewUrl: '{{settings.storefrontUrl}}'
});

describe('LivePreviewComponent', () => {
    let httpController: HttpTestingController;
    let initializator: AppInitializator;

    beforeEach(() => {
        // a valid token keeps init() from trading the cookie session for a new one
        localStorage.setItem(STORAGE_KEY, JSON.stringify({ token: 'test-token', expiresAt: Date.now() + 60000 }));

        TestBed.configureTestingModule({
            providers: [
                provideHttpClient(),
                provideHttpClientTesting(),
                provideMockStore({}),
                {
                    provide: EnvironmentRef,
                    useValue: {
                        nativeWindow: {
                            location: { search: '?storeId=store1', href: 'https://admin.local/apps/pagebuilder/' }
                        }
                    }
                },
                { provide: CookieService, useValue: {} },
                EvaluatorService,
                AppConfig
            ]
        });

        // the preview frame and its toolbar are irrelevant here, the resolved address is what matters
        TestBed.overrideComponent(LivePreviewComponent, { set: { template: '', imports: [] } });

        httpController = TestBed.inject(HttpTestingController);
        initializator = TestBed.inject(AppInitializator);
    });

    afterEach(() => {
        localStorage.removeItem(STORAGE_KEY);
    });

    it('picks up a store URL corrected in the Manager when the preview is retried', () => {
        const fixture = TestBed.createComponent(LivePreviewComponent);
        const component = fixture.componentInstance;

        initializator.init();
        httpController.expectOne('data/settings.json').flush(configResponse());
        // the store has no usable address yet, so the preview cannot be shown
        httpController.expectOne('/api/stores/store1').flush({ url: '' });
        httpController.expectOne(request => request.url.startsWith('/api/pagebuilder/settings')).flush({});

        expect(component.url()).toBeNull();

        // the URL is filled in on the platform and the user presses Retry
        component.reload();

        httpController.expectOne('data/settings.json').flush(configResponse());
        // without dropping the cached store response the old address would be resolved again
        httpController.expectOne('/api/stores/store1').flush({ url: 'https://storefront.local' });
        httpController.expectOne(request => request.url.startsWith('/api/pagebuilder/settings')).flush({});

        expect(component.url()).toBe('https://storefront.local');
    });
});
