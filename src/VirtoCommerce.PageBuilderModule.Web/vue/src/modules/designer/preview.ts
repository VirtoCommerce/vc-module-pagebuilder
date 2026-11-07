import { onBeforeUnmount, ref, shallowRef, type Ref } from "vue";
import {
  PREVIEW_PROTOCOL,
  parsePreviewMessage,
  type Capability,
  type DesignerMessage,
  type Loaded,
  type Resolved,
  type TextEdit,
} from "./previewMessages";

export interface PreviewChannelOptions {
  /** posts a message to the preview, `source: "builder"` added */
  post: (message: DesignerMessage) => void;
  /** a storefront speaking protocol 2 announced itself: its schemas, what it can do, its rule conditions */
  onLoaded: (loaded: Loaded) => void;
  /** what must be done before the handshake goes out (the catalog's upload); never throws */
  prepare?: (loaded: Loaded) => Promise<void>;
  /** what goes out after `loaded`, built when it goes out */
  handshake: () => DesignerMessage[];
  onSelect?: (event: { sectionId: string; blockId?: string }) => void;
  onHover?: (sectionId: string | null) => void;
  /** a text edited on the page: the string at the path of that section or block, in `cultureName` for a translatable one */
  onEdit?: (edit: TextEdit) => void;
  onResolved?: (resolved: Resolved) => void;
}

/**
 * The designer's side of preview protocol 2 (docs/contracts/preview-protocol.md), without the frame:
 *  - on every frame load the designer sends `connect` with the protocol; the preview answers `loaded`, also unasked;
 *  - a `loaded` without the protocol is a storefront that can't be designed here: nothing more is sent to it;
 *  - on every `loaded` the catalog step runs, then the handshake goes out in order; until then nothing else is sent,
 *    as the handshake carries the state as it is when it goes;
 *  - `select`, `hover`, `edit` and `resolved` count only from a storefront that completed `loaded`.
 */
export function createPreviewChannel(options: PreviewChannelOptions) {
  const connected = ref(false);
  /** the storefront answered without protocol 2 */
  const unsupported = ref(false);
  const loaded = shallowRef<Loaded>();
  // a newer frame load or `loaded` makes an unfinished handshake stale
  let generation = 0;

  const can = (capability: Capability) => !!loaded.value?.capabilities.includes(capability);

  function send(message: DesignerMessage) {
    if (connected.value) options.post(message);
  }

  /** The frame (re)loaded: a new document, so a new handshake */
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

  /** A message from the preview's window */
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

/**
 * The storefront's /designer-preview page in an iframe, driven over postMessage: the frame's URL carries
 * ?ep=<this origin>; messages go to the storefront's origin only, and only its frame's messages are taken.
 */
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
      // plain data: the document is a reactive proxy, which postMessage can't clone
      if (target && targetOrigin) target.postMessage(JSON.parse(JSON.stringify({ ...message, source: "builder" })), targetOrigin);
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

/**
 * A page of the storefront: `path` (with its query) under the storefront URL's own path, like the Angular designer's
 * combine(), so a storefront URL with a path keeps it; undefined without an http(s) storefront.
 */
export function storefrontPage(storefrontUrl: string | undefined, path: string): URL | undefined {
  if (!storefrontUrl) return undefined;
  try {
    const url = new URL(storefrontUrl);
    if (url.protocol !== "http:" && url.protocol !== "https:") return undefined;
    // only its path, query and hash are taken, so "//evil.example" or "https://evil.example" can't change the host
    const page = new URL(`/${path.replace(/^[/\\]+/, "")}`, "http://storefront.invalid");
    url.pathname = url.pathname.replace(/\/+$/, "") + page.pathname;
    url.search = page.search;
    url.hash = page.hash;
    return url;
  } catch {
    return undefined;
  }
}

/** The preview page of a storefront: {storefront}{previewPath}?ep={this origin} (the path must be exactly /designer-preview). */
export function previewUrl(storefrontUrl: string | undefined, previewPath = "/designer-preview"): string | undefined {
  const url = storefrontPage(storefrontUrl, previewPath);
  url?.searchParams.set("ep", window.location.origin);
  return url?.toString();
}
