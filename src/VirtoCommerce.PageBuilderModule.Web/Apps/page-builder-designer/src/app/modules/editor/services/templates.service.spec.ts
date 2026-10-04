import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AppConfig, BuilderHttpClient, EvaluatorService } from '@integration/services';

import { CookieService } from 'ngx-cookie-service';
import { EnvironmentRef } from '@integration/services/environment.ref';
import { createEntry } from '@app/testing';

import { TemplatesService } from './templates.service';

describe('TemplatesService page versions', () => {
    let service: TemplatesService;
    let requests: HttpTestingController;
    const url = (id: string) => `/api/page-builder-pages/grouped/${id}/content`;
    let config: AppConfig;
    const document = { settings: {}, content: [] };

    beforeEach(() => {
        TestBed.configureTestingModule({ providers: [
            provideHttpClient(), provideHttpClientTesting(), TemplatesService, BuilderHttpClient,
            AppConfig, EvaluatorService,
            { provide: EnvironmentRef, useValue: { nativeWindow: { location: window.location } } },
            { provide: CookieService, useValue: {} },
        ] });
        config = TestBed.inject(AppConfig);
        config.initConfigWith({
            templateUrl: { pages: { url: '/api/page-builder-pages/grouped/{{groupId}}/content', method: 'GET', versioned: true, cacheable: true } },
            saveGroupedPage: { url: '/api/page-builder-pages/grouped/{{groupId}}/content', method: 'POST', versioned: true, body: '{{=JSON.stringify(this.content)}}' },
        });
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
        service.getTemplate('', 'pages', createEntry({ type: 'pages' }), id).subscribe();
        requests.expectOne(url(id)).flush({ content: JSON.stringify(document), eTag: version }, { headers: { ETag: 'W/\"proxy\"' } });
    }

    it('sends the read version and advances it only after a successful save', () => {
        load('one', '"v1"');
        service.saveGroupedPage('one', document).subscribe();
        const first = requests.expectOne(url('one'));
        expect(first.request.headers.get('If-Match')).toBe('"v1"');
        first.flush({ eTag: '"v2"' });
        service.saveGroupedPage('one', document).subscribe();
        const second = requests.expectOne(url('one'));
        expect(second.request.headers.get('If-Match')).toBe('"v2"');
        second.flush({ eTag: '"v3"' });
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
        config.initConfigWith({ saveGroupedPage: { url: '/custom/content', method: 'POST', body: document } });
        service.saveGroupedPage('one', document).subscribe();
        const save = requests.expectOne('/custom/content');
        expect(save.request.headers.has('If-Match')).toBe(false);
        save.flush(null);
    });

    it.each([null, { method: 'POST' }, [], [null]].map(descriptor => ({ descriptor })))('rejects an unavailable save request without reporting success: %j', ({ descriptor }) => {
        config.initConfigWith({ saveGroupedPage: descriptor });
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
        save.flush({ eTag: '"a2"' });
    });

    it('does not attach a save response version to a document reloaded while that save was pending', () => {
        load('one', '"v1"');
        service.saveGroupedPage('one', document).subscribe();
        const pendingSave = requests.expectOne(url('one'));
        load('one', '"v1"');
        pendingSave.flush({ eTag: '"v2"' });

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
        pendingSave.flush({ eTag: '"v2"' });

        service.saveGroupedPage('one', document).subscribe();
        const nextSave = requests.expectOne(url('one'));
        expect(nextSave.request.headers.get('If-Match')).toBe('"v3"');
        nextSave.flush({ eTag: '"v4"' });
    });

    it('does not restore a usable version after a failed reload from a pending save response', () => {
        load('one', '"v1"');
        service.saveGroupedPage('one', document).subscribe();
        const pendingSave = requests.expectOne(url('one'));
        service.getTemplate('', 'pages', createEntry({ type: 'pages' }), 'one').subscribe({ error: () => undefined });
        requests.expectOne(url('one')).flush('Failed', { status: 500, statusText: 'Error' });
        pendingSave.flush({ eTag: '"v2"' });

        let error: any;
        service.saveGroupedPage('one', document).subscribe({ error: value => error = value });
        expect(error.status).toBe(428);
        requests.expectNone(url('one'));
    });

    it('loads an unseeded page as a blank document with its server version', () => {
        let loaded: unknown;
        service.getTemplate('', 'pages', createEntry({ type: 'pages' }), 'one').subscribe(value => loaded = value);
        requests.expectOne(url('one')).flush({ content: JSON.stringify(document), eTag: '"empty"' });
        expect((loaded as any).content).toEqual([]);
        service.saveGroupedPage('one', document).subscribe();
        const save = requests.expectOne(url('one'));
        expect(save.request.headers.get('If-Match')).toBe('"empty"');
        save.flush({ eTag: '"saved"' });
    });

    it('does not turn a deleted or unauthorized page into a blank editable page', () => {
        let error: any;
        service.getTemplate('', 'pages', createEntry({ type: 'pages' }), 'one').subscribe({ error: value => error = value });
        requests.expectOne(url('one')).flush(null, { status: 404, statusText: 'Not Found' });
        expect(error.status).toBe(404);
    });

    it('reloads body and version together even when the descriptor is cacheable', () => {
        load('one', '"v1"');
        load('one', '"v2"');
        service.saveGroupedPage('one', document).subscribe();
        const save = requests.expectOne(url('one'));
        expect(save.request.headers.get('If-Match')).toBe('"v2"');
        save.flush({ eTag: '"v3"' });
    });

    it('drops the previous token when a reload no longer returns an ETag', () => {
        load('one', '"v1"');
        service.getTemplate('', 'pages', createEntry({ type: 'pages' }), 'one').subscribe();
        requests.expectOne(url('one')).flush(document);
        let error: any;
        service.saveGroupedPage('one', document).subscribe({ error: value => error = value });
        expect(error.status).toBe(428);
        requests.expectNone(url('one'));
    });

    it('does not accept a fresh version when document conversion fails', () => {
        load('one', '"v1"');
        let loadError: unknown;
        service.getTemplate('', 'pages', createEntry({ type: 'pages' }), 'one').subscribe({ error: value => loadError = value });
        requests.expectOne(url('one')).flush({ content: 'invalid JSON', eTag: '"v2"' });
        expect(loadError).toBeDefined();
        let saveError: any;
        service.saveGroupedPage('one', document).subscribe({ error: value => saveError = value });
        expect(saveError.status).toBe(428);
        requests.expectNone(url('one'));
    });

    it('does not accept a fresh version when the document cannot be prepared for the editor', () => {
        load('one', '"v1"');
        let loadError: unknown;
        service.getTemplate('', 'pages', createEntry({ type: 'pages' }), 'one').subscribe({ error: value => loadError = value });
        requests.expectOne(url('one')).flush({ content: JSON.stringify({ settings: {}, content: [{ type: 'text', blocks: {} }] }), eTag: '"v2"' });
        expect(loadError).toBeDefined();

        let saveError: any;
        service.saveGroupedPage('one', document).subscribe({ error: value => saveError = value });
        expect(saveError?.status).toBe(428);
        requests.expectNone(url('one'));
    });

    it('does not accept a version from a failed GET', () => {
        load('one', '"v1"');
        service.getTemplate('', 'pages', createEntry({ type: 'pages' }), 'one').subscribe({ error: () => undefined });
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
        service.getTemplate('', 'pages', createEntry({ type: 'pages' }), 'one').subscribe();
        requests.expectOne(url('one')).flush(null);
        requests.expectOne('/fallback').flush(document);
        let saveError: any;
        service.saveGroupedPage('one', document).subscribe({ error: value => saveError = value });
        expect(saveError.status).toBe(428);
        requests.expectNone(url('one'));
    });
    it('does not accept a version when conversion returns no editor document', () => {
        load('one', '"v1"');
        service.getTemplate('', 'pages', createEntry({ type: 'pages' }), 'one').subscribe();
        requests.expectOne(url('one')).flush({ content: JSON.stringify({ unsupported: true }), eTag: '"v2"' });
        let error: HttpErrorResponse | undefined;
        service.saveGroupedPage('one', document).subscribe({ error: value => error = value });
        expect(error?.status).toBe(428);
        requests.expectNone(url('one'));
    });

    it('preserves literal template expressions in authored text', () => {
        load('one', '"v1"');
        const content = { settings: {}, content: [{ type: 'text', text: '{{config.secret}} @{{1+1}}' }] };
        service.saveGroupedPage('one', content).subscribe();
        const save = requests.expectOne(url('one'));
        expect(JSON.parse(save.request.body)).toEqual(content);
        save.flush({ eTag: '"v2"' }, { headers: { ETag: 'W/"rewritten"' } });
        service.saveGroupedPage('one', content).subscribe();
        const retry = requests.expectOne(url('one'));
        expect(retry.request.headers.get('If-Match')).toBe('"v2"');
        retry.flush({ eTag: '"v3"' });
    });

});
