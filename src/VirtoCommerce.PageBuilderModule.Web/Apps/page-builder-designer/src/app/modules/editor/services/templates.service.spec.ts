import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Observable } from 'rxjs';
import { AppConfig, BuilderHttpClient, EvaluatorService } from '@integration/services';

import { CookieService } from 'ngx-cookie-service';
import { EnvironmentRef } from '@integration/services/environment.ref';
import { createEntry, createTemplate } from '@app/testing';

import { TemplatesService } from './templates.service';
import { ThemeSettingsService } from '@theme/services/theme-settings.service';

describe('TemplatesService', () => {
    let service: TemplatesService;
    let requests: HttpTestingController;
    let config: AppConfig;

    beforeEach(() => {
        TestBed.configureTestingModule({ providers: [
            provideHttpClient(), provideHttpClientTesting(), TemplatesService, BuilderHttpClient,
            AppConfig, EvaluatorService,
            { provide: EnvironmentRef, useValue: { nativeWindow: { location: window.location } } },
            { provide: CookieService, useValue: {} },
        ] });
        config = TestBed.inject(AppConfig);
        config.initConfigWith({
            saveTemplates: { url: '/theme/save', method: 'POST', body: { files: '{{=JSON.stringify(this.templatesToSave)}}' } },
            saveGroupedPage: { url: '/api/page-builder-pages/grouped/{{groupId}}/content', method: 'POST', body: '{{=JSON.stringify(this.content)}}' },
            saveSettings: { url: '/settings', method: 'POST', body: '{{item}}' },
            externalPreview: { url: '/designer-preview?pageId={{groupId}}' },
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

    function expectRefusalReported(save: () => Observable<unknown>, url: string): void {
        let saved = false;
        let error: any;
        save().subscribe({ next: () => saved = true, error: value => error = value });
        requests.expectOne(url).flush('Forbidden', { status: 403, statusText: 'Forbidden' });
        expect(saved).toBe(false);
        expect(error?.status).toBe(403);

        save().subscribe({ next: () => saved = true });
        requests.expectOne(url).flush(null);
        expect(saved).toBe(true);
    }

    it('reports a refused template save instead of completing it as saved', () => {
        const templates = [{ entry: createEntry({ path: 'page.page', type: 'pages' }), content: createTemplate() }];

        expectRefusalReported(() => service.saveTemplates(templates), '/theme/save');
    });

    it('reports a refused page save instead of completing it as saved', () => {
        expectRefusalReported(() => service.saveGroupedPage('one', { settings: {}, content: [] }), '/api/page-builder-pages/grouped/one/content');
    });

    it('reports a refused theme settings save instead of completing it as saved', () => {
        const themeSettings = TestBed.inject(ThemeSettingsService);

        expectRefusalReported(() => themeSettings.saveSettings({ current: {} } as any), '/settings');
    });

    it('opens the external preview without giving it a handle on the designer', () => {
        const open = vi.spyOn(window, 'open').mockReturnValue(null);
        service.externalPreview('', 'pages', createEntry(), 'one');
        expect(open).toHaveBeenCalledWith('/designer-preview?pageId=one', '_blank', 'noopener,noreferrer');
        open.mockRestore();
    });
});
