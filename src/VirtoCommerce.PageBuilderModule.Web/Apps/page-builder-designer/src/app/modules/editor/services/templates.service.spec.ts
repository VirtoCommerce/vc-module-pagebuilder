import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AppConfig, BuilderHttpClient, EvaluatorService } from '@integration/services';

import { CookieService } from 'ngx-cookie-service';
import { EnvironmentRef } from '@integration/services/environment.ref';
import { createEntry, createSection, createTemplate } from '@app/testing';

import { TemplatesService } from './templates.service';
import settings from '../../../../data/settings.json';
import { ThemeSettingsService } from '@theme/services/theme-settings.service';

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
        vi.spyOn(TestBed.inject(AppConfig), 'getRawValueByEntryType').mockReturnValue([
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

    it('preserves literal expressions in theme template content', () => {
        config.initConfigWith({ saveTemplates: { ...settings.saveTemplates, url: '/theme/save' } });
        const text = '{{=1+1}} {{config.secret}} @{{1+1}}';
        service.saveTemplates([{ entry: createEntry({ path: 'page.json', type: 'pages' }), content: createTemplate({
            content: [createSection({ id: 'text', type: 'text', text })],
        }) }]).subscribe();
        const save = requests.expectOne('/theme/save');
        const files = JSON.parse(save.request.body.files);
        expect(files[0].content.content[0].text).toBe(text);
        save.flush(null);
    });

    it('preserves literal path and revision values in history requests', () => {
        config.initConfigWith({ history: {
            url: '/history?path={{path}}',
            restore: { url: '/restore/{{sha}}', method: 'POST' },
            preview: { url: '/preview/{{sha}}' },
        } });
        const literal = 'literal{{=1+1}}';
        service.getPageHistory(literal, 'pages', createEntry(), '', 'next').subscribe();
        requests.expectOne('/history?path=' + literal + '&after=next').flush({ items: [] });
        service.restoreVersion('', 'pages', createEntry(), '', literal).subscribe();
        requests.expectOne('/restore/' + literal).flush({ branch: 'branch', commitSha: literal });
        const open = vi.spyOn(window, 'open').mockReturnValue(null);
        service.previewVersion('', 'pages', createEntry(), '', literal);
        expect(open).toHaveBeenCalledWith('/preview/' + literal, '_blank');
        open.mockRestore();
    });

    it.each(['"v1"', '"v2"'])('compares the remote version without replacing the local document token: %s', remoteVersion => {
        load('one', '"v1"');
        let changed: boolean | undefined;
        service.hasPageChanged('', 'pages', createEntry({ type: 'pages' }), 'one').subscribe(value => changed = value);
        requests.expectOne(url('one')).flush({ content: JSON.stringify(document), eTag: remoteVersion });
        expect(changed).toBe(remoteVersion !== '"v1"');
        service.saveGroupedPage('one', document).subscribe();
        const save = requests.expectOne(url('one'));
        expect(save.request.headers.get('If-Match')).toBe('"v1"');
        save.flush({ eTag: '"v3"' });
    });

    it('discards a version probe if the document was saved while it was pending', () => {
        load('one', '"v1"');
        let changed: boolean | undefined;
        service.hasPageChanged('', 'pages', createEntry({ type: 'pages' }), 'one').subscribe(value => changed = value);
        const probe = requests.expectOne(url('one'));
        service.saveGroupedPage('one', document).subscribe();
        requests.expectOne(url('one')).flush({ eTag: '"v2"' });
        probe.flush({ content: JSON.stringify(document), eTag: '"old-probe"' });
        expect(changed).toBe(false);
    });

    it('reuses a changed probe document and accepts its version only on reload', () => {
        load('one', '"v1"');
        service.hasPageChanged('', 'pages', createEntry({ type: 'pages' }), 'one').subscribe();
        const remote = { settings: { name: 'Assistant' }, content: [] };
        requests.expectOne(url('one')).flush({ content: JSON.stringify(remote), eTag: '"v2"' });
        let loaded: any;
        service.getTemplate('', 'pages', createEntry({ type: 'pages' }), 'one', true).subscribe(value => loaded = value);
        requests.expectNone(url('one'));
        expect(loaded.settings.name).toBe('Assistant');
        service.saveGroupedPage('one', remote).subscribe();
        const save = requests.expectOne(url('one'));
        expect(save.request.headers.get('If-Match')).toBe('"v2"');
        save.flush({ eTag: '"v3"' });
    });

    it('does not reuse a probe after a save has taken ownership of the version', () => {
        load('one', '"v1"');
        service.hasPageChanged('', 'pages', createEntry({ type: 'pages' }), 'one').subscribe();
        requests.expectOne(url('one')).flush({ content: JSON.stringify(document), eTag: '"v2"' });
        service.saveGroupedPage('one', document).subscribe();
        requests.expectOne(url('one')).flush({ eTag: '"v3"' });
        let loaded: any;
        service.getTemplate('', 'pages', createEntry({ type: 'pages' }), 'one', true).subscribe(value => loaded = value);
        const latestDocument = { settings: { name: 'After save' }, content: [] };
        requests.expectOne(url('one')).flush({ content: JSON.stringify(latestDocument), eTag: '"v3"' });
        expect(loaded.settings.name).toBe('After save');
    });

    it('preserves literal request values across reads, probes and publishing', () => {
        const literal = 'page{{=1+1}}';
        const entry = createEntry({ type: 'pages', path: literal });
        config.initConfigWith({
            templateUrl: { pages: { url: '/read/{{path}}/{{item.path}}', versioned: true } },
            publishPages: { pages: Object.fromEntries(['status', 'publish', 'unpublish', 'promote'].map(action =>
                [action, { url: '/' + action + '/{{path}}/{{item.path}}' }])) },
        });
        service.getTemplate(literal, 'pages', entry, 'one').subscribe();
        requests.expectOne(`/read/${literal}/${literal}`).flush({ content: JSON.stringify(document), eTag: '"v1"' });
        service.hasPageChanged(literal, 'pages', entry, 'one').subscribe();
        requests.expectOne(`/read/${literal}/${literal}`).flush({ content: JSON.stringify(document), eTag: '"v1"' });
        for (const [action, method] of [
            ['status', 'getTemplatePublishStatus'], ['publish', 'publishTemplate'],
            ['unpublish', 'unpublishTemplate'], ['promote', 'promoteTemplate'],
        ] as const) {
            service[method](literal, 'pages', entry, 'one').subscribe();
            const request = requests.expectOne(`/${action}/${literal}/${literal}`);
            expect(request.request.body).toBeNull();
            request.flush({});
        }
    });

    it('evaluates theme settings once and preserves authored literal values', () => {
        config.initConfigWith({ saveSettings: { url: '/settings', method: 'POST', body: '{{item}}' } });
        const data = { current: { text: '{{=1+1}}' } };
        TestBed.inject(ThemeSettingsService).saveSettings(data as any).subscribe();
        const request = requests.expectOne('/settings');
        expect(JSON.parse(request.request.body)).toEqual(data);
        request.flush(true);
    });

    it('resolves sessionId lazily for raw save, history and restore descriptors', () => {
        const session = vi.spyOn(config, 'getCurrentSessionId').mockReturnValue('session-one');
        config.initConfigWith({
            saveTemplates: { url: '/save/{{sessionId}}', method: 'POST' },
            history: { url: '/history/{{sessionId}}', restore: { url: '/restore/{{sessionId}}' } },
        });
        expect(session).not.toHaveBeenCalled();
        service.saveTemplates([]).subscribe();
        requests.expectOne('/save/session-one').flush(null);
        session.mockReturnValue('session-two');
        service.getPageHistory('', 'pages', createEntry(), '').subscribe();
        requests.expectOne('/history/session-two').flush({});
        service.restoreVersion('', 'pages', createEntry(), '', 'sha').subscribe();
        requests.expectOne('/restore/session-two').flush({});
        expect(session).toHaveBeenCalled();
    });

});
