import { TestBed } from '@angular/core/testing';
import { throwError } from 'rxjs';

import { AppConfig, BuilderHttpClient } from '@integration/services';

import { TemplatesService } from './templates.service';

function setup(request: unknown, response = throwError(() => ({ status: 403 }))) {
    const http = {
        generateRequest: vi.fn().mockReturnValue(request),
        doRequest: vi.fn().mockReturnValue(response),
    };

    TestBed.configureTestingModule({
        providers: [
            TemplatesService,
            { provide: BuilderHttpClient, useValue: http },
            { provide: AppConfig, useValue: { getValue: vi.fn().mockReturnValue(request) } },
        ],
    });

    return { service: TestBed.inject(TemplatesService), http };
}

describe('TemplatesService', () => {
    afterEach(() => {
        TestBed.resetTestingModule();
        vi.restoreAllMocks();
    });

    it('propagates grouped-page save errors', () => {
        const request = { method: 'POST', url: '/api/page-builder-pages/grouped/group-1/content' };
        const error = new Error('Shared Component was not found.');
        const http = {
            generateRequest: vi.fn().mockReturnValue(request),
            doRequest: vi.fn().mockReturnValue(throwError(() => error)),
        };

        TestBed.configureTestingModule({
            providers: [
                TemplatesService,
                { provide: BuilderHttpClient, useValue: http },
                {
                    provide: AppConfig,
                    useValue: { getValue: vi.fn().mockReturnValue(request) },
                },
            ],
        });

        let receivedError: unknown;
        TestBed.inject(TemplatesService).saveGroupedPage('group-1', {}).subscribe({
            error: (value) => {
                receivedError = value;
            },
        });

        expect(receivedError).toBe(error);
        expect(http.doRequest).toHaveBeenCalledWith(request, { nullWhenError: false }, null);
    });

    it('reports a refused theme template save instead of completing it as saved', () => {
        const { service, http } = setup({ method: 'POST', url: '/theme/save' });
        let saved = false;
        let receivedError: any;

        service.saveTemplates([]).subscribe({ next: () => saved = true, error: (value) => receivedError = value });

        expect(saved).toBe(false);
        expect(receivedError?.status).toBe(403);
        expect(http.doRequest).toHaveBeenCalledWith({ method: 'POST', url: '/theme/save' }, { nullWhenError: false }, null);
    });

    it('opens the external preview without giving it a handle on the designer', () => {
        const { service } = setup({ url: '/designer-preview?pageId=one' });
        const open = vi.spyOn(window, 'open').mockReturnValue(null);

        service.externalPreview('', 'pages', {} as any, 'one');

        expect(open).toHaveBeenCalledWith('/designer-preview?pageId=one', '_blank', 'noopener,noreferrer');
    });

    it('opens a version preview without giving it a handle on the designer', () => {
        const { service } = setup({ preview: { url: '/preview/sha' } });
        const open = vi.spyOn(window, 'open').mockReturnValue(null);

        service.previewVersion('', 'pages', {} as any, '', 'sha');

        expect(open).toHaveBeenCalledWith('/preview/sha', '_blank', 'noopener,noreferrer');
    });
});
