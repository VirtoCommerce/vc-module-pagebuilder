import { TestBed } from '@angular/core/testing';

import { AppConfig } from '@integration/services';

import { AssetUrlService } from './asset-url.service';

describe('AssetUrlService', () => {
    let service: AssetUrlService;
    let appConfig: {
        getContext: ReturnType<typeof vi.fn>;
        getValue: ReturnType<typeof vi.fn>;
    };

    beforeEach(() => {
        appConfig = {
            getContext: vi.fn(() => ({ location: { params: { storeId: 'app-store' } } })),
            getValue: vi.fn((property: string, context: any) => {
                if (property === 'assetLibraryRootFolderUrl') {
                    return `/configured/${context.storeId}/Page Builder`;
                }

                if (property === 'assetsUrlTemplate') {
                    return context.publicAssetUrl;
                }

                return null;
            })
        };

        TestBed.configureTestingModule({
            providers: [
                AssetUrlService,
                { provide: AppConfig, useValue: appConfig }
            ]
        });
        service = TestBed.inject(AssetUrlService);
    });

    it('resolves root folder URL through config', () => {
        expect(service.getRootFolderUrl({ template: { storeId: 'template-store' } }))
            .toBe('/configured/template-store/Page Builder');
    });

    it('resolves store-relative asset URLs through config', () => {
        expect(service.getPublicAssetUrl('/stores/B2B-store/Page Builder/hero.png'))
            .toBe('/assets/stores/B2B-store/Page Builder/hero.png');
    });

    it('resolves relative asset URLs in service logic before applying config template', () => {
        expect(service.getPublicAssetUrl('Page Builder/hero.png'))
            .toBe('/assets/Page Builder/hero.png');
    });

    it('keeps already public asset URLs public', () => {
        expect(service.getPublicAssetUrl('/assets/stores/B2B-store/Page Builder/hero.png'))
            .toBe('/assets/stores/B2B-store/Page Builder/hero.png');
    });

    it('keeps absolute and data URLs unchanged', () => {
        expect(service.getPublicAssetUrl('https://cdn.example.com/hero.png'))
            .toBe('https://cdn.example.com/hero.png');
        expect(service.getPublicAssetUrl('data:image/png;base64,abc'))
            .toBe('data:image/png;base64,abc');
    });

    it('returns null for non-string URLs', () => {
        expect(service.getPublicAssetUrl({ url: '/stores/B2B-store/Page Builder/hero.png' }))
            .toBeNull();
    });

    it('adds preview timestamps without remapping configured public URLs again', () => {
        appConfig.getValue.mockImplementation((property: string, context: any) => {
            if (property === 'assetsUrlTemplate') {
                return `/cdn${context.publicAssetUrl}`;
            }

            return null;
        });

        expect(service.getPreviewUrl({
            type: 'blob',
            name: 'hero.png',
            relativeUrl: '/stores/B2B-store/Page Builder/hero.png',
            modifiedDate: '2026-05-15'
        })).toBe('/cdn/assets/stores/B2B-store/Page Builder/hero.png?t=2026-05-15');
    });
});
