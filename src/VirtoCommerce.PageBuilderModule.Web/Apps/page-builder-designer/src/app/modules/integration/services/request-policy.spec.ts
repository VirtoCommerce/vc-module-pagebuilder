import { isAllowedRequest } from './request-policy';

const platform = 'https://admin.example.com/apps/page-builder-designer/index.html';

describe('request policy', () => {
    it('allows a read on the platform origin', () => {
        expect(isAllowedRequest('/api/stores/store1', 'GET', platform)).toBe(true);
        expect(isAllowedRequest('/api/stores/store1', null, platform)).toBe(true);
        expect(isAllowedRequest('https://admin.example.com/api/stores/store1', 'GET', platform)).toBe(true);
    });

    it('allows the writes the shipped settings and schemas make', () => {
        for (const url of [
            '/api/pagebuilder/save?storeId=store1',
            '/graphql',
            '/api/page-builder-assets/search',
            '/api/page-builder-pages/grouped/g1/content',
            '/api/assets',
            '/api/assets/folder',
            '/api/content/pages/store1/publishing',
            '/api/platform/security/users/search',
        ]) {
            expect(isAllowedRequest(url, 'POST', platform)).toBe(true);
        }
    });

    it('does not let a write reach another platform endpoint', () => {
        expect(isAllowedRequest('/api/platform/security/users', 'POST', platform)).toBe(false);
        expect(isAllowedRequest('/api/platform/security/roleassignments/new', 'POST', platform)).toBe(false);
        // a sibling that only shares the prefix string is not covered
        expect(isAllowedRequest('/api/assets-export', 'POST', platform)).toBe(false);
    });

    it('does not let a descriptor reach another origin', () => {
        expect(isAllowedRequest('https://elsewhere.example/steal', 'GET', platform)).toBe(false);
        expect(isAllowedRequest('https://elsewhere.example/steal', 'POST', platform)).toBe(false);
        expect(isAllowedRequest('//elsewhere.example/steal', 'GET', platform)).toBe(false);
        expect(isAllowedRequest('https://admin.example.com.evil.test/steal', 'GET', platform)).toBe(false);
    });
});
