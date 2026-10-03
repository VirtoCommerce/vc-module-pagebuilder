import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CookieService } from 'ngx-cookie-service';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';

import { AppConfig, EnvironmentRef, EvaluatorService } from '@integration/services';
import settings from '../../../../data/settings.json';
import { AssetLibraryApiService } from './asset-library-api.service';
import { AssetLibraryUploadCoordinatorService } from './asset-library-upload-coordinator.service';
import { AssetPickerStateService } from '../dialogs/asset-picker/asset-picker-state.service';

describe('Asset library folder API', () => {
    let api: AssetLibraryApiService;
    let http: HttpTestingController;

    beforeEach(() => {
        TestBed.configureTestingModule({ providers: [
            provideHttpClient(), provideHttpClientTesting(),
            { provide: EnvironmentRef, useValue: { nativeWindow: { location: { search: '' } } } },
            { provide: CookieService, useValue: {} },
            EvaluatorService, AppConfig,
            AssetPickerStateService,
            { provide: MAT_DIALOG_DATA, useValue: { rootFolderUrl: '/stores/store/Page Builder', multiple: true } },
            { provide: AssetLibraryUploadCoordinatorService, useValue: {} },
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
        {}, [], { url: '', method: 'POST' }, '/api/assets/folder',
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

    it('delivers request preparation failures through the observable error handler', () => {
        vi.spyOn(TestBed.inject(AppConfig), 'getValue').mockImplementation(() => {
            throw new Error('Unable to evaluate folder request.');
        });
        const next = vi.fn();
        const error = vi.fn();

        expect(() => api.createFolder('/stores/store/Page Builder', 'new folder').subscribe({ next, error })).not.toThrow();

        expect(error).toHaveBeenCalledWith(expect.objectContaining({ message: 'Unable to evaluate folder request.' }));
        expect(next).not.toHaveBeenCalled();
        http.expectNone('/api/assets/folder');
    });

    it('keeps the real picker state retryable after invalid request configuration', () => {
        const root = '/stores/store/Page Builder';
        const config = TestBed.inject(AppConfig);
        config.initConfigWith({ assetLibraryCreateFolderRequest: { url: 42, method: 'POST' } });
        const state = TestBed.inject(AssetPickerStateService);
        http.expectOne(request => request.url.startsWith('/api/assets?')).flush({ results: [] });
        state.openFolderForm();
        state.onFolderNameChange('new folder');

        state.createFolder();

        expect(state.creatingFolder()).toBe(false);
        expect(state.folderFormOpen()).toBe(true);
        expect(state.currentFolderUrl()).toBe(root);
        expect(state.folderNameError()).toBe('Folder creation requires a configured POST request.');
        expect(state.canCreateFolder()).toBe(true);
        http.expectNone('/api/assets/folder');

        config.initConfigWith(settings);
        state.createFolder();
        http.expectOne('/api/assets/folder').flush(null, { status: 204, statusText: 'No Content' });
        http.expectOne(request => request.url.startsWith('/api/assets?')).flush({ results: [] });
        expect(state.currentFolderUrl()).toBe(`${root}/new folder`);
        expect(state.folderNameError()).toBeNull();
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
});
