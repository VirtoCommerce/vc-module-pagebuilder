import { onBeforeUnmount, ref, shallowRef, type Ref } from "vue";
import type { Item, PageDocument, Schemas } from "./types";

/**
 * The storefront's /designer-preview page in an iframe, driven over postMessage. Wire-compatible with the Angular
 * designer, so the storefront needs no change:
 *  - the frame's URL carries ?ep=<this origin>; the editor sends {type:'connect'} on every frame load and the
 *    preview answers {source:'preview', type:'loaded', data:<schemas>};
 *  - messages wait in a queue until 'loaded', then go out in order with source:'builder';
 *  - the preview stays blank until its first message after 'connect', so 'page' goes out right after 'loaded';
 *  - the preview sends 'select' / 'hover' ({data:{sectionId}}) for shared-component placements only;
 *  - a theme template (product, category, page) shows on a real page: 'navigate' to it, then renders that carry the
 *    template's key; the preview answers 'resolved' with the template the store's rules pick there.
 */
export type PreviewMessage =
  | { type: "page" | "changed" | "reload"; template: PageDocument; templateKey?: string; cultureName?: string; sectionId?: string; sectionLabels?: Record<string, string> }
  | { type: "preview"; template: PageDocument; templateKey?: string; model: Record<string, unknown>; cultureName?: string; sectionLabels?: Record<string, string> }
  /** the page the next render with a templateKey shows (a storefront route: /product/{id}, /category/{id}, a permalink) */
  | { type: "navigate"; url: string }
  | { type: "select"; sectionId: string }
  | { type: "hover"; sectionId: string | null }
  | { type: "settings"; settings: Record<string, unknown> }
  /** off: the preview works like the live page (no outlines, clicks reach the page) */
  | { type: "inspector"; enabled: boolean }
  /** the theme's header and footer sections as they are edited (Shopify's section groups) */
  | { type: "groups"; header: Item[]; footer: Item[] };
// from the preview: 'loaded', 'select', 'hover', 'edit' {itemId, path, value} (a text edited on the page), 'resolved'

/** The template the store's rules pick for the page the preview shows: `rule` is the winning rule's index, null for the default */
export interface Resolved {
  pageType: string;
  template: string;
  rule: number | null;
}

export function usePreviewBridge(options: {
  frame: Ref<HTMLIFrameElement | undefined>;
  url: Ref<string | undefined>;
  /** the preview is ready (on every frame load): send it the page */
  onLoaded: (schemas: Schemas) => void;
  onSelect?: (sectionId: string) => void;
  onHover?: (sectionId: string | null) => void;
  /** a text edited on the page: the string at the path of that section or block */
  onEdit?: (edit: { itemId: string; path: (string | number)[]; value: string }) => void;
  onResolved?: (resolved: Resolved) => void;
}) {
  const connected = ref(false);
  const schemas = shallowRef<Schemas>();
  let queue: PreviewMessage[] = [];

  const origin = () => {
    try {
      return options.url.value ? new URL(options.url.value).origin : undefined;
    } catch {
      return undefined;
    }
  };

  function post(message: PreviewMessage | { type: "connect" }) {
    const target = options.frame.value?.contentWindow;
    const targetOrigin = origin();
    // plain data: the document is a reactive proxy, which postMessage can't clone
    if (target && targetOrigin) target.postMessage(JSON.parse(JSON.stringify({ ...message, source: "builder" })), targetOrigin);
  }

  /** Sends now, or once the preview is ready. A newer full render replaces queued renders. */
  function send(message: PreviewMessage) {
    if (connected.value) return post(message);
    // only the last of each kind counts: the render the page needs, and the page it shows on
    if ("template" in message) queue = queue.filter((x) => !("template" in x));
    if (message.type === "navigate") queue = queue.filter((x) => x.type !== "navigate");
    queue.push(message);
  }

  /** The frame (re)loaded: a new document, so a new handshake */
  function onFrameLoad() {
    connected.value = false;
    post({ type: "connect" });
  }

  function onMessage(event: MessageEvent) {
    if (event.source !== options.frame.value?.contentWindow || event.origin !== origin()) return;
    const data = event.data as { source?: string; type?: string; data?: { sectionId?: string | null } & Partial<Schemas> };
    if (data?.source !== "preview") return;
    if (data.type === "loaded") {
      schemas.value = data.data as Schemas;
      connected.value = true;
      options.onLoaded(schemas.value);
      const pending = queue;
      queue = [];
      pending.forEach(post);
    } else if (data.type === "select" && data.data?.sectionId) options.onSelect?.(data.data.sectionId);
    else if (data.type === "hover") options.onHover?.(data.data?.sectionId ?? null);
    else if (data.type === "edit") {
      const edit = data.data as { itemId?: unknown; path?: unknown; value?: unknown } | undefined;
      const path = Array.isArray(edit?.path) ? (edit.path as unknown[]) : [];
      if (typeof edit?.itemId === "string" && typeof edit.value === "string" && path.length && path.every((x) => typeof x === "string" || typeof x === "number"))
        options.onEdit?.({ itemId: edit.itemId, path: path as (string | number)[], value: edit.value });
    } else if (data.type === "resolved") {
      const resolved = data.data as Partial<Resolved> | undefined;
      if (typeof resolved?.template === "string" && (typeof resolved.rule === "number" || resolved.rule === null))
        options.onResolved?.({ pageType: String(resolved.pageType ?? ""), template: resolved.template, rule: resolved.rule });
    }
  }

  window.addEventListener("message", onMessage);
  onBeforeUnmount(() => window.removeEventListener("message", onMessage));

  return { connected, schemas, send, onFrameLoad };
}

/** The preview page of a storefront: {storefront}{previewPath}?ep={this origin} (the path must be exactly /designer-preview). */
export function previewUrl(storefrontUrl: string | undefined, previewPath = "/designer-preview"): string | undefined {
  if (!storefrontUrl) return undefined;
  try {
    // joined like the Angular designer's combine(): a storefront URL with a path keeps it
    const url = new URL(storefrontUrl.replace(/\/+$/, "") + "/" + previewPath.replace(/^\/+/, ""));
    if (url.protocol !== "http:" && url.protocol !== "https:") return undefined;
    url.searchParams.set("ep", window.location.origin);
    return url.toString();
  } catch {
    return undefined;
  }
}
