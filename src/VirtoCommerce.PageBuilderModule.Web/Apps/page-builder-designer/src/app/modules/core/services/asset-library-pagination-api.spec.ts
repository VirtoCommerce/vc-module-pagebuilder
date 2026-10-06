import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CookieService } from 'ngx-cookie-service';
import { AppConfig, EnvironmentRef, EvaluatorService } from '@integration/services';
import settings from '../../../../data/settings.json';
import { AssetLibraryApiService } from './asset-library-api.service';

describe('Asset library pagination HTTP configuration', () => {
    let api: AssetLibraryApiService;
    let http: HttpTestingController;

    beforeEach(() => {
        TestBed.resetTestingModule();
        TestBed.configureTestingModule({ providers: [
            provideHttpClient(), provideHttpClientTesting(),
            { provide: EnvironmentRef, useValue: { nativeWindow: { location: { search: '' } } } },
            { provide: CookieService, useValue: {} },
            AppConfig, EvaluatorService,
        ] });
        TestBed.inject(AppConfig).initConfigWith(settings);
        api = TestBed.inject(AssetLibraryApiService);
        http = TestBed.inject(HttpTestingController);
    });

    afterEach(() => http.verify());

    it('sends numeric paging and the search term through the shipped settings', () => {
        api.search('/folder', ' hero ', { skip: 80, take: 50, sort: 'name:desc' }).subscribe();
        const request = http.expectOne('/api/page-builder-assets/search');
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual({
            folderUrl: '/folder', keyword: 'hero', skip: 80, take: 50, sort: 'name:desc', exactName: '', acceptedTypes: [],
        });
        request.flush({ totalCount: 500, results: [] });
    });

    it('limits an exact upload conflict lookup to one result', () => {
        api.search('/folder', undefined, { exactName: 'asset-499.png', take: 1 }).subscribe();
        const request = http.expectOne('/api/page-builder-assets/search');
        expect(request.request.body.exactName).toBe('asset-499.png');
        expect(request.request.body.take).toBe(1);
        request.flush({ totalCount: 1, results: [{ type: 'blob', name: 'asset-499.png' }] });
    });

    it('forwards picker MIME and extension filters in the search body', () => {
        api.search('/folder', '', { acceptedTypes: ['image/*', '.pdf'] }).subscribe();
        const request = http.expectOne('/api/page-builder-assets/search');
        expect(request.request.body.acceptedTypes).toEqual(['image/*', '.pdf']);
        request.flush({ totalCount: 25, results: [] });
    });

    it('migrates the legacy standard URL from an external configuration', () => {
        TestBed.inject(AppConfig).initConfigWith({
            assetLibrarySearchRequest: '/api/assets?folderUrl={{=encodeURIComponent(this.folderUrl)}}{{=this.keyword ? "&keyword=" + encodeURIComponent(this.keyword) : ""}}',
        });
        api.search('/folder with spaces', 'photo', { skip: 40, take: 20, acceptedTypes: ['image/*'] }).subscribe();
        const request = http.expectOne('/api/page-builder-assets/search');
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toMatchObject({
            folderUrl: '/folder with spaces', keyword: 'photo', skip: 40, take: 20, acceptedTypes: ['image/*'],
        });
        request.flush({ totalCount: 500, results: [] });
    });

    it('migrates a legacy theme descriptor and keeps the API prefix and request options', () => {
        TestBed.inject(AppConfig).initConfigWith({ assetLibrarySearchRequest: {
            url: 'https://platform.example/backend/api/assets?folderUrl={{=encodeURIComponent(this.folderUrl)}}',
            method: 'GET', options: { headers: { 'X-Fixture': 'test' }, withCredentials: true },
        } });
        api.search('/folder', '', { skip: 20, take: 50 }).subscribe();
        const request = http.expectOne('https://platform.example/backend/api/page-builder-assets/search');
        expect(request.request.method).toBe('POST');
        expect(request.request.headers.get('X-Fixture')).toBe('test');
        expect(request.request.withCredentials).toBe(true);
        expect(request.request.body).toMatchObject({ folderUrl: '/folder', skip: 20, take: 50 });
        request.flush({ totalCount: 500, results: [] });
    });

    it('keeps a custom search endpoint unchanged', () => {
        TestBed.inject(AppConfig).initConfigWith({ assetLibrarySearchRequest: '/custom-assets?offset={{skip}}' });
        api.search('/folder', '', { skip: 40, take: 20 }).subscribe();
        const request = http.expectOne('/custom-assets?offset=40');
        expect(request.request.method).toBe('GET');
        request.flush({ totalCount: 500, results: [] });
    });
});
