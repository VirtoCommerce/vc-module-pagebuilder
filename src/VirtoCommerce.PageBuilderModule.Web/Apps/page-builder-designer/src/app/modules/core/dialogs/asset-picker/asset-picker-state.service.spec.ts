import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { HttpErrorResponse } from '@angular/common/http';
import { of, Subject } from 'rxjs';

import { AssetLibraryService, AssetLibraryUploadCoordinatorService } from '@core/services';
import { AssetPickerStateService } from './asset-picker-state.service';
import { getFolderNameError } from './asset-picker-folder-name';

describe('Asset picker folder creation', () => {
    const root = '/stores/store/Page Builder';
    const image = { type: 'blob' as const, name: 'image.jpg', relativeUrl: `${root}/image.jpg`, contentType: 'image/jpeg' };
    let created: Subject<unknown>;
    let assets: { getLabels: ReturnType<typeof vi.fn>; search: ReturnType<typeof vi.fn>; createFolder: ReturnType<typeof vi.fn>; isImage: ReturnType<typeof vi.fn> };
    let uploads: { uploadFiles: ReturnType<typeof vi.fn> };
    let state: AssetPickerStateService;

    function setup(multiple = true) {
        created = new Subject();
        assets = {
            getLabels: vi.fn(() => AssetLibraryService.prototype.getLabels()),
            search: vi.fn(() => of({ results: [image], totalCount: 1 })),
            createFolder: vi.fn(() => created),
            isImage: vi.fn(() => true),
        };
        uploads = { uploadFiles: vi.fn(() => of([])) };
        TestBed.configureTestingModule({ providers: [
            AssetPickerStateService,
            { provide: MAT_DIALOG_DATA, useValue: { rootFolderUrl: root, multiple } },
            { provide: AssetLibraryService, useValue: assets },
            { provide: AssetLibraryUploadCoordinatorService, useValue: uploads },
        ] });
        state = TestBed.inject(AssetPickerStateService);
    }

    it('keeps selection, clears search, enters the created folder and uploads there', () => {
        setup();
        state.selectAsset(image);
        state.onSearch('image');
        state.openFolderForm();
        state.onFolderNameChange('  new folder  ');
        state.createFolder();
        state.createFolder();
        expect(assets.createFolder).toHaveBeenCalledExactlyOnceWith(root, 'new folder');
        created.next(null);
        expect(state.currentFolderUrl()).toBe(`${root}/new folder`);
        expect(state.searchValue()).toBe('');
        expect(state.selectedAssets()).toEqual([image]);
        expect(state.folderFormOpen()).toBe(false);
        state.uploadFiles([new File(['image'], 'new.jpg', { type: 'image/jpeg' })]);
        expect(uploads.uploadFiles.mock.calls[0][0]).toBe(`${root}/new folder`);
    });

    it('keeps the form, folder and selection on failure and allows retry after editing', () => {
        setup();
        state.selectAsset(image);
        state.openFolderForm();
        state.onFolderNameChange('new folder');
        state.createFolder();
        created.error(new Error('Folder already exists.'));
        expect(state.folderNameError()).toBe('Folder already exists.');
        expect(state.creatingFolder()).toBe(false);
        expect(state.folderFormOpen()).toBe(true);
        expect(state.currentFolderUrl()).toBe(root);
        expect(state.selectedAssets()).toEqual([image]);
        state.onFolderNameChange('another folder');
        expect(state.folderNameError()).toBeNull();
    });

    it('rejects invalid and empty names without calling the server', () => {
        setup();
        for (const name of ['', '  ', 'UPPER', 'ab', 'a--b', 'a/b']) {
            state.onFolderNameChange(name);
            state.createFolder();
        }
        expect(assets.createFolder).not.toHaveBeenCalled();
    });

    it('shows the Assets API validation message inline', () => {
        setup();
        state.openFolderForm();
        state.onFolderNameChange('new folder');
        state.createFolder();
        created.error(new HttpErrorResponse({
            status: 400,
            error: { message: 'Folder already exists.' },
        }));
        expect(state.folderNameError()).toBe('Folder already exists.');
    });

    it('allows retrying the same valid name after a transient server failure', () => {
        setup();
        state.openFolderForm();
        state.onFolderNameChange('new folder');
        state.createFolder();
        created.error(new Error('Service unavailable.'));
        assets.createFolder.mockReturnValue(of(null));

        state.createFolder();

        expect(assets.createFolder).toHaveBeenCalledTimes(2);
        expect(state.folderNameError()).toBeNull();
        expect(state.currentFolderUrl()).toBe(`${root}/new folder`);
    });

    it('retains the single-selection contract when entering a new folder', () => {
        setup(false);
        state.selectAsset(image);
        state.onFolderNameChange('new folder');
        state.createFolder();
        created.next(null);
        expect(state.selectedCount()).toBe(0);
    });

    it('uses the captured parent when navigation happens during creation', () => {
        setup();
        state.onFolderNameChange('new folder');
        state.createFolder();
        state.navigateToBreadcrumb(`${root}/other`);
        created.next(null);
        expect(state.currentFolderUrl()).toBe(`${root}/new folder`);
    });

    it('prevents uploading while a folder is being created', () => {
        setup();
        state.onFolderNameChange('new folder');
        state.createFolder();
        state.uploadFiles([new File(['image'], 'new.jpg', { type: 'image/jpeg' })]);
        expect(uploads.uploadFiles).not.toHaveBeenCalled();
    });

    it('prevents folder creation while files are being uploaded', () => {
        setup();
        state.uploading.set(true);
        state.onFolderNameChange('new folder');
        state.createFolder();
        expect(assets.createFolder).not.toHaveBeenCalled();
    });

    it('does not erase a draft when New folder is clicked again', () => {
        setup();
        state.openFolderForm();
        state.onFolderNameChange('my folder');
        state.openFolderForm();
        expect(state.folderName()).toBe('my folder');
    });

    it('does not reopen or reset the folder form during creation', () => {
        setup();
        state.openFolderForm();
        state.onFolderNameChange('my folder');
        state.createFolder();
        state.openFolderForm();
        expect(state.folderName()).toBe('my folder');
        expect(state.creatingFolder()).toBe(true);
    });

    it('ignores a stale root search response after entering the created folder', () => {
        setup();
        const staleSearch = new Subject<{ results: typeof image[]; totalCount: number }>();
        assets.search.mockReturnValueOnce(staleSearch).mockReturnValue(of({ results: [], totalCount: 0 }));
        state.navigateToBreadcrumb(root);
        state.selectAsset(image);
        state.onFolderNameChange('new folder');
        state.createFolder();
        created.next(null);

        staleSearch.next({ results: [image], totalCount: 1 });

        expect(state.currentFolderUrl()).toBe(`${root}/new folder`);
        expect(state.entries()).toEqual([]);
        expect(state.loading()).toBe(false);
        expect(state.selectedAssets()).toEqual([image]);
    });

    it('cancels a pending search debounce after folder creation', () => {
        vi.useFakeTimers();
        try {
            setup();
            state.onSearch('old search');
            state.onFolderNameChange('new folder');
            state.createFolder();
            created.next(null);
            vi.advanceTimersByTime(300);

            expect(assets.search).toHaveBeenCalledTimes(2);
            expect(assets.search).toHaveBeenLastCalledWith(`${root}/new folder`, '');
        } finally {
            vi.useRealTimers();
        }
    });

    it('unsubscribes from folder creation when the picker is destroyed', () => {
        setup();
        state.onFolderNameChange('new folder');
        state.createFolder();
        expect(created.observed).toBe(true);

        TestBed.resetTestingModule();
        expect(created.observed).toBe(false);
        created.next(null);
        expect(state.currentFolderUrl()).toBe(root);
    });

    it('preserves selections from multiple folders while creating a nested folder', () => {
        setup();
        const nestedImage = { ...image, name: 'nested.jpg', relativeUrl: `${root}/nested/nested.jpg` };
        state.selectAsset(image);
        state.navigateTo({ type: 'folder', name: 'nested', relativeUrl: `${root}/nested/` });
        state.selectAsset(nestedImage);
        state.onFolderNameChange('new folder');
        state.createFolder();
        created.next(null);

        expect(assets.createFolder).toHaveBeenCalledExactlyOnceWith(`${root}/nested`, 'new folder');
        expect(state.currentFolderUrl()).toBe(`${root}/nested/new folder`);
        expect(state.selectedAssets()).toEqual([image, nestedImage]);
    });
});

describe('Folder names accepted by Admin and Designer', () => {
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
