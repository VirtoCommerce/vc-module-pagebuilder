import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';

import { AppConfig, EnvironmentRef } from '@integration/services';

import { PreviewBridgeService } from './preview-bridge.service';

describe('PreviewBridgeService', () => {
  const previewUrl = 'https://store.example/designer-preview?ep=https://builder.example';
  const previewOrigin = 'https://store.example';

  let service: PreviewBridgeService;
  let previewWindow: Window;
  let otherWindow: Window;
  let frame: HTMLIFrameElement;

  beforeEach(() => {
    previewWindow = { postMessage: vi.fn() } as unknown as Window;
    otherWindow = { postMessage: vi.fn() } as unknown as Window;
    frame = { contentWindow: previewWindow } as unknown as HTMLIFrameElement;

    TestBed.configureTestingModule({
      providers: [
        PreviewBridgeService,
        { provide: AppConfig, useValue: { getValue: vi.fn().mockReturnValue(previewUrl) } },
        { provide: EnvironmentRef, useValue: { nativeWindow: window } },
      ],
    });

    service = TestBed.inject(PreviewBridgeService);
    service.registerFrame(frame);
  });

  it('accepts a select message from the configured preview frame', async () => {
    const result = firstValueFrom(service.messages$);

    dispatchPreviewMessage({
      source: 'preview',
      type: 'select',
      data: { sectionId: 'placement-1' },
    });

    await expect(result).resolves.toEqual({
      source: 'preview',
      type: 'select',
      data: { sectionId: 'placement-1' },
    });
  });

  it('accepts hover leave from the configured preview frame', async () => {
    const result = firstValueFrom(service.messages$);

    dispatchPreviewMessage({
      source: 'preview',
      type: 'hover',
      data: { sectionId: null },
    });

    await expect(result).resolves.toEqual({
      source: 'preview',
      type: 'hover',
      data: { sectionId: null },
    });
  });

  it.each([
    null,
    { source: 'preview', type: 'select' },
    { source: 'preview', type: 'select', data: { sectionId: 42 } },
    { source: 'preview', type: 'hover', data: {} },
    { source: 'preview', type: 'loaded', data: 'invalid schemas' },
    { source: 'preview', type: 'unknown', data: { sectionId: 's1' } },
  ])('rejects malformed preview data: %o', (data) => {
    const received = vi.fn();
    const subscription = service.messages$.subscribe(received);

    dispatchPreviewMessage(data);

    expect(received).not.toHaveBeenCalled();
    subscription.unsubscribe();
  });

  it('rejects a message from a different origin', () => {
    const received = vi.fn();
    const subscription = service.messages$.subscribe(received);

    dispatchPreviewMessage(
      { source: 'preview', type: 'select', data: { sectionId: 's1' } },
      'https://attacker.example',
    );

    expect(received).not.toHaveBeenCalled();
    subscription.unsubscribe();
  });

  it('rejects a message from a different window on the same origin', () => {
    const received = vi.fn();
    const subscription = service.messages$.subscribe(received);

    dispatchPreviewMessage(
      { source: 'preview', type: 'select', data: { sectionId: 's1' } },
      previewOrigin,
      otherWindow,
    );

    expect(received).not.toHaveBeenCalled();
    subscription.unsubscribe();
  });

  it('sends builder messages only to the registered frame and configured origin', () => {
    service.send({ type: 'hover', sectionId: 'placement-1' });

    expect(previewWindow.postMessage).toHaveBeenCalledWith(
      { source: 'builder', type: 'hover', sectionId: 'placement-1' },
      previewOrigin,
    );
  });

  function dispatchPreviewMessage(data: unknown, origin = previewOrigin, source = previewWindow): void {
    window.dispatchEvent(
      new MessageEvent('message', {
        data,
        origin,
        source,
      }),
    );
  }
});
