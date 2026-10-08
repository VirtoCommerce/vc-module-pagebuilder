import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideMockStore, MockStore } from '@ngrx/store/testing';
import { AppConfig } from '@integration/services';
import { refreshTemplateFromAssistant } from '@editor/store/actions';
import { OzContextService } from './oz-context.service';
import { OzAgentTransportService } from './oz-agent-transport.service';

describe('OzAgentTransportService save notifications', () => {
    let service: OzAgentTransportService;
    let dispatch: ReturnType<typeof vi.spyOn>;
    let iframe: HTMLIFrameElement;

    beforeEach(() => {
        TestBed.configureTestingModule({ providers: [
            provideMockStore(),
            { provide: AppConfig, useValue: { getValue: () => 'https://assistant.example/chat' } },
            { provide: OzContextService, useValue: { items: signal([]) } },
        ] });
        service = TestBed.inject(OzAgentTransportService);
        dispatch = vi.spyOn(TestBed.inject(MockStore), 'dispatch');
        iframe = document.createElement('iframe');
        document.body.appendChild(iframe);
        service.setIframe(iframe);
    });

    afterEach(() => {
        iframe.remove();
        TestBed.resetTestingModule();
    });

    it('refreshes the document only for the bound assistant frame and origin', () => {
        window.dispatchEvent(new MessageEvent('message', {
            origin: 'https://assistant.example', source: iframe.contentWindow, data: { type: 'RELOAD_BLADE' },
        }));
        expect(dispatch).toHaveBeenCalledExactlyOnceWith(refreshTemplateFromAssistant());
    });

    it('ignores other origins, windows, and detached frames', () => {
        window.dispatchEvent(new MessageEvent('message', {
            origin: 'https://unrelated.example', source: iframe.contentWindow, data: { type: 'RELOAD_BLADE' },
        }));
        window.dispatchEvent(new MessageEvent('message', {
            origin: 'https://assistant.example', source: window, data: { type: 'RELOAD_BLADE' },
        }));
        service.setIframe(null);
        window.dispatchEvent(new MessageEvent('message', {
            origin: 'https://assistant.example', source: iframe.contentWindow, data: { type: 'RELOAD_BLADE' },
        }));
        expect(dispatch).not.toHaveBeenCalled();
    });
});
