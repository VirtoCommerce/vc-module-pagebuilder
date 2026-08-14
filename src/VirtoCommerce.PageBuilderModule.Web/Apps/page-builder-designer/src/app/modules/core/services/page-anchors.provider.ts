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
