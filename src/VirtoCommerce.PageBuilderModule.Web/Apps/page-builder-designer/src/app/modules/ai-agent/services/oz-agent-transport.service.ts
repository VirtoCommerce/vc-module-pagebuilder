import { DestroyRef, Injectable, inject } from '@angular/core';
import { AppConfig } from '@integration/services';
import type { OzChatMessage, OzParentToChatMessage } from '../types';
import { OzContextService } from './oz-context.service';

@Injectable({ providedIn: 'root' })
export class OzAgentTransportService {

  private readonly destroyRef = inject(DestroyRef);
  private readonly config = inject(AppConfig);
  private readonly context = inject(OzContextService);

  private iframe: HTMLIFrameElement | null = null;
  // Set when CHAT_READY arrives before the iframe ref is available;
  // flushed as soon as setIframe() gets called.
  private pendingInit = false;

  private readonly ozOrigin: string | null = this.computeOzOrigin();

  constructor() {
    if (!this.ozOrigin) {
      console.log('[oz-agent-transport] ozAgentUrl not configured — transport inert');
      return;
    }
    const handler = (event: MessageEvent) => this.handleIncoming(event);
    window.addEventListener('message', handler);
    this.destroyRef.onDestroy(() => window.removeEventListener('message', handler));
    console.log('[oz-agent-transport] listener registered, ozOrigin:', this.ozOrigin);
  }

  setIframe(el: HTMLIFrameElement | null) {
    this.iframe = el;
    if (!el) {
      console.log('[oz-agent-transport] iframe ref cleared');
      this.pendingInit = false;
      return;
    }
    console.log('[oz-agent-transport] iframe ref received, pendingInit:', this.pendingInit);
    if (this.pendingInit) {
      this.pendingInit = false;
      this.sendInitContext();
    }
  }

  private computeOzOrigin(): string | null {
    const url = this.config.getValue('ozAgentUrl') as string | undefined;
    if (!url) return null;
    try {
      return new URL(url).origin;
    } catch {
      return null;
    }
  }

  private handleIncoming(event: MessageEvent) {
    if (event.origin !== this.ozOrigin) {
      // Don't log every message — page is noisy with BroadcastChannel traffic.
      // Only surface events that look like ours but come from the wrong origin.
      const data = event.data as { type?: unknown } | null;
      if (data && typeof data === 'object' && typeof data.type === 'string' && data.type.startsWith('CHAT_')) {
        console.warn('[oz-agent-transport] dropped OZ-like message from unexpected origin:', event.origin, 'expected:', this.ozOrigin);
      }
      return;
    }

    const data = event.data as OzChatMessage | null;
    if (!data || typeof data !== 'object' || typeof data.type !== 'string') return;

    console.log('[oz-agent-transport] incoming', data.type, { origin: event.origin, sourceMatch: !this.iframe || event.source === this.iframe.contentWindow });

    // Once we have an iframe ref, drop messages that don't come from it
    // (defends against stale messages from a previous iframe instance).
    if (this.iframe && event.source !== this.iframe.contentWindow) {
      console.warn('[oz-agent-transport] dropped — event.source does not match iframe.contentWindow');
      return;
    }

    this.dispatch(data);
  }

  private dispatch(message: OzChatMessage) {
    if (message.type === 'CHAT_READY') {
      if (this.iframe?.contentWindow) {
        console.log('[oz-agent-transport] CHAT_READY → sending INIT_CONTEXT immediately');
        this.sendInitContext();
      } else {
        console.log('[oz-agent-transport] CHAT_READY arrived before iframe ref — buffering');
        this.pendingInit = true;
      }
      return;
    }
    console.log('[oz-agent-transport] message from OZ', message.type, message.payload);
  }

  private sendInitContext() {
    const payload = this.context.buildInitPayload();
    console.log('[oz-agent-transport] sending INIT_CONTEXT', payload);
    this.sendToIframe({ type: 'INIT_CONTEXT', payload });
  }

  private sendToIframe(message: OzParentToChatMessage) {
    const target = this.iframe?.contentWindow;
    if (!target || !this.ozOrigin) {
      console.warn('[oz-agent-transport] sendToIframe skipped — target:', !!target, 'origin:', this.ozOrigin);
      return;
    }
    target.postMessage(message, this.ozOrigin);
  }
}
