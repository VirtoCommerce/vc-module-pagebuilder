import { Injectable, inject } from '@angular/core';
import { Observable, filter, fromEvent, map, share } from 'rxjs';

import { AppConfig, EnvironmentRef } from '@integration/services';
import { isPreviewInboundMessage } from '@shared/models';
import type { PreviewInboundMessage, PreviewOutboundMessage } from '@shared/models';

@Injectable({
  providedIn: 'root',
})
export class PreviewBridgeService {
  private readonly appConfig = inject(AppConfig);
  private readonly nativeWindow = inject(EnvironmentRef).nativeWindow;
  private frame: HTMLIFrameElement | null = null;

  readonly messages$: Observable<PreviewInboundMessage> = fromEvent<MessageEvent<unknown>>(
    this.nativeWindow,
    'message',
  ).pipe(
    filter((event) => this.isFromCurrentPreview(event)),
    map((event) => event.data),
    filter(isPreviewInboundMessage),
    share(),
  );

  get previewUrl(): string {
    return this.getPreviewUrl();
  }

  get previewOrigin(): string {
    return new URL(this.previewUrl, this.nativeWindow.location.href).origin;
  }

  registerFrame(frame: HTMLIFrameElement): void {
    this.frame = frame;
  }

  unregisterFrame(frame: HTMLIFrameElement): void {
    if (this.frame === frame) {
      this.frame = null;
    }
  }

  send(message: PreviewOutboundMessage): void {
    this.frame?.contentWindow?.postMessage({ ...message, source: 'builder' }, this.previewOrigin);
  }

  private isFromCurrentPreview(event: MessageEvent<unknown>): boolean {
    const previewWindow = this.frame?.contentWindow;
    return previewWindow != null && event.source === previewWindow && event.origin === this.previewOrigin;
  }

  private getPreviewUrl(): string {
    const previewUrl = this.appConfig.getValue('fullPreviewUrl');
    if (typeof previewUrl !== 'string' || previewUrl.trim().length === 0) {
      throw new Error('The fullPreviewUrl setting must contain a valid preview URL.');
    }

    return previewUrl;
  }
}
