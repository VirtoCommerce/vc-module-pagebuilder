import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CookieService } from 'ngx-cookie-service';

import { AppConfig, EnvironmentRef, EvaluatorService } from '@integration/services';
import settings from '../../../../data/settings.json';
import { AssetLibraryApiService } from './asset-library-api.service';

describe('AssetLibraryApiService', () => {
    let api: AssetLibraryApiService;
    let http: HttpTestingController;

    beforeEach(() => {
        TestBed.configureTestingModule({ providers: [
            provideHttpClient(), provideHttpClientTesting(),
            { provide: EnvironmentRef, useValue: { nativeWindow: { location: { search: '' } } } },
            { provide: CookieService, useValue: {} },
            EvaluatorService, AppConfig,
        ] });
        TestBed.inject(AppConfig).initConfigWith(settings);
        api = TestBed.inject(AssetLibraryApiService);
        http = TestBed.inject(HttpTestingController);
    });

    afterEach(() => http.verify());

    it('uses the existing Admin endpoint and handles its empty 204 response', () => {
        const next = vi.fn();
        api.createFolder('/stores/store/Page Builder', 'new folder').subscribe(next);
        const request = http.expectOne('/api/assets/folder');
        expect(request.request.method).toBe('POST');
        expect(request.request.body).toEqual({ name: 'new folder', parentUrl: '/stores/store/Page Builder' });
        request.flush(null, { status: 204, statusText: 'No Content' });
        expect(next).toHaveBeenCalledOnce();
    });

    it('propagates server errors so the picker stays open for correction', () => {
        const next = vi.fn();
        const error = vi.fn();
        api.createFolder('/stores/store/Page Builder', 'new folder').subscribe({ next, error });
        http.expectOne('/api/assets/folder').flush({ message: 'Denied' }, { status: 403, statusText: 'Forbidden' });
        expect(error).toHaveBeenCalledOnce();
        expect(next).not.toHaveBeenCalled();
    });

    it('fails instead of reporting a phantom folder when the request is not configured', () => {
        TestBed.inject(AppConfig).initConfigWith({ assetLibraryCreateFolderRequest: null });
        const next = vi.fn();
        const error = vi.fn();

        api.createFolder('/stores/store/Page Builder', 'new folder').subscribe({ next, error });

        expect(error).toHaveBeenCalledOnce();
        expect(next).not.toHaveBeenCalled();
        http.expectNone('/api/assets/folder');
    });

    it.each([
        {}, [], [settings.assetLibraryCreateFolderRequest, settings.assetLibraryCreateFolderRequest],
        { url: '', method: 'POST' }, '/api/assets/folder',
        { url: '/api/assets/folder', method: 'GET' },
        { url: 42, method: 'POST' }, { url: '/api/assets/folder', method: 42 },
    ])('rejects an invalid folder mutation descriptor: %j', descriptor => {
        TestBed.inject(AppConfig).initConfigWith({ assetLibraryCreateFolderRequest: descriptor });
        const next = vi.fn();
        const error = vi.fn();

        api.createFolder('/stores/store/Page Builder', 'new folder').subscribe({ next, error });

        expect(error).toHaveBeenCalledOnce();
        expect(next).not.toHaveBeenCalled();
        http.expectNone('/api/assets/folder');
    });

    it('honours a custom POST URL while preserving the request body', () => {
        TestBed.inject(AppConfig).initConfigWith({ assetLibraryCreateFolderRequest: {
            ...settings.assetLibraryCreateFolderRequest, url: '/custom/assets/folder',
        } });
        const next = vi.fn();
        api.createFolder('/stores/store/Page Builder/nested', 'new-folder').subscribe(next);
        const request = http.expectOne('/custom/assets/folder');
        expect(request.request.body).toEqual({ name: 'new-folder', parentUrl: '/stores/store/Page Builder/nested' });
        request.flush(null, { status: 204, statusText: 'No Content' });
        expect(next).toHaveBeenCalledExactlyOnceWith(undefined);
    });

    it.each([
        '/stores/store/Page Builder/{{draft}}',
        "/stores/store/Page Builder/{{='draft'}}",
    ])('preserves a literal folder path in the request body: %s', parentUrl => {
        api.createFolder(parentUrl, 'child').subscribe();
        const request = http.expectOne('/api/assets/folder');
        expect(request.request.body).toEqual({ parentUrl, name: 'child' });
        request.flush(null, { status: 204, statusText: 'No Content' });
    });

    it('preserves literal reference URLs without a second evaluation', () => {
        const assetUrls = ["/assets/{{='draft'}}/image.jpg", '/assets/{{draft}}/image.jpg'];
        api.searchReferences('store', assetUrls).subscribe();
        const request = http.expectOne(settings.assetLibraryReferencesRequest.url);
        expect(request.request.body).toEqual({ storeId: 'store', assetUrls, includePages: true });
        request.flush({ results: [], totalCount: 0 });
    });

    it('still encodes folder search paths and builds upload form data', () => {
        const folderUrl = '/stores/store/Page Builder/{{draft}}';
        api.search(folderUrl, 'a & b').subscribe();
        http.expectOne(`/api/assets?folderUrl=${encodeURIComponent(folderUrl)}&keyword=a%20%26%20b`).flush({ results: [] });
        const file = new File(['image'], 'image.jpg', { type: 'image/jpeg' });
        api.upload(folderUrl, file).subscribe();
        const request = http.expectOne(`/api/assets?folderUrl=${encodeURIComponent(folderUrl)}`);
        expect(request.request.method).toBe('POST');
        expect(request.request.body.get('file')).toBe(file);
        request.flush([]);
    });

    it.each([false, undefined, null, 'true'])('hides mutations without an explicit permission: %s', canCreateAssets => {
        TestBed.inject(AppConfig).initConfigWith({ canCreateAssets });
        expect(api.canCreateFolder()).toBe(false);
        expect(api.canUpload()).toBe(false);
    });

    it('requires both permission and configured requests and responds to config reload', () => {
        const config = TestBed.inject(AppConfig);
        config.initConfigWith({ canCreateAssets: true });
        expect(api.canCreateFolder()).toBe(true);
        expect(api.canUpload()).toBe(true);
        config.initConfigWith({ assetLibraryCreateFolderRequest: null, assetLibraryUploadRequest: null });
        expect(api.canCreateFolder()).toBe(false);
        expect(api.canUpload()).toBe(false);
        config.initConfigWith({ ...settings, canCreateAssets: true });
        expect(api.canCreateFolder()).toBe(true);
        expect(api.canUpload()).toBe(true);
    });
});
