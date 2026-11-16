import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { AppConfig, BuilderHttpClient } from '@integration/services';

import { ThemeSettingsService } from './theme-settings.service';

function setup(response: unknown) {
    const request = { method: 'POST', url: '/settings' };
    const http = {
        generateRequest: vi.fn().mockReturnValue(request),
        doRequest: vi.fn().mockReturnValue(response),
    };

    TestBed.configureTestingModule({
        providers: [
            ThemeSettingsService,
            { provide: BuilderHttpClient, useValue: http },
            { provide: AppConfig, useValue: { getValue: vi.fn().mockReturnValue(request) } },
        ],
    });

    return { service: TestBed.inject(ThemeSettingsService), http, request };
}

describe('ThemeSettingsService', () => {
    afterEach(() => {
        TestBed.resetTestingModule();
    });

    it('reports a refused theme settings save instead of completing it as saved', () => {
        const { service, http, request } = setup(throwError(() => ({ status: 403 })));
        let saved = false;
        let receivedError: any;

        service.saveSettings({ current: {} } as any).subscribe({ next: () => saved = true, error: (value) => receivedError = value });

        expect(saved).toBe(false);
        expect(receivedError?.status).toBe(403);
        expect(http.doRequest).toHaveBeenCalledWith(request, { nullWhenError: false });
    });

    it('completes a save that the server accepts with an empty body', () => {
        const { service } = setup(of(null));
        let saved = false;

        service.saveSettings({ current: {} } as any).subscribe({ next: () => saved = true });

        expect(saved).toBe(true);
    });
});
