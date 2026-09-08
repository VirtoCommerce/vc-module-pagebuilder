import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  effect,
  signal,
  viewChild,
  inject,
} from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Store } from '@ngrx/store';

import { EventsBusService } from '@core/services';
import { AppConfig, BuilderHttpClient, EnvironmentRef, SessionService } from '@integration/services';
import { AppInitializator } from '@integration/services/app.initializator';

import { BuilderState } from '@shared/store';
import * as fromRoute from '@shared/routing';
import { NgClass } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { PreviewBridgeService } from '@shared/services';
import { isPreviewOutboundMessage } from '@shared/models';
import type { PreviewOutboundMessage } from '@shared/models';

import { IconComponent } from '@core/components/icon/icon.component';
import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';
import { isUsablePreviewUrl } from './live-preview.utils';

@Component({
  selector: 'app-live-preview',
  templateUrl: './live-preview.component.html',
  styleUrls: ['./live-preview.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgClass, IconComponent, IconButtonComponent],
})
export class LivePreviewComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly store = inject(Store<BuilderState>);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly eventsBus = inject(EventsBusService);
  private readonly previewBridge = inject(PreviewBridgeService);
  private readonly config = inject(AppConfig);
  private readonly env = inject(EnvironmentRef);
  private readonly session = inject(SessionService);
  private readonly initializator = inject(AppInitializator);
  private readonly http = inject(BuilderHttpClient);

  readonly frame = viewChild<ElementRef<HTMLIFrameElement>>('frame');

  private readonly previewLoaded = signal(false);
  private readonly pendingMessages: PreviewOutboundMessage[] = [];
  private readonly registerPreviewFrame = effect((onCleanup) => {
    const frame = this.frame()?.nativeElement;
    if (!frame) {
      return;
    }

    this.previewLoaded.set(false);
    this.previewBridge.registerFrame(frame);
    this.requestPreviewConnection();
    onCleanup(() => this.previewBridge.unregisterFrame(frame));
  });

  isPresetPreviewMode = toSignal(this.store.select(fromRoute.isPresetPreviewMode), { initialValue: false });
  previewPresetName = toSignal(this.store.select(fromRoute.selectPresetParameter), { initialValue: null });
  previewMode = toSignal(this.store.select(fromRoute.selectPreviewModeParameter), { initialValue: null });

  /** The storefront address to preview, or null when the store settings could not be resolved. */
  readonly url = computed<string | null>(() => {
    this.config.version(); // settings are resolved lazily, recompute once they are (re)loaded
    const value: unknown = this.config.getValue('fullPreviewUrl');
    return isUsablePreviewUrl(value, this.env.nativeWindow.location.href) ? value : null;
  });

  readonly previewUrl = computed<SafeResourceUrl | null>(() => {
    const url = this.url();
    return url ? this.sanitizer.bypassSecurityTrustResourceUrl(url) : null;
  });

  readonly sessionExpired = this.session.expired;
  readonly reloading = signal(false);

  constructor() {
    const sub = this.eventsBus.on(
      (args) => args.target === 'preview',
      (msg) => {
        if (!isPreviewOutboundMessage(msg.payload)) {
          return;
        }

        if (msg.payload.type === 'preview-loaded') {
          this.previewLoaded.set(true);
          this.pendingMessages.splice(0).forEach((x) => this.doSend(x));
        } else {
          this.sendMessage(msg.payload);
        }
      },
    );
    this.destroyRef.onDestroy(() => sub.unsubscribe());
    // A changed storefront address starts a new preview handshake.
    effect(() => {
      this.url();
      this.previewLoaded.set(false);
    });
  }

  /** Resolves the store settings again after the storefront address has been corrected. */
  reload() {
    if (this.reloading()) {
      return;
    }
    this.reloading.set(true);
    this.http.clearCache();
    this.initializator.init()
      .catch(error => console.warn('Failed to reload the configuration:', error))
      .finally(() => this.reloading.set(false));
  }

  onPreviewFrameLoaded(frame: HTMLIFrameElement): void {
    this.previewLoaded.set(false);
    this.previewBridge.registerFrame(frame);
    this.requestPreviewConnection();
  }

  private sendMessage(msg: PreviewOutboundMessage) {
    if (this.previewLoaded()) {
      this.doSend(msg);
    } else {
      this.pendingMessages.push(msg);
      this.requestPreviewConnection();
    }
  }

  private requestPreviewConnection(): void {
    this.doSend({ type: 'connect' });
  }

  private doSend(msg: PreviewOutboundMessage) {
    if (this.url()) {
      this.previewBridge.send(msg);
    }
  }
}
