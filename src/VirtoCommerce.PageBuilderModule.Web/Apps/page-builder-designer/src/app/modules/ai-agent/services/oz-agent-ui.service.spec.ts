import { TestBed } from '@angular/core/testing';
import { Store } from '@ngrx/store';
import { AppConfig } from '@integration/services';
import { LocalStorageService } from '@core/store/local-storage.service';
import { refreshTemplateFromAssistant } from '@editor/store/actions';
import { OzAgentUiService } from './oz-agent-ui.service';

describe('OzAgentUiService', () => {
    afterEach(() => TestBed.resetTestingModule());

    it('requests a version check only when an open panel closes, including through toggle', () => {
        const dispatch = vi.fn();
        TestBed.configureTestingModule({ providers: [
            { provide: Store, useValue: { dispatch } },
            { provide: AppConfig, useValue: { getValue: () => 'https://assistant.example' } },
            { provide: LocalStorageService, useValue: { getItem: () => null, setItem: vi.fn() } },
        ] });
        const ui = TestBed.inject(OzAgentUiService);
        ui.close();
        expect(dispatch).not.toHaveBeenCalled();
        ui.open();
        ui.close();
        ui.close();
        expect(dispatch).toHaveBeenCalledExactlyOnceWith(refreshTemplateFromAssistant());
        ui.toggle();
        ui.toggle();
        expect(dispatch).toHaveBeenCalledTimes(2);
        expect(ui.isOpen()).toBe(false);
    });
});
