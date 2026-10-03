import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AppConfig, BuilderHttpClient, EvaluatorService } from '@integration/services';

import { TemplatesService } from './templates.service';

describe('TemplatesService page versions', () => {
    let service: TemplatesService;
    let requests: HttpTestingController;
    const url = (id: string) => `/api/page-builder-pages/grouped/${id}/content`;
    const document = { settings: {}, content: [] };

    beforeEach(() => {
        TestBed.configureTestingModule({ providers: [
            provideHttpClient(), provideHttpClientTesting(), TemplatesService, BuilderHttpClient,
            { provide: EvaluatorService, useValue: { evaluate: (value: unknown) => value } },
            { provide: AppConfig, useValue: {
                getContext: () => ({}),
                getValueByEntryType: (_: string, context: any) => ({ url: url(context.groupId), method: 'GET', cacheable: true }),
                getValue: (_: string, context: any) => ({ url: url(context.groupId), method: 'POST', body: context.content }),
            } },
        ] });
        service = TestBed.inject(TemplatesService);
        requests = TestBed.inject(HttpTestingController);
    });

    afterEach(() => {
        try {
            requests.verify();
        } finally {
            TestBed.resetTestingModule();
        }
    });

    function load(id: string, version: string): void {
        service.getTemplate('', 'pages', { type: 'pages' } as any, id).subscribe();
        requests.expectOne(url(id)).flush(document, { headers: { ETag: version } });
    }

    it('sends the read version and advances it only after a successful save', () => {
        load('one', '"v1"');
        service.saveGroupedPage('one', document).subscribe();
        const first = requests.expectOne(url('one'));
        expect(first.request.headers.get('If-Match')).toBe('"v1"');
        first.flush(null, { status: 204, statusText: 'No Content', headers: { ETag: '"v2"' } });
        service.saveGroupedPage('one', document).subscribe();
        const second = requests.expectOne(url('one'));
        expect(second.request.headers.get('If-Match')).toBe('"v2"');
        second.flush(null, { status: 204, statusText: 'No Content', headers: { ETag: '"v3"' } });
    });

    it('propagates a conflict and keeps the stale token on a retry', () => {
        load('one', '"v1"');
        let error: any;
        service.saveGroupedPage('one', document).subscribe({ error: value => error = value });
        requests.expectOne(url('one')).flush('Page changed', { status: 412, statusText: 'Precondition Failed' });
        expect(error.status).toBe(412);
        service.saveGroupedPage('one', document).subscribe({ error: () => undefined });
        const retry = requests.expectOne(url('one'));
        expect(retry.request.headers.get('If-Match')).toBe('"v1"');
        retry.flush('Page changed', { status: 412, statusText: 'Precondition Failed' });
    });

    it('refuses an unversioned save before sending any content', () => {
        let error: any;
        service.saveGroupedPage('one', document).subscribe({ error: value => error = value });
        expect(error?.status).toBe(428);
        requests.expectNone(url('one'));
    });

    it('preserves the configured save contract for an external content backend', () => {
        vi.spyOn(TestBed.inject(AppConfig), 'getValue').mockReturnValue({ url: '/custom/content', method: 'POST', body: document });
        service.saveGroupedPage('one', document).subscribe();
        const save = requests.expectOne('/custom/content');
        expect(save.request.headers.has('If-Match')).toBe(false);
        save.flush(null);
    });

    it.each([null, { method: 'POST' }, [], [null]].map(descriptor => ({ descriptor })))('rejects an unavailable save request without reporting success: %j', ({ descriptor }) => {
        vi.spyOn(TestBed.inject(AppConfig), 'getValue').mockReturnValue(descriptor);
        let error: unknown;
        const success = vi.fn();
        service.saveGroupedPage('one', document).subscribe({ next: success, error: value => error = value });
        expect(error).toBeInstanceOf(Error);
        expect(success).not.toHaveBeenCalled();
        requests.expectNone(() => true);
    });

    it('keeps each page version separate', () => {
        load('one', '"a"');
        load('two', '"b"');
        service.saveGroupedPage('one', document).subscribe();
        const save = requests.expectOne(url('one'));
        expect(save.request.headers.get('If-Match')).toBe('"a"');
        save.flush(null, { headers: { ETag: '"a2"' } });
    });

    it('does not attach a save response version to a document reloaded while that save was pending', () => {
        load('one', '"v1"');
        service.saveGroupedPage('one', document).subscribe();
        const pendingSave = requests.expectOne(url('one'));
        load('one', '"v1"');
        pendingSave.flush(null, { status: 204, statusText: 'No Content', headers: { ETag: '"v2"' } });

        service.saveGroupedPage('one', document).subscribe({ error: () => undefined });
        const retry = requests.expectOne(url('one'));
        expect(retry.request.headers.get('If-Match')).toBe('"v1"');
        retry.flush('Page changed', { status: 412, statusText: 'Precondition Failed' });
    });

    it('keeps the version of a newer reload when an older save response arrives afterwards', () => {
        load('one', '"v1"');
        service.saveGroupedPage('one', document).subscribe();
        const pendingSave = requests.expectOne(url('one'));
        load('one', '"v3"');
        pendingSave.flush(null, { status: 204, statusText: 'No Content', headers: { ETag: '"v2"' } });

        service.saveGroupedPage('one', document).subscribe();
        const nextSave = requests.expectOne(url('one'));
        expect(nextSave.request.headers.get('If-Match')).toBe('"v3"');
        nextSave.flush(null, { headers: { ETag: '"v4"' } });
    });

    it('does not restore a usable version after a failed reload from a pending save response', () => {
        load('one', '"v1"');
        service.saveGroupedPage('one', document).subscribe();
        const pendingSave = requests.expectOne(url('one'));
        service.getTemplate('', 'pages', { type: 'pages' } as any, 'one').subscribe({ error: () => undefined });
        requests.expectOne(url('one')).flush('Failed', { status: 500, statusText: 'Error' });
        pendingSave.flush(null, { headers: { ETag: '"v2"' } });

        let error: any;
        service.saveGroupedPage('one', document).subscribe({ error: value => error = value });
        expect(error.status).toBe(428);
        requests.expectNone(url('one'));
    });

    it('loads an unseeded page as a blank document with its server version', () => {
        let loaded: unknown;
        service.getTemplate('', 'pages', { type: 'pages' } as any, 'one').subscribe(value => loaded = value);
        requests.expectOne(url('one')).flush(null, { status: 404, statusText: 'Not Found', headers: { ETag: '"empty"' } });
        expect((loaded as any).content).toEqual([]);
        service.saveGroupedPage('one', document).subscribe();
        const save = requests.expectOne(url('one'));
        expect(save.request.headers.get('If-Match')).toBe('"empty"');
        save.flush(null, { headers: { ETag: '"saved"' } });
    });

    it('does not turn a deleted or unauthorized page into a blank editable page', () => {
        let error: any;
        service.getTemplate('', 'pages', { type: 'pages' } as any, 'one').subscribe({ error: value => error = value });
        requests.expectOne(url('one')).flush(null, { status: 404, statusText: 'Not Found' });
        expect(error.status).toBe(404);
    });

    it('reloads body and version together even when the descriptor is cacheable', () => {
        load('one', '"v1"');
        load('one', '"v2"');
        service.saveGroupedPage('one', document).subscribe();
        const save = requests.expectOne(url('one'));
        expect(save.request.headers.get('If-Match')).toBe('"v2"');
        save.flush(null, { headers: { ETag: '"v3"' } });
    });

    it('drops the previous token when a reload no longer returns an ETag', () => {
        load('one', '"v1"');
        service.getTemplate('', 'pages', { type: 'pages' } as any, 'one').subscribe();
        requests.expectOne(url('one')).flush(document);
        let error: any;
        service.saveGroupedPage('one', document).subscribe({ error: value => error = value });
        expect(error.status).toBe(428);
        requests.expectNone(url('one'));
    });

    it('does not accept a fresh version when document conversion fails', () => {
        load('one', '"v1"');
        let loadError: unknown;
        service.getTemplate('', 'pages', { type: 'pages' } as any, 'one').subscribe({ error: value => loadError = value });
        requests.expectOne(url('one')).flush({ pageContent: 'invalid JSON' }, { headers: { ETag: '"v2"' } });
        expect(loadError).toBeDefined();
        let saveError: any;
        service.saveGroupedPage('one', document).subscribe({ error: value => saveError = value });
        expect(saveError.status).toBe(428);
        requests.expectNone(url('one'));
    });

    it('does not accept a fresh version when the document cannot be prepared for the editor', () => {
        load('one', '"v1"');
        let loadError: unknown;
        service.getTemplate('', 'pages', { type: 'pages' } as any, 'one').subscribe({ error: value => loadError = value });
        requests.expectOne(url('one')).flush({
            settings: {}, content: [{ type: 'text', blocks: {} }],
        }, { headers: { ETag: '"v2"' } });
        expect(loadError).toBeDefined();

        let saveError: any;
        service.saveGroupedPage('one', document).subscribe({ error: value => saveError = value });
        expect(saveError?.status).toBe(428);
        requests.expectNone(url('one'));
    });

    it('does not accept a version from a failed GET', () => {
        load('one', '"v1"');
        service.getTemplate('', 'pages', { type: 'pages' } as any, 'one').subscribe({ error: () => undefined });
        requests.expectOne(url('one')).flush('Failed', { status: 500, statusText: 'Error', headers: { ETag: '"v2"' } });
        let saveError: any;
        service.saveGroupedPage('one', document).subscribe({ error: value => saveError = value });
        expect(saveError.status).toBe(428);
        requests.expectNone(url('one'));
    });

    it('does not pair a grouped version with a fallback document', () => {
        vi.spyOn(TestBed.inject(AppConfig), 'getValueByEntryType').mockReturnValue([
            { url: url('one'), method: 'GET' }, { url: '/fallback', method: 'GET' },
        ]);
        service.getTemplate('', 'pages', { type: 'pages' } as any, 'one').subscribe();
        requests.expectOne(url('one')).flush(null, { headers: { ETag: '"v2"' } });
        requests.expectOne('/fallback').flush(document);
        let saveError: any;
        service.saveGroupedPage('one', document).subscribe({ error: value => saveError = value });
        expect(saveError.status).toBe(428);
        requests.expectNone(url('one'));
    });
});
