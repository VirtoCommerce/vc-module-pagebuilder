import { InjectionToken } from '@angular/core';

/** A link target available on the page currently open in the designer. */
export interface PageAnchor {
    /** Anchor value without the leading `#`, e.g. `specifications` — this is what a link stores. */
    value: string;
    /** Readable origin of the anchor, e.g. the section name, shown next to the value in the picker. */
    label: string;
}

/**
 * Seam that lets rich-text controls read the anchors of the whole page being edited.
 *
 * The controls live in `core`, while the page model belongs to the lazily loaded `editor` feature,
 * so the dependency is inverted through this token — the same approach `ngv-markdown` uses for its
 * data service. Controls inject it with `{ optional: true }` and degrade to CKEditor's own
 * field-local anchor lookup when nothing is registered.
 */
export interface PageAnchorsProvider {
    /** Anchors in page order. */
    getAnchors(): PageAnchor[];
}

export const PAGE_ANCHORS_PROVIDER = new InjectionToken<PageAnchorsProvider>('PAGE_ANCHORS_PROVIDER');

/**
 * The provider of the editor route that is currently active, or `null` outside it.
 *
 * The rich-text controls patch the global CKEditor namespace, which outlives the editor route, while
 * the provider is route scoped and replaced on every visit. Routing it through this registry keeps
 * the patches reading the live service — and, once the route is gone, nothing at all, so an editor
 * opened elsewhere falls back to CKEditor's own field-local anchors instead of listing the anchors
 * of the page that happened to be open last.
 */
let activeProvider: PageAnchorsProvider | null = null;

/** Called by the route scoped provider when it is created. */
export function setActivePageAnchorsProvider(provider: PageAnchorsProvider): void {
    activeProvider = provider;
}

/**
 * Called by the route scoped provider when it is destroyed. Navigating between pages creates the new
 * provider before the old one is torn down, so a provider that is no longer the active one is
 * ignored rather than clearing its successor.
 */
export function clearActivePageAnchorsProvider(provider: PageAnchorsProvider): void {
    if (activeProvider === provider) {
        activeProvider = null;
    }
}

/** Anchors of the page currently being edited, empty outside the editor route. */
export function getActivePageAnchors(): PageAnchor[] {
    return activeProvider?.getAnchors() || [];
}
