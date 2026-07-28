import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  effect,
  signal,
  viewChild,
  inject,
} from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Store } from '@ngrx/store';

import { EventsBusService } from '@core/services';

import { BuilderState } from '@shared/store';
import * as fromRoute from '@shared/routing';
import { NgClass } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { PreviewBridgeService } from '@shared/services';
import { isPreviewOutboundMessage } from '@shared/models';
import type { PreviewOutboundMessage } from '@shared/models';

@Component({
  selector: 'app-live-preview',
  templateUrl: './live-preview.component.html',
  styleUrls: ['./live-preview.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgClass],
})
export class LivePreviewComponent {
  private readonly destroyRef = inject(DestroyRef);
  private readonly store = inject(Store<BuilderState>);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly eventsBus = inject(EventsBusService);
  private readonly previewBridge = inject(PreviewBridgeService);

  readonly frame = viewChild<ElementRef<HTMLIFrameElement>>('frame');

  private readonly previewLoaded = signal(false);
  private readonly pendingMessages: PreviewOutboundMessage[] = [];
  private readonly registerPreviewFrame = effect((onCleanup) => {
    const frame = this.frame()?.nativeElement;
    if (!frame) {
      return;
    }

    this.previewBridge.registerFrame(frame);
    this.requestPreviewConnection();
    onCleanup(() => this.previewBridge.unregisterFrame(frame));
  });

  isPresetPreviewMode = toSignal(this.store.select(fromRoute.isPresetPreviewMode), { initialValue: false });
  previewPresetName = toSignal(this.store.select(fromRoute.selectPresetParameter), { initialValue: null });
  previewMode = toSignal(this.store.select(fromRoute.selectPreviewModeParameter), { initialValue: null });

  readonly previewUrl: SafeResourceUrl;
  readonly url: string;

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
    this.url = this.previewBridge.previewUrl;
    this.previewUrl = this.sanitizer.bypassSecurityTrustResourceUrl(this.url);
  }

  onPreviewFrameLoaded(frame: HTMLIFrameElement): void {
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
    this.previewBridge.send({ type: 'connect' });
  }

  private doSend(msg: PreviewOutboundMessage) {
    this.previewBridge.send(msg);
  }
}
