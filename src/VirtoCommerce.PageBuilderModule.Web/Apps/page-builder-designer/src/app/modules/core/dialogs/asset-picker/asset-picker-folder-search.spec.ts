import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { Subject } from 'rxjs';
import { AssetLibraryService, AssetLibraryUploadCoordinatorService } from '@core/services';
import { AssetLibrarySearchResult } from '@core/services/asset-library.models';
import { AssetPickerStateService } from './asset-picker-state.service';

describe('Asset picker search when opening a folder', () => {
    let search: ReturnType<typeof vi.fn>;
    let state: AssetPickerStateService;

    beforeEach(() => {
        vi.useFakeTimers();
        search = vi.fn(() => new Subject<AssetLibrarySearchResult>());
        TestBed.configureTestingModule({ providers: [
            AssetPickerStateService,
            { provide: MAT_DIALOG_DATA, useValue: { rootFolderUrl: '/folder' } },
            { provide: AssetLibraryService, useValue: {
                isImage: () => true,
                getPreviewUrl: () => '/preview',
                getLabels: () => ({ title: 'Assets', select: 'Select' }),
                search,
            } },
            { provide: AssetLibraryUploadCoordinatorService, useValue: { uploadFiles: vi.fn() } },
        ] });
        state = TestBed.inject(AssetPickerStateService);
    });

    afterEach(() => {
        vi.useRealTimers();
    });

    it('clears the search when a found folder is opened', () => {
        state.onSearch('p500');
        vi.advanceTimersByTime(300);

        state.navigateTo({ type: 'folder', name: 'p500', relativeUrl: '/folder/p500' });

        expect(state.searchValue()).toBe('');
        expect(search).toHaveBeenLastCalledWith('/folder/p500', '', expect.anything());
    });

    it('clears the search when navigating through a breadcrumb', () => {
        state.navigateTo({ type: 'folder', name: 'child', relativeUrl: '/folder/child' });
        state.onSearch('p500');
        vi.advanceTimersByTime(300);

        state.navigateToBreadcrumb('/folder');

        expect(state.searchValue()).toBe('');
        expect(search).toHaveBeenLastCalledWith('/folder', '', expect.anything());
    });

    it('does not run a pending search after a folder is opened', () => {
        state.onSearch('p500');
        state.navigateTo({ type: 'folder', name: 'p500', relativeUrl: '/folder/p500' });
        const callCount = search.mock.calls.length;

        vi.advanceTimersByTime(300);

        expect(search).toHaveBeenCalledTimes(callCount);
        expect(state.searchValue()).toBe('');
    });
});
