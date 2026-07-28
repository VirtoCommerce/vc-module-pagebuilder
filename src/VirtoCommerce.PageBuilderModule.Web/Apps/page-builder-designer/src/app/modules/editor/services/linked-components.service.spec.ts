import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { AppConfig, BuilderHttpClient, EvaluatorService } from '@integration/services';
import { createTemplate } from '@app/testing';

import { LinkedComponentsService } from './linked-components.service';

describe('LinkedComponentsService', () => {
    let service: LinkedComponentsService;
    let http: HttpTestingController;

    beforeEach(() => {
        TestBed.configureTestingModule({
            providers: [
                provideHttpClient(),
                provideHttpClientTesting(),
                BuilderHttpClient,
                { provide: EvaluatorService, useValue: {} },
                {
                    provide: AppConfig,
                    useValue: {
                        getContext: () => ({ location: { params: { storeId: 'store-1' } } }),
                    },
                },
            ],
        });

        service = TestBed.inject(LinkedComponentsService);
        http = TestBed.inject(HttpTestingController);
    });

    afterEach(() => http.verify());

    it('searches in the active store', () => {
        service.search(' hero ', 10, 20).subscribe();

        const request = http.expectOne('/api/page-builder-linked-components/search');
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual({ storeId: 'store-1', keyword: 'hero', skip: 10, take: 20 });
        request.flush({ totalCount: 0, results: [] });
    });

    it('uses a bounded first page by default', () => {
        service.search().subscribe();

        const request = http.expectOne('/api/page-builder-linked-components/search');
        expect(request.request.body).toEqual({ storeId: 'store-1', keyword: '', skip: 0, take: 20 });
        request.flush({ totalCount: 0, results: [] });
    });

    it('creates metadata and content together', () => {
        const content = createTemplate();
        service.create(' Hero ', content).subscribe();

        const request = http.expectOne('/api/page-builder-linked-components');
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual({ storeId: 'store-1', name: 'Hero', content });
        request.flush({ id: 'component-1', storeId: 'store-1', name: 'Hero', usageCount: 0, usagePages: [] });
    });

    it('loads and updates raw component content', () => {
        const content = createTemplate();

        service.getContent('component/1').subscribe();
        let request = http.expectOne('/api/page-builder-linked-components/component%2F1/content');
        expect(request.request.method).toBe('GET');
        request.flush(content);

        service.updateContent('component/1', content).subscribe();
        request = http.expectOne('/api/page-builder-linked-components/component%2F1/content');
        expect(request.request.method).toBe('PUT');
        expect(request.request.body).toBe(content);
        request.flush(null);
    });
});
