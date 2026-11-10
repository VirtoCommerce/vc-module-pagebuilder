import { onBeforeUnmount, ref, shallowRef, type Ref } from "vue";
import {
  PREVIEW_PROTOCOL,
  parsePreviewMessage,
  storefrontPath,
  type Capability,
  type DesignerMessage,
  type Loaded,
  type Resolved,
  type TextEdit,
} from "./previewMessages";
import { clone } from "./json";

export interface PreviewChannelOptions {
  post: (message: DesignerMessage) => void;
  onLoaded: (loaded: Loaded) => void;
  prepare?: (loaded: Loaded) => Promise<void>;
  handshake: () => DesignerMessage[];
  onSelect?: (event: { sectionId: string; blockId?: string }) => void;
  onHover?: (sectionId: string | null) => void;
  onEdit?: (edit: TextEdit) => void;
  onResolved?: (resolved: Resolved) => void;
}

/**
 * On every `loaded` the catalog step runs and then the handshake goes out. Nothing else is sent until then, because
 * the handshake carries the state as it is when it goes.
 */
export function createPreviewChannel(options: PreviewChannelOptions) {
  const connected = ref(false);
  const unsupported = ref(false);
  const loaded = shallowRef<Loaded>();
  // A newer frame load or `loaded` makes an unfinished handshake stale.
  let generation = 0;

  const can = (capability: Capability) => !!loaded.value?.capabilities.includes(capability);

  function send(message: DesignerMessage) {
    if (connected.value) options.post(message);
  }

  function frameLoaded() {
    generation++;
    connected.value = false;
    unsupported.value = false;
    loaded.value = undefined;
    options.post({ type: "connect", protocol: PREVIEW_PROTOCOL });
  }

  async function handshake(next: Loaded | "unsupported") {
    const at = ++generation;
    connected.value = false;
    if (next === "unsupported") {
      loaded.value = undefined;
      unsupported.value = true;
      return;
    }
    unsupported.value = false;
    loaded.value = next;
    options.onLoaded(next);
    try {
      await options.prepare?.(next);
    } catch (e) {
      console.warn("[page-builder] The preview's handshake goes out without its preparation", e);
    }
    if (at !== generation) return;
    options.handshake().forEach(options.post);
    connected.value = true;
  }

  async function receive(data: unknown) {
    const event = parsePreviewMessage(data);
    if (!event) return;
    if (event.type === "loaded") return handshake(event.loaded);
    if (!loaded.value) return;
    if (event.type === "select") options.onSelect?.({ sectionId: event.sectionId, ...(event.blockId ? { blockId: event.blockId } : {}) });
    else if (event.type === "hover") options.onHover?.(event.sectionId);
    else if (event.type === "edit") {
      const { type: _type, ...edit } = event;
      if (can("inline-edit")) options.onEdit?.(edit);
    } else options.onResolved?.(event.resolved);
  }

  return { connected, unsupported, loaded, can, send, frameLoaded, receive };
}

export function usePreviewBridge(options: Omit<PreviewChannelOptions, "post"> & { frame: Ref<HTMLIFrameElement | undefined>; url: Ref<string | undefined> }) {
  const origin = () => {
    try {
      return options.url.value ? new URL(options.url.value).origin : undefined;
    } catch {
      return undefined;
    }
  };

  const channel = createPreviewChannel({
    ...options,
    post: (message) => {
      const target = options.frame.value?.contentWindow;
      const targetOrigin = origin();
      // Plain data, because the document is a reactive proxy, which postMessage cannot clone.
      if (target && targetOrigin) target.postMessage(clone({ ...message, source: "builder" }), targetOrigin);
    },
  });

  function onMessage(event: MessageEvent) {
    if (event.source !== options.frame.value?.contentWindow || event.origin !== origin()) return;
    void channel.receive(event.data);
  }

  window.addEventListener("message", onMessage);
  onBeforeUnmount(() => window.removeEventListener("message", onMessage));

  return { connected: channel.connected, unsupported: channel.unsupported, send: channel.send, onFrameLoad: channel.frameLoaded };
}

export function storefrontPage(storefrontUrl: string | undefined, path: string): URL | undefined {
  if (!storefrontUrl) return undefined;
  try {
    const url = new URL(storefrontUrl);
    if (url.protocol !== "http:" && url.protocol !== "https:") return undefined;
    const page = storefrontPath(path);
    url.pathname = url.pathname.replace(/\/+$/, "") + page.pathname;
    url.search = page.search;
    url.hash = page.hash;
    return url;
  } catch {
    return undefined;
  }
}

/** The storefront's preview path must be exactly /designer-preview. */
export function previewUrl(storefrontUrl: string | undefined, previewPath = "/designer-preview"): string | undefined {
  const url = storefrontPage(storefrontUrl, previewPath);
  url?.searchParams.set("ep", window.location.origin);
  url?.searchParams.set("protocol", String(PREVIEW_PROTOCOL));
  return url?.toString();
}
