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
});
