import { TestBed } from '@angular/core/testing';
import { provideMockStore } from '@ngrx/store/testing';
import { Subscription } from 'rxjs';

import type { EventBusArgs } from '@core/models';
import { EventsBusService } from '@core/services';
import * as fromRoute from '@shared/routing';
import { PreviewBridgeService } from '@shared/services';

import { LivePreviewComponent } from './live-preview.component';

describe('LivePreviewComponent', () => {
  const previewUrl = 'https://store.example/designer-preview';

  let sendToPreview: (payload: unknown) => void = () => {
    throw new Error('The preview event handler is not registered.');
  };
  let previewBridge: {
    previewUrl: string;
    registerFrame: ReturnType<typeof vi.fn>;
    unregisterFrame: ReturnType<typeof vi.fn>;
    send: ReturnType<typeof vi.fn>;
  };

  beforeEach(() => {
    sendToPreview = () => {
      throw new Error('The preview event handler is not registered.');
    };
    previewBridge = {
      previewUrl,
      registerFrame: vi.fn(),
      unregisterFrame: vi.fn(),
      send: vi.fn(),
    };

    const eventsBus = {
      on: vi.fn((matcher: (event: EventBusArgs) => boolean, handler: (event: EventBusArgs) => void): Subscription => {
        sendToPreview = (payload: unknown) => {
          const event: EventBusArgs = { target: 'preview', payload };
          if (matcher(event)) {
            handler(event);
          }
        };
        return new Subscription();
      }),
    };

    TestBed.configureTestingModule({
      imports: [LivePreviewComponent],
      providers: [
        provideMockStore({
          selectors: [
            { selector: fromRoute.isPresetPreviewMode, value: false },
            { selector: fromRoute.selectPresetParameter, value: null },
            { selector: fromRoute.selectPreviewModeParameter, value: null },
          ],
        }),
        { provide: EventsBusService, useValue: eventsBus },
        { provide: PreviewBridgeService, useValue: previewBridge },
      ],
    });
  });

  it('registers the rendered iframe with the preview bridge', async () => {
    const fixture = TestBed.createComponent(LivePreviewComponent);
    await fixture.whenStable();
    const frame = fixture.nativeElement.querySelector('iframe') as HTMLIFrameElement;

    expect(previewBridge.registerFrame).toHaveBeenCalledWith(frame);
    expect(previewBridge.send).toHaveBeenCalledWith({ type: 'connect' });

    fixture.destroy();
    expect(previewBridge.unregisterFrame).toHaveBeenCalledWith(frame);
  });

  it('requests a fresh readiness announcement whenever the iframe loads', async () => {
    const fixture = TestBed.createComponent(LivePreviewComponent);
    await fixture.whenStable();
    const frame = fixture.nativeElement.querySelector('iframe') as HTMLIFrameElement;
    previewBridge.send.mockClear();

    frame.dispatchEvent(new Event('load'));
    await fixture.whenStable();

    expect(previewBridge.registerFrame).toHaveBeenLastCalledWith(frame);
    expect(previewBridge.send).toHaveBeenCalledWith({ type: 'connect' });
  });

  it('queues typed outbound messages until preview-loaded and ignores malformed payloads', async () => {
    const fixture = TestBed.createComponent(LivePreviewComponent);
    await fixture.whenStable();
    const pageMessage = { type: 'page', template: { content: [], settings: {} } };
    previewBridge.send.mockClear();

    sendToPreview({ template: {} });
    sendToPreview(pageMessage);

    expect(previewBridge.send).toHaveBeenCalledTimes(1);
    expect(previewBridge.send).toHaveBeenCalledWith({ type: 'connect' });
    expect(previewBridge.send).not.toHaveBeenCalledWith(pageMessage);

    sendToPreview({ type: 'preview-loaded' });

    expect(previewBridge.send).toHaveBeenCalledTimes(2);
    expect(previewBridge.send).toHaveBeenCalledWith(pageMessage);
  });
});
