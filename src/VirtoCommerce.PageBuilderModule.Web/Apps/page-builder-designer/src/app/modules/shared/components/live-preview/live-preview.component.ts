import { ChangeDetectionStrategy, Component, DestroyRef, ElementRef, computed, effect, signal, viewChild, inject } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Store } from '@ngrx/store';

import { EventsBusService } from '@core/services';
import { AppConfig, EnvironmentRef, SessionService } from '@integration/services';
import { AppInitializator } from '@integration/services/app.initializator';

import { BuilderState } from '@shared/store';
import * as fromRoute from '@shared/routing';
import { NgClass } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';

import { IconComponent } from '@core/components/icon/icon.component';
import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';
import { isUsablePreviewUrl } from './live-preview.utils';

@Component({
  selector: 'app-live-preview',
  templateUrl: './live-preview.component.html',
  styleUrls: ['./live-preview.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgClass, IconComponent, IconButtonComponent]
})
export class LivePreviewComponent {

  private readonly destroyRef = inject(DestroyRef);
  private readonly store = inject(Store<BuilderState>);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly eventsBus = inject(EventsBusService);
  private readonly config = inject(AppConfig);
  private readonly env = inject(EnvironmentRef);
  private readonly session = inject(SessionService);
  private readonly initializator = inject(AppInitializator);

  readonly frame = viewChild<ElementRef>('frame');

  private readonly previewLoaded = signal(false);
  private readonly pendingMessages: any[] = [];

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
    const sub = this.eventsBus.on(args => args.target === 'preview', msg => {
      if (msg.payload?.type === 'preview-loaded') {
        this.previewLoaded.set(true);
        this.pendingMessages.splice(0).forEach(x => this.doSend(x));
      }
      else {
        this.sendMessage(msg.payload);
      }
    });
    this.destroyRef.onDestroy(() => sub.unsubscribe());

    // a new address means a new document in the frame, which reports itself as loaded again
    effect(() => {
      this.url();
      this.previewLoaded.set(false);
    });
  }

  /** Resolves the store settings again, for example after the store URL has been filled in. */
  reload() {
    if (this.reloading()) {
      return;
    }
    this.reloading.set(true);
    this.initializator.init()
      .catch(error => console.warn('Failed to reload the configuration:', error))
      .finally(() => this.reloading.set(false));
  }

  private sendMessage(msg: any) {
    if (this.previewLoaded()) {
      this.doSend(msg);
    } else {
      this.pendingMessages.push(msg);
    }
  }

  private doSend(msg: any) {
    const frame = this.frame()?.nativeElement as HTMLIFrameElement | undefined;
    const url = this.url();
    if (!frame || !url) return;
    const message = { ...msg, source: 'builder' };
    if (message.type !== 'hover') {
      console.log(message);
    }
    frame.contentWindow?.postMessage(message, url);
  }
}
