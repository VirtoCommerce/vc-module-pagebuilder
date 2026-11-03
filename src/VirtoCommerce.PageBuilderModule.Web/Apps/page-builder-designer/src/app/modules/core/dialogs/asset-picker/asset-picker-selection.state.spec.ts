import { AssetLibraryEntry, AssetLibraryService } from '@core/services';

import { AssetPickerSelectionState } from './asset-picker-selection.state';

describe('AssetPickerSelectionState', () => {
    const imageA = createEntry('a.jpg');
    const imageB = createEntry('b.jpg');

    function createEntry(name: string): AssetLibraryEntry {
        return {
            type: 'blob',
            name,
            relativeUrl: `/stores/B2B-store/Page Builder/${name}`,
            contentType: 'image/jpeg'
        };
    }

    function createAssetsService(): AssetLibraryService {
        return {
            getPublicUrl: vi.fn((entry: AssetLibraryEntry) => `/assets${entry.relativeUrl}`),
            getPreviewUrl: vi.fn((entry: AssetLibraryEntry) => `/assets${entry.relativeUrl}?preview=true`),
            isImage: vi.fn(() => true)
        } as unknown as AssetLibraryService;
    }

    it('keeps multiple selected assets and returns all of them', () => {
        const state = new AssetPickerSelectionState(createAssetsService(), ['image/*'], true);

        state.selectAsset(imageA);
        state.selectAsset(imageB);

        const result = state.getSelectionResult();

        expect(Array.isArray(result)).toBe(true);
        expect(result).toEqual([
            {
                entry: imageA,
                url: '/assets/stores/B2B-store/Page Builder/a.jpg',
                previewUrl: '/assets/stores/B2B-store/Page Builder/a.jpg?preview=true'
            },
            {
                entry: imageB,
                url: '/assets/stores/B2B-store/Page Builder/b.jpg',
                previewUrl: '/assets/stores/B2B-store/Page Builder/b.jpg?preview=true'
            }
        ]);
    });

    it('replaces selection in single-select mode', () => {
        const state = new AssetPickerSelectionState(createAssetsService(), ['image/*'], false);

        state.selectAsset(imageA);
        state.selectAsset(imageB);

        expect(state.getSelectionResult()).toEqual({
            entry: imageB,
            url: '/assets/stores/B2B-store/Page Builder/b.jpg',
            previewUrl: '/assets/stores/B2B-store/Page Builder/b.jpg?preview=true'
        });
    });
});
