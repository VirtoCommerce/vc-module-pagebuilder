import { vi } from 'vitest';
import { of } from 'rxjs';
import { AssetLibraryEntry, AssetLibraryService } from '@core/services/asset-library.service';

export function createAssetLibraryMock(overrides: Partial<AssetLibraryService> = {}) {
    return {
        getLabels: vi.fn(overrides.getLabels ?? (() => AssetLibraryService.prototype.getLabels())),
        getRootFolderUrl: vi.fn(overrides.getRootFolderUrl ?? (() => '/stores/store/Page Builder')),
        search: vi.fn(overrides.search ?? (() => of({ results: [] as AssetLibraryEntry[], totalCount: 0 }))),
        createFolder: vi.fn(overrides.createFolder ?? (() => of(undefined))),
        canCreateFolder: vi.fn(overrides.canCreateFolder ?? (() => true)),
        canUpload: vi.fn(overrides.canUpload ?? (() => true)),
        isImage: vi.fn(overrides.isImage ?? (() => true)),
        getPublicUrl: vi.fn(overrides.getPublicUrl ?? ((entry: AssetLibraryEntry) => `/assets${entry.relativeUrl}`)),
        getPreviewUrl: vi.fn(overrides.getPreviewUrl ?? ((entry: AssetLibraryEntry) => `/assets${entry.relativeUrl}`)),
    };
}
