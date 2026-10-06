import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { Subject } from 'rxjs';
import { AssetLibraryEntry, AssetLibraryService, AssetLibraryUploadCoordinatorService } from '@core/services';
import { AssetLibrarySearchResult } from '@core/services/asset-library.models';
import { AssetPickerStateService } from './asset-picker-state.service';

describe('Asset picker loading during upload', () => {
    let lists: Subject<AssetLibrarySearchResult>[];
    let uploads: Subject<AssetLibraryEntry[]>[];
    let uploadFiles: ReturnType<typeof vi.fn>;
    let state: AssetPickerStateService;

    beforeEach(() => {
        lists = [];
        uploads = [];
        uploadFiles = vi.fn(() => {
            const response = new Subject<AssetLibraryEntry[]>();
            uploads.push(response);
            return response;
        });
        TestBed.configureTestingModule({ providers: [
            AssetPickerStateService,
            { provide: MAT_DIALOG_DATA, useValue: { rootFolderUrl: '/folder' } },
            { provide: AssetLibraryService, useValue: {
                isImage: () => true,
                getPreviewUrl: () => '/preview',
                getLabels: () => ({ title: 'Assets', select: 'Select', uploadError: 'Upload failed' }),
                search: vi.fn(() => {
                    const response = new Subject<AssetLibrarySearchResult>();
                    lists.push(response);
                    return response;
                }),
            } },
            { provide: AssetLibraryUploadCoordinatorService, useValue: { uploadFiles } },
        ] });
        state = TestBed.inject(AssetPickerStateService);
    });

    it('allows retry after upload fails while an invalidated list request is pending', () => {
        const file = new File(['image'], 'image.png', { type: 'image/png' });
        expect(state.loading()).toBe(true);

        state.uploadFiles([file]);
        uploads[0].error(new Error('Name lookup failed'));

        expect(state.uploading()).toBe(false);
        expect(state.loading()).toBe(false);
        expect(state.error()).toBe('Name lookup failed');

        lists[0].next({ totalCount: 1, results: [{ type: 'folder', name: 'obsolete' }] });
        expect(state.entries()).toEqual([]);
        expect(state.error()).toBe('Name lookup failed');

        state.uploadFiles([file]);
        expect(uploadFiles).toHaveBeenCalledTimes(2);
        uploads[1].next([]);
        expect(state.loading()).toBe(true);
        lists[1].next({ totalCount: 0, results: [] });
        expect(state.loading()).toBe(false);
        expect(state.uploading()).toBe(false);
        expect(state.error()).toBeNull();
    });

    it('keeps a newer list request loading when upload fails', () => {
        state.uploadFiles([new File(['image'], 'image.png', { type: 'image/png' })]);
        state.navigateToBreadcrumb('/folder/child');

        uploads[0].error(new Error('Upload failed'));
        expect(state.uploading()).toBe(false);
        expect(state.loading()).toBe(true);

        lists[0].next({ totalCount: 1, results: [{ type: 'folder', name: 'obsolete' }] });
        expect(state.loading()).toBe(true);
        expect(state.entries()).toEqual([]);

        lists[1].next({ totalCount: 0, results: [] });
        expect(state.loading()).toBe(false);
        expect(state.totalCount()).toBe(0);
    });
    it('preserves the displayed page and search when upload fails', () => {
        state.searchValue.set('old');
        state.onPage(3, 20);
        lists[1].next({ totalCount: 100, results: [{ type: 'folder', name: 'old page' }] });
        state.uploadFiles([new File(['image'], 'image.png', { type: 'image/png' })]);
        uploads[0].error(new Error('Upload failed'));
        expect(state.searchValue()).toBe('old');
        expect(state.pageIndex()).toBe(3);
        expect(state.entries()[0].name).toBe('old page');
    });

    it('moves to the returned page containing the successful upload', () => {
        state.uploadFiles([new File(['image'], 'z.png', { type: 'image/png' })]);
        const uploaded = { type: 'blob' as const, name: 'z.png', relativeUrl: '/folder/z.png', contentType: 'image/png' };
        uploads[0].next([uploaded]);
        lists[1].next({ totalCount: 101, fileCount: 101, skip: 100, results: [uploaded] });
        expect(state.pageIndex()).toBe(5);
        expect(state.entries()).toEqual([uploaded]);
        expect(state.selectedAssets()).toEqual([uploaded]);
    });

});
