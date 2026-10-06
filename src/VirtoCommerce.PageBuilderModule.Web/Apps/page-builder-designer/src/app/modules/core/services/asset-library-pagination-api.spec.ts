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
            folderUrl: '/folder', keyword: 'hero', skip: 80, take: 50, sort: 'name:desc', exactName: '', preferredAssetUrl: '', acceptedTypes: [],
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

    it('preserves a legacy GET descriptor and locally pages its complete response', () => {
        TestBed.inject(AppConfig).initConfigWith({
            assetLibrarySearchRequest: '/api/assets?folderUrl={{=encodeURIComponent(this.folderUrl)}}',
        });
        let result: any;
        api.search('/folder with spaces', '', { skip: 20, take: 20, acceptedTypes: ['image/*'] }).subscribe(value => result = value);
        const request = http.expectOne('/api/assets?folderUrl=%2Ffolder%20with%20spaces');
        expect(request.request.method).toBe('GET');
        request.flush({ totalCount: 51, results: [
            { type: 'folder', name: 'z-folder' },
            ...Array.from({ length: 25 }, (_, i) => ({ type: 'blob', name: `image-${String(i).padStart(2, '0')}.png` })),
            ...Array.from({ length: 25 }, (_, i) => ({ type: 'blob', name: `document-${i}.pdf` })),
        ] });
        expect(result.totalCount).toBe(26);
        expect(result.fileCount).toBe(25);
        expect(result.results).toHaveLength(6);
        expect(result.results[0].name).toBe('image-19.png');
        expect(result.skip).toBe(20);
    });

    it('preserves the host, route, method and options of a theme descriptor', () => {
        TestBed.inject(AppConfig).initConfigWith({ assetLibrarySearchRequest: {
            url: 'https://platform.example/backend/api/assets?folderUrl={{=encodeURIComponent(this.folderUrl)}}',
            method: 'GET', options: { headers: { 'X-Fixture': 'test' }, withCredentials: true },
        } });
        api.search('/folder', '', { skip: 20, take: 50 }).subscribe();
        const request = http.expectOne('https://platform.example/backend/api/assets?folderUrl=%2Ffolder');
        expect(request.request.method).toBe('GET');
        expect(request.request.headers.get('X-Fixture')).toBe('test');
        expect(request.request.withCredentials).toBe(true);
        request.flush({ totalCount: 500, results: [] });
    });

    it('keeps literal template delimiters in folder, keyword and exact-name data', () => {
        const literal = '{{=1 + 2}}';
        api.search(`/folder/${literal}`, literal, { exactName: `${literal}.png` }).subscribe();
        const request = http.expectOne('/api/page-builder-assets/search');
        expect(request.request.body.folderUrl).toBe(`/folder/${literal}`);
        expect(request.request.body.keyword).toBe(literal);
        expect(request.request.body.exactName).toBe(`${literal}.png`);
        request.flush({ totalCount: 0, skip: 0, results: [] });
    });

    it('filters and sorts a small complete custom listing before clamping to its last page', () => {
        TestBed.inject(AppConfig).initConfigWith({ assetLibrarySearchRequest: '/custom-assets' });
        let result: any;
        api.search('/folder', '', { skip: 40, take: 20, sort: 'size:desc' }).subscribe(value => result = value);
        http.expectOne('/custom-assets').flush({ results: [
            { type: 'blob', name: 'a.png', size: 10 }, { type: 'folder', name: 'z-folder' },
        ] });
        expect(result.skip).toBe(0);
        expect(result.fileCount).toBe(1);
        expect(result.results.map((entry: any) => entry.name)).toEqual(['z-folder', 'a.png']);
    });

    it('preserves the server-filtered page and its file count and offset', () => {
        let result: any;
        api.search('/folder', '', { acceptedTypes: ['image/*'] }).subscribe(value => result = value);
        const response = { totalCount: 21, fileCount: 20, skip: 20, results: [{ type: 'blob', name: 'image.tif', contentType: 'image/tiff' }] };
        http.expectOne('/api/page-builder-assets/search').flush(response);
        expect(result).toEqual(response);
    });

    it('keeps a custom search endpoint unchanged', () => {
        TestBed.inject(AppConfig).initConfigWith({ assetLibrarySearchRequest: '/custom-assets?offset={{skip}}' });
        api.search('/folder', '', { skip: 40, take: 20 }).subscribe();
        const request = http.expectOne('/custom-assets?offset=40');
        expect(request.request.method).toBe('GET');
        request.flush({ totalCount: 500, results: [] });
    });
});
