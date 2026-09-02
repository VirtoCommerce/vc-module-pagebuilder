import { isUsablePreviewUrl } from './live-preview.utils';

describe('isUsablePreviewUrl', () => {

    const designerUrl = 'https://admin.govirto.com/Modules/PageBuilder/Content/page-builder-designer/index.html?storeId=B2B-store#/pages';

    it('accepts an absolute storefront url', () => {
        expect(isUsablePreviewUrl('https://store.govirto.com/designer-preview?ep=https://admin.govirto.com', designerUrl)).toBe(true);
        expect(isUsablePreviewUrl('http://localhost:3000/designer-preview', designerUrl)).toBe(true);
    });

    it('rejects an empty url', () => {
        expect(isUsablePreviewUrl(null, designerUrl)).toBe(false);
        expect(isUsablePreviewUrl(undefined, designerUrl)).toBe(false);
        expect(isUsablePreviewUrl('', designerUrl)).toBe(false);
        expect(isUsablePreviewUrl('   ', designerUrl)).toBe(false);
    });

    it('rejects a relative url left after the store settings failed to resolve', () => {
        // both settings are missing: combine(null, null) + the query of the template
        expect(isUsablePreviewUrl('?ep=https://admin.govirto.com', designerUrl)).toBe(false);
        // only the store url is missing
        expect(isUsablePreviewUrl('/designer-preview?ep=https://admin.govirto.com', designerUrl)).toBe(false);
    });

    it('rejects a url with unresolved placeholders', () => {
        expect(isUsablePreviewUrl('https://store.govirto.com/{{settings.previewPath}}', designerUrl)).toBe(false);
    });

    it('rejects a non-http url', () => {
        expect(isUsablePreviewUrl('javascript:alert(1)', designerUrl)).toBe(false);
        expect(isUsablePreviewUrl('file:///c:/preview.html', designerUrl)).toBe(false);
    });

    it('rejects a url pointing back at the designer document', () => {
        expect(isUsablePreviewUrl('https://admin.govirto.com/Modules/PageBuilder/Content/page-builder-designer/index.html?ep=x', designerUrl)).toBe(false);
    });

    it('accepts a storefront hosted on the designer origin under another path', () => {
        expect(isUsablePreviewUrl('https://admin.govirto.com/designer-preview', designerUrl)).toBe(true);
    });
});
