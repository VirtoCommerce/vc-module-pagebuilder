import { DestroyRef, Injectable, effect, inject } from '@angular/core';
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

  // The context last handed to the chat, or null while there is no chat to tell. The panel keeps the
  // iframe mounted while it is open, so the page changes under a live chat; without a re-send the agent
  // keeps acting on whatever was open at handshake time. Serialized rather than compared by reference:
  // the items are rebuilt on every route change, and re-sending an identical context is noise.
  private lastSentItems: string | null = null;

  private readonly ozOrigin: string | null = this.computeOzOrigin();

  constructor() {
    if (!this.ozOrigin) {
      return;
    }
    const handler = (event: MessageEvent) => this.handleIncoming(event);
    window.addEventListener('message', handler);
    this.destroyRef.onDestroy(() => window.removeEventListener('message', handler));

    effect(() => {
      const items = this.context.items();

      // no handshake yet — INIT_CONTEXT will carry the current items when it happens
      if (this.lastSentItems === null) {
        return;
      }

      const serialized = JSON.stringify(items);
      if (serialized === this.lastSentItems) {
        return;
      }

      this.lastSentItems = serialized;
      this.sendToIframe({ type: 'UPDATE_CONTEXT', payload: { contextType: 'list', items } });
    });
  }

  setIframe(el: HTMLIFrameElement | null) {
    this.iframe = el;
    if (!el) {
      this.pendingInit = false;
      // the chat is gone; the next one starts from a fresh INIT_CONTEXT
      this.lastSentItems = null;
      return;
    }
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
      return;
    }

    const data = event.data as OzChatMessage | null;
    if (!data || typeof data !== 'object' || typeof data.type !== 'string') return;

    // Once we have an iframe ref, drop messages that don't come from it
    // (defends against stale messages from a previous iframe instance).
    if (this.iframe && event.source !== this.iframe.contentWindow) {
      return;
    }

    this.dispatch(data);
  }

  private dispatch(message: OzChatMessage) {
    if (message.type === 'CHAT_READY') {
      if (this.iframe?.contentWindow) {
        this.sendInitContext();
      } else {
        this.pendingInit = true;
      }
    }
  }

  private sendInitContext() {
    const payload = this.context.buildInitPayload();
    // from here on the effect owns the context: it re-sends whenever the page changes
    this.lastSentItems = JSON.stringify(payload.items ?? []);
    this.sendToIframe({ type: 'INIT_CONTEXT', payload });
  }

  private sendToIframe(message: OzParentToChatMessage) {
    const target = this.iframe?.contentWindow;
    if (!target || !this.ozOrigin) {
      return;
    }
    target.postMessage(message, this.ozOrigin);
  }
}
