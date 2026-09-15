import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { CookieService } from 'ngx-cookie-service';
import { AppConfig, BuilderHttpClient, EnvironmentRef, SessionService } from '@integration/services';
import { AppInitializator } from '@integration/services/app.initializator';
import { EvaluatorService } from '@integration/services/evaluator.service';
import { provideMockStore } from '@ngrx/store/testing';
import { Subscription } from 'rxjs';

import type { EventBusArgs } from '@core/models';
import { EventsBusService } from '@core/services';
import * as fromRoute from '@shared/routing';
import { PreviewBridgeService } from '@shared/services';

import { LivePreviewComponent } from './live-preview.component';

describe('LivePreviewComponent bridge', () => {
  const previewUrl = 'https://store.example/designer-preview';
  const configuredUrl = signal(previewUrl);

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
    configuredUrl.set(previewUrl);
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
        { provide: AppConfig, useValue: { version: signal(0), getValue: () => configuredUrl() } },
        { provide: EnvironmentRef, useValue: { nativeWindow: { location: { href: 'https://admin.example/designer/' } } } },
        { provide: SessionService, useValue: { expired: signal(false) } },
        { provide: AppInitializator, useValue: { init: vi.fn().mockResolvedValue(undefined) } },
        { provide: BuilderHttpClient, useValue: { clearCache: vi.fn() } },
      ],
    });
  });

  it('registers the rendered iframe with the preview bridge', async () => {
    const fixture = TestBed.createComponent(LivePreviewComponent);
    await fixture.whenStable();
    const frame = fixture.nativeElement.querySelector('iframe') as HTMLIFrameElement;

    expect(previewBridge.registerFrame).toHaveBeenCalledWith(frame);
    expect(previewBridge.send).toHaveBeenCalledWith({ type: 'connect' });
    expect(frame.title).toBe('Storefront page preview');

    fixture.destroy();
    expect(previewBridge.unregisterFrame).toHaveBeenCalledWith(frame);
  });

  it('requests a fresh readiness announcement whenever the iframe loads', async () => {
    const fixture = TestBed.createComponent(LivePreviewComponent);
    await fixture.whenStable();
    const frame = fixture.nativeElement.querySelector('iframe') as HTMLIFrameElement;
    const pageMessage = { type: 'page', template: { content: [], settings: {} } };
    sendToPreview({ type: 'preview-loaded' });
    previewBridge.send.mockClear();

    frame.dispatchEvent(new Event('load'));
    sendToPreview(pageMessage);
    await fixture.whenStable();

    expect(previewBridge.registerFrame).toHaveBeenLastCalledWith(frame);
    expect(previewBridge.send).toHaveBeenCalledWith({ type: 'connect' });
    expect(previewBridge.send).not.toHaveBeenCalledWith(pageMessage);

    sendToPreview({ type: 'preview-loaded' });
    expect(previewBridge.send).toHaveBeenCalledWith(pageMessage);
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

  it('removes an unavailable preview and reconnects the bridge when its address recovers', async () => {
    const fixture = TestBed.createComponent(LivePreviewComponent);
    await fixture.whenStable();
    const oldFrame = fixture.nativeElement.querySelector('iframe') as HTMLIFrameElement;
    sendToPreview({ type: 'preview-loaded' });

    configuredUrl.set('');
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('iframe')).toBeNull();
    expect(previewBridge.unregisterFrame).toHaveBeenCalledWith(oldFrame);
    expect(fixture.nativeElement.textContent).toContain('Preview is not available');

    previewBridge.send.mockClear();
    const pageMessage = { type: 'page', template: { content: [], settings: {} } };
    sendToPreview(pageMessage);
    expect(previewBridge.send).not.toHaveBeenCalled();

    configuredUrl.set('https://new-store.example/designer-preview');
    await fixture.whenStable();
    const newFrame = fixture.nativeElement.querySelector('iframe') as HTMLIFrameElement;
    expect(newFrame.src).toBe('https://new-store.example/designer-preview');
    expect(previewBridge.registerFrame).toHaveBeenLastCalledWith(newFrame);
    expect(previewBridge.send).toHaveBeenCalledWith({ type: 'connect' });
    expect(previewBridge.send).not.toHaveBeenCalledWith(pageMessage);

    sendToPreview({ type: 'preview-loaded' });
    expect(previewBridge.send).toHaveBeenCalledWith(pageMessage);
  });
});

const STORAGE_KEY = 'ls.authenticationData';

// Only the two settings the preview address is built from. Built fresh for every response:
// the initializator resolves the `init` descriptors in place, so a shared object would only
// carry them on the first load.
const configResponse = () => ({
    storefrontUrl: {
        init: 'requests',
        requests: [{
            url: '/api/stores/{{location.params.storeId}}',
            method: 'GET',
            cacheable: true,
            response: { result: '$.url', isArray: false }
        }]
    },
    fullPreviewUrl: '{{settings.storefrontUrl}}'
});

describe('LivePreviewComponent configuration', () => {
    let httpController: HttpTestingController;
    let initializator: AppInitializator;

    beforeEach(() => {
        // a valid token keeps init() from trading the cookie session for a new one
        localStorage.setItem(STORAGE_KEY, JSON.stringify({ token: 'test-token', expiresAt: Date.now() + 60000 }));

        TestBed.configureTestingModule({
            providers: [
                provideHttpClient(),
                provideHttpClientTesting(),
                provideMockStore({}),
                {
                    provide: EnvironmentRef,
                    useValue: {
                        nativeWindow: {
                            location: { search: '?storeId=store1', href: 'https://admin.local/apps/pagebuilder/' }
                        }
                    }
                },
                { provide: CookieService, useValue: {} },
                { provide: PreviewBridgeService, useValue: { registerFrame: vi.fn(), unregisterFrame: vi.fn(), send: vi.fn() } },
                EvaluatorService,
                AppConfig
            ]
        });

        // the preview frame and its toolbar are irrelevant here, the resolved address is what matters
        TestBed.overrideComponent(LivePreviewComponent, { set: { template: '', imports: [] } });

        httpController = TestBed.inject(HttpTestingController);
        initializator = TestBed.inject(AppInitializator);
    });

    afterEach(() => {
        localStorage.removeItem(STORAGE_KEY);
    });

    it('picks up a store URL corrected in the Manager when the preview is retried', () => {
        const fixture = TestBed.createComponent(LivePreviewComponent);
        const component = fixture.componentInstance;

        initializator.init();
        httpController.expectOne('data/settings.json').flush(configResponse());
        // the store has no usable address yet, so the preview cannot be shown
        httpController.expectOne('/api/stores/store1').flush({ url: '' });
        httpController.expectOne(request => request.url.startsWith('/api/pagebuilder/settings')).flush({});

        expect(component.url()).toBeNull();

        // the URL is filled in on the platform and the user presses Retry
        component.reload();

        httpController.expectOne('data/settings.json').flush(configResponse());
        // without dropping the cached store response the old address would be resolved again
        httpController.expectOne('/api/stores/store1').flush({ url: 'https://storefront.local' });
        httpController.expectOne(request => request.url.startsWith('/api/pagebuilder/settings')).flush({});

        expect(component.url()).toBe('https://storefront.local');
    });
});
