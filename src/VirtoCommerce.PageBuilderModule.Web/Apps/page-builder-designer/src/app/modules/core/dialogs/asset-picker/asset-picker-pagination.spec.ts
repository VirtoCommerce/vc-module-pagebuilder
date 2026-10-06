import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA } from '@angular/material/dialog';
import { Subject } from 'rxjs';
import { AssetLibraryService, AssetLibraryUploadCoordinatorService } from '@core/services';
import { AssetLibrarySearchResult } from '@core/services/asset-library.models';
import { AssetPickerStateService } from './asset-picker-state.service';

describe('Asset picker server pagination', () => {
    let responses: Subject<AssetLibrarySearchResult>[];
    let assets: { search: ReturnType<typeof vi.fn>; getLabels: ReturnType<typeof vi.fn> };
    let state: AssetPickerStateService;

    beforeEach(() => {
        responses = [];
        assets = {
            getLabels: vi.fn(() => ({ title: 'Assets', select: 'Select', assetsCounter: '{count} assets' })),
            search: vi.fn(() => {
                const response = new Subject<AssetLibrarySearchResult>();
                responses.push(response);
                return response;
            }),
        };
        TestBed.configureTestingModule({ providers: [
            AssetPickerStateService,
            { provide: MAT_DIALOG_DATA, useValue: { rootFolderUrl: '/folder' } },
            { provide: AssetLibraryService, useValue: assets },
            { provide: AssetLibraryUploadCoordinatorService, useValue: {} },
        ] });
        state = TestBed.inject(AssetPickerStateService);
    });

    afterEach(() => vi.useRealTimers());

    it('requests 20 entries initially and uses the offset for the selected page', () => {
        expect(assets.search).toHaveBeenLastCalledWith('/folder', '', { skip: 0, take: 20, sort: 'name' });
        responses[0].next({ totalCount: 500, results: [] });
        state.onPage(3, 20);
        expect(assets.search).toHaveBeenLastCalledWith('/folder', '', { skip: 60, take: 20, sort: 'name' });
        expect(state.totalCount()).toBe(500);
    });

    it('resets the offset when the page size or location changes', () => {
        state.onPage(3, 20);
        state.onPage(3, 50);
        expect(assets.search).toHaveBeenLastCalledWith('/folder', '', { skip: 0, take: 50, sort: 'name' });
        state.onPage(2, 50);
        state.navigateToBreadcrumb('/folder/child');
        expect(assets.search).toHaveBeenLastCalledWith('/folder/child', '', { skip: 0, take: 50, sort: 'name' });
    });

    it('drops stale responses during rapid page changes', () => {
        state.onPage(1, 20);
        responses[1].next({ totalCount: 500, results: [{ type: 'folder', name: 'new' }] });
        responses[0].next({ totalCount: 1, results: [{ type: 'folder', name: 'old' }] });
        expect(state.entries()[0].name).toBe('new');
        expect(state.totalCount()).toBe(500);
        expect(state.loading()).toBe(false);
    });

    it('invalidates immediately when typing and searches from page one after debounce', () => {
        vi.useFakeTimers();
        state.onPage(4, 20);
        state.onSearch('asset-499');
        responses[1].next({ totalCount: 500, results: [{ type: 'folder', name: 'obsolete' }] });
        expect(state.entries()).toEqual([]);
        vi.advanceTimersByTime(300);
        expect(assets.search).toHaveBeenLastCalledWith('/folder', 'asset-499', { skip: 0, take: 20, sort: 'name' });
        responses[2].next({ totalCount: 1, results: [{ type: 'folder', name: 'match' }] });
        expect(state.totalCount()).toBe(1);
    });

    it('sends the picker accept filter on initial load and subsequent pages', () => {
        TestBed.resetTestingModule();
        TestBed.configureTestingModule({ providers: [
            AssetPickerStateService,
            { provide: MAT_DIALOG_DATA, useValue: { rootFolderUrl: '/folder', accept: ['image/*', '.pdf'] } },
            { provide: AssetLibraryService, useValue: assets },
            { provide: AssetLibraryUploadCoordinatorService, useValue: {} },
        ] });
        state = TestBed.inject(AssetPickerStateService);
        expect(assets.search).toHaveBeenLastCalledWith('/folder', '', {
            skip: 0, take: 20, sort: 'name', acceptedTypes: ['image/*', '.pdf'],
        });
        state.onPage(1, 20);
        expect(assets.search).toHaveBeenLastCalledWith('/folder', '', {
            skip: 20, take: 20, sort: 'name', acceptedTypes: ['image/*', '.pdf'],
        });
    });
});
