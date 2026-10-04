import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { HttpErrorResponse } from '@angular/common/http';
import { of, Subject } from 'rxjs';

import { AssetLibraryService, AssetLibraryUploadCoordinatorService } from '@core/services';
import { AssetPickerStateService } from './asset-picker-state.service';
import { createAssetLibraryMock } from '@app/testing/asset-library';
import { getFolderNameError } from './asset-picker-folder-name';

describe('AssetPickerStateService', async () => {
    const root = '/stores/store/Page Builder';
    const image = { type: 'blob' as const, name: 'image.jpg', relativeUrl: `${root}/image.jpg`, contentType: 'image/jpeg' };
    let created: Subject<void>;
    let assets: ReturnType<typeof createAssetLibraryMock>;
    let uploads: { uploadFiles: ReturnType<typeof vi.fn> };
    let state: AssetPickerStateService;

    function setup(multiple = true) {
        created = new Subject();
        assets = createAssetLibraryMock({
            search: vi.fn(() => of({ results: [image], totalCount: 1 })),
            createFolder: vi.fn(() => created),
        });
        uploads = { uploadFiles: vi.fn(() => of([])) };
        TestBed.configureTestingModule({ providers: [
            AssetPickerStateService,
            { provide: MAT_DIALOG_DATA, useValue: { rootFolderUrl: root, multiple } },
            { provide: AssetLibraryService, useValue: assets },
            { provide: AssetLibraryUploadCoordinatorService, useValue: uploads },
        ] });
        state = TestBed.inject(AssetPickerStateService);
    }

    it('keeps selection, clears search, enters the created folder and uploads there', async () => {
        setup();
        state.selectAsset(image);
        state.onSearch('image');
        state.openFolderForm();
        state.folderName.set('  new folder  ');
        const submission = state.createFolder();
        state.createFolder();
        expect(assets.createFolder).toHaveBeenCalledExactlyOnceWith(root, 'new folder');
        created.next();
        await submission;
        expect(state.currentFolderUrl()).toBe(`${root}/new folder`);
        expect(state.searchValue()).toBe('');
        expect(state.selectedAssets()).toEqual([image]);
        expect(state.folderFormOpen()).toBe(false);
        state.uploadFiles([new File(['image'], 'new.jpg', { type: 'image/jpeg' })]);
        expect(uploads.uploadFiles.mock.calls[0][0]).toBe(`${root}/new folder`);
    });

    it('keeps the form, folder and selection on failure and allows retry after editing', async () => {
        setup();
        state.selectAsset(image);
        state.openFolderForm();
        state.folderName.set('new folder');
        const submission = state.createFolder();
        created.error(new Error('Folder already exists.'));
        await submission;
        expect(state.folderNameError()).toBe('Folder already exists.');
        expect(state.creatingFolder()).toBe(false);
        expect(state.folderFormOpen()).toBe(true);
        expect(state.currentFolderUrl()).toBe(root);
        expect(state.selectedAssets()).toEqual([image]);
        state.folderName.set('another folder');
        expect(state.folderNameError()).toBeNull();
    });

    it('rejects invalid and empty names without calling the server', async () => {
        setup();
        for (const name of ['', '  ', 'UPPER', 'ab', 'a--b', 'a/b']) {
            state.folderName.set(name);
            state.createFolder();
        }
        expect(assets.createFolder).not.toHaveBeenCalled();
    });

    it('shows the Assets API validation message inline', async () => {
        setup();
        state.openFolderForm();
        state.folderName.set('new folder');
        const submission = state.createFolder();
        created.error(new HttpErrorResponse({
            status: 400,
            error: { message: 'Folder already exists.' },
        }));
        await submission;
        expect(state.folderNameError()).toBe('Folder already exists.');
    });

    it('allows retrying the same valid name after a transient server failure', async () => {
        setup();
        state.openFolderForm();
        state.folderName.set('new folder');
        const submission = state.createFolder();
        created.error(new Error('Service unavailable.'));
        await submission;
        assets.createFolder.mockReturnValue(of(undefined));

        await state.createFolder();

        expect(assets.createFolder).toHaveBeenCalledTimes(2);
        expect(state.folderNameError()).toBeNull();
        expect(state.currentFolderUrl()).toBe(`${root}/new folder`);
    });

    it('retains the single-selection contract when entering a new folder', async () => {
        setup(false);
        state.selectAsset(image);
        state.folderName.set('new folder');
        const submission = state.createFolder();
        created.next();
        await submission;
        expect(state.selectedCount()).toBe(0);
    });

    it('uses the captured parent when navigation happens during creation', async () => {
        setup();
        state.folderName.set('new folder');
        const submission = state.createFolder();
        state.navigateToBreadcrumb(`${root}/other`);
        created.next();
        await submission;
        expect(state.currentFolderUrl()).toBe(`${root}/new folder`);
    });

    it('prevents uploading while a folder is being created', async () => {
        setup();
        state.folderName.set('new folder');
        state.createFolder();
        state.uploadFiles([new File(['image'], 'new.jpg', { type: 'image/jpeg' })]);
        expect(uploads.uploadFiles).not.toHaveBeenCalled();
    });

    it('prevents folder creation while files are being uploaded', async () => {
        setup();
        state.uploading.set(true);
        state.folderName.set('new folder');
        state.createFolder();
        expect(assets.createFolder).not.toHaveBeenCalled();
    });

    it('does not erase a draft when New folder is clicked again', async () => {
        setup();
        state.openFolderForm();
        state.folderName.set('my folder');
        state.openFolderForm();
        expect(state.folderName()).toBe('my folder');
    });

    it('does not reopen or reset the folder form during creation', async () => {
        setup();
        state.openFolderForm();
        state.folderName.set('my folder');
        state.createFolder();
        state.openFolderForm();
        expect(state.folderName()).toBe('my folder');
        expect(state.creatingFolder()).toBe(true);
    });

    it('ignores a stale root search response after entering the created folder', async () => {
        setup();
        const staleSearch = new Subject<{ results: typeof image[]; totalCount: number }>();
        assets.search.mockReturnValueOnce(staleSearch).mockReturnValue(of({ results: [], totalCount: 0 }));
        state.navigateToBreadcrumb(root);
        state.selectAsset(image);
        state.folderName.set('new folder');
        const submission = state.createFolder();
        created.next();
        await submission;

        staleSearch.next({ results: [image], totalCount: 1 });

        expect(state.currentFolderUrl()).toBe(`${root}/new folder`);
        expect(state.entries()).toEqual([]);
        expect(state.loading()).toBe(false);
        expect(state.selectedAssets()).toEqual([image]);
    });

    it('cancels a pending search debounce after folder creation', async () => {
        vi.useFakeTimers();
        try {
            setup();
            state.onSearch('old search');
            state.folderName.set('new folder');
            const submission = state.createFolder();
            created.next();
            await submission;
            vi.advanceTimersByTime(300);

            expect(assets.search).toHaveBeenCalledTimes(2);
            expect(assets.search).toHaveBeenLastCalledWith(`${root}/new folder`, '');
        } finally {
            vi.useRealTimers();
        }
    });

    it('unsubscribes from folder creation when the picker is destroyed', async () => {
        setup();
        state.folderName.set('new folder');
        const submission = state.createFolder();
        expect(created.observed).toBe(true);

        TestBed.resetTestingModule();
        expect(created.observed).toBe(false);
        created.next();
        await submission;
        expect(state.currentFolderUrl()).toBe(root);
    });

    it('preserves selections from multiple folders while creating a nested folder', async () => {
        setup();
        const nestedImage = { ...image, name: 'nested.jpg', relativeUrl: `${root}/nested/nested.jpg` };
        state.selectAsset(image);
        state.navigateTo({ type: 'folder', name: 'nested', relativeUrl: `${root}/nested/` });
        state.selectAsset(nestedImage);
        state.folderName.set('new folder');
        const submission = state.createFolder();
        created.next();
        await submission;

        expect(assets.createFolder).toHaveBeenCalledExactlyOnceWith(`${root}/nested`, 'new folder');
        expect(state.currentFolderUrl()).toBe(`${root}/nested/new folder`);
        expect(state.selectedAssets()).toEqual([image, nestedImage]);
    });
    it.each([0, 400, 403, 500])('uses the folder error label for a bodyless HTTP %s response', async status => {
        setup();
        state.openFolderForm();
        state.folderName.set('new folder');
        const submission = state.createFolder();
        created.error(new HttpErrorResponse({ status, url: '/api/assets/folder' }));
        await submission;
        expect(state.folderNameError()).toBe(state.labels.folderCreateError);
        expect(state.canCreateFolder()).toBe(true);
        state.folderName.set('another folder');
        expect(state.folderNameError()).toBeNull();
    });

    it('uses the same HTTP message extraction for loading and uploading', () => {
        setup();
        const searched = new Subject<{ results: typeof image[]; totalCount: number }>();
        assets.search.mockReturnValue(searched);
        state.navigateToBreadcrumb(root);
        searched.error(new HttpErrorResponse({ status: 500 }));
        expect(state.error()).toBe(state.labels.loadError);
        const uploaded = new Subject<never>();
        uploads.uploadFiles.mockReturnValue(uploaded);
        state.uploadFiles([new File(['image'], 'new.jpg', { type: 'image/jpeg' })]);
        uploaded.error(new HttpErrorResponse({ status: 400, error: { message: 'Upload rejected.' } }));
        expect(state.error()).toBe('Upload rejected.');
    });

});

describe('getFolderNameError', async () => {
    it.each(['abc', 'a b', 'a-b', '123', 'a'.repeat(63), '  abc  ', 'a - b'])('accepts %s', name => {
        expect(getFolderNameError(name)).toBeNull();
    });

    it.each([
        ['ab', 'minLength'], ['a'.repeat(64), 'maxLength'], ['-abc', 'dashStart'],
        ['abc-', 'dashEnd'], ['a--b', 'dashConsecutive'], ['Abc', 'invalidCharacters'],
        ['a_b', 'invalidCharacters'], ['a/b', 'invalidCharacters'], ['a\\b', 'invalidCharacters'],
        ['абв', 'invalidCharacters'], ['a.b', 'invalidCharacters'],
    ])('rejects %s with %s', (name, error) => {
        expect(getFolderNameError(name)).toBe(error);
    });
});
