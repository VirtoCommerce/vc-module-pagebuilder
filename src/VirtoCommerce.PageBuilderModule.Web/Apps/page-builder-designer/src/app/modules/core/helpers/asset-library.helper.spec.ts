import { assetLibraryHelpers } from './index';

describe('assetLibraryHelpers', () => {
    describe('toPublicAssetUrl', () => {
        it('keeps already public /assets URLs', () => {
            expect(assetLibraryHelpers.toPublicAssetUrl('/assets/stores/B2B-store/Page%20Builder/hero.png'))
                .toBe('/assets/stores/B2B-store/Page%20Builder/hero.png');
        });

        it('prefixes store-relative asset paths', () => {
            expect(assetLibraryHelpers.toPublicAssetUrl('/stores/B2B-store/Page%20Builder/hero.png'))
                .toBe('/assets/stores/B2B-store/Page%20Builder/hero.png');
        });

        it('handles unicode paths without decoding them away', () => {
            expect(assetLibraryHelpers.toPublicAssetUrl('/stores/B2B-store/Page Builder/снимок.png'))
                .toBe('/assets/stores/B2B-store/Page Builder/снимок.png');
        });

        it('keeps absolute and data URLs unchanged', () => {
            expect(assetLibraryHelpers.toPublicAssetUrl('https://cdn.example.com/assets/hero.png'))
                .toBe('https://cdn.example.com/assets/hero.png');
            expect(assetLibraryHelpers.toPublicAssetUrl('data:image/png;base64,abc'))
                .toBe('data:image/png;base64,abc');
        });
    });

    describe('toAssetPreviewUrl', () => {
        it('adds a cache-busting timestamp to asset URLs', () => {
            expect(assetLibraryHelpers.toAssetPreviewUrl('/stores/B2B-store/Page Builder/hero.png', '2026-05-15T10:20:30Z'))
                .toBe('/assets/stores/B2B-store/Page Builder/hero.png?t=2026-05-15T10%3A20%3A30Z');
        });

        it('uses an ampersand when the URL already has a query string', () => {
            expect(assetLibraryHelpers.toAssetPreviewUrl('/assets/stores/B2B-store/hero.png?size=large', '2026-05-15'))
                .toBe('/assets/stores/B2B-store/hero.png?size=large&t=2026-05-15');
        });
    });

    describe('normalizeAssetFileName', () => {
        it('decodes, trims, normalizes and folds casing for provider-independent collision checks', () => {
            expect(assetLibraryHelpers.normalizeAssetFileName('  HERO%20Banner.PNG  ')).toBe('hero banner.png');
            expect(assetLibraryHelpers.normalizeAssetFileName('Cafe\u0301.png')).toBe('café.png');
        });
    });

});
