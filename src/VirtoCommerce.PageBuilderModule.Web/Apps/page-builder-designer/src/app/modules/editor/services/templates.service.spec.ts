import { TestBed } from '@angular/core/testing';
import { throwError } from 'rxjs';

import { AppConfig, BuilderHttpClient } from '@integration/services';

import { TemplatesService } from './templates.service';

describe('TemplatesService', () => {
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
});
