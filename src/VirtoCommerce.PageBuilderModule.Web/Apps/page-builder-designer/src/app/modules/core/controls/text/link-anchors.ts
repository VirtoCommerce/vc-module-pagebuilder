import { PageAnchor, getActivePageAnchors } from '@core/services';

/**
 * Anchor entry CKEditor's link dialog consumes. Stock CKEditor describes one anchor element through
 * both slots — `name` from `<a name="...">` and `id` from the same element's id attribute — and
 * offers each through its own select, "By Anchor Name" and "By Element Id". The href is then built
 * as `'#' + (name || id)`.
 *
 * Page anchors are published through `name` only. Both selects commit unconditionally and `name`
 * wins when the href is assembled, so the slot that is checked first is the one that always carries
 * the picked value through, and the second select is left empty for the dialog patch to hide.
 */
interface CkAnchor {
    name: string;
    id: null;
}

interface CkDomElement {
    hide(): void;
    show(): void;
    getParent(): CkDomElement | null;
}

interface CkSelect {
    clear(): void;
    add(label: string, value?: string): void;
    setValue(value: string): void;
    getElement(): CkDomElement;
}

interface CkLinkData {
    anchor?: { name?: string; id?: string };
}

type CkSetup = (this: CkSelect, data: CkLinkData) => void;

interface CkElementDefinition {
    setup?: CkSetup;
    /** Layout slots of the container definitions the anchor picker is assembled from. */
    label?: string;
    style?: string;
    width?: string | number;
    align?: string;
}

interface CkDialogContents {
    get(id: string): CkElementDefinition | null;
}

interface CkDialogDefinitionEvent {
    data: {
        name: string;
        definition: { getContents(id: string): CkDialogContents | null };
    };
}

interface CkLinkPlugin {
    getEditorAnchors?: (editor: unknown) => CkAnchor[];
    pbPageWideAnchorsInstalled?: boolean;
}

interface CkEditorNamespace {
    on(event: 'dialogDefinition', listener: (event: CkDialogDefinitionEvent) => void): void;
    plugins?: { link?: CkLinkPlugin };
}

/**
 * Lifts the CKEditor link dialog out of its single-field scope.
 *
 * `CKEDITOR.plugins.link.getEditorAnchors` only scans the editable the dialog was opened from, so a
 * content manager could not link to a heading rendered by another section — which is exactly what a
 * table of contents or a jump link needs (VCST-5704). The option itself cannot be configured away:
 * "Link to anchor in the text" is hardcoded in the link plugin's dialog definition and is not
 * affected by `removePlugins: 'anchor'`, which only drops the toolbar button.
 *
 * Two things are patched:
 *
 * * `getEditorAnchors`, which decides whether the anchor picker is shown at all and resolves an
 *   existing `#value` href back to a list entry;
 * * the anchor picker itself, so it lists readable names instead of bare generated ids.
 *
 * The patches are global and idempotent: they read whichever editor route is currently active
 * through `getActivePageAnchors`, so every later editor instance — in this module or another one —
 * reuses them and gets the anchors that belong to it.
 *
 * @returns `true` once the override is in place, `false` while the link plugin is not loaded yet.
 */
export function installPageWideAnchors(): boolean {
    const ckeditor = (globalThis as { CKEDITOR?: CkEditorNamespace }).CKEDITOR;
    const link = ckeditor?.plugins?.link;

    if (!ckeditor || !link?.getEditorAnchors) {
        return false;
    }

    if (link.pbPageWideAnchorsInstalled) {
        return true;
    }

    const fieldLocalAnchors = link.getEditorAnchors.bind(link);

    link.getEditorAnchors = (editor: unknown) => {
        const anchors = getActivePageAnchors();
        // Fall back to CKEditor's own lookup so an editor opened outside the page editor — or before
        // the page model is available — behaves exactly as it did before.
        return anchors.length
            ? anchors.map(anchor => (<CkAnchor>{ name: anchor.value, id: null }))
            : fieldLocalAnchors(editor);
    };

    customizeAnchorPicker(ckeditor);
    link.pbPageWideAnchorsInstalled = true;

    return true;
}

function customizeAnchorPicker(ckeditor: CkEditorNamespace): void {
    ckeditor.on('dialogDefinition', event => {
        if (event.data.name !== 'link') {
            return;
        }

        const info = event.data.definition.getContents('info');
        const anchorName = info?.get('anchorName');
        const anchorId = info?.get('anchorId');

        if (!info || !anchorName || !anchorId) {
            return;
        }

        flattenAnchorPicker(info);

        const stockNameSetup = anchorName.setup;
        anchorName.setup = function (data: CkLinkData) {
            const anchors = getActivePageAnchors();
            if (!anchors.length) {
                stockNameSetup?.call(this, data);
                return;
            }

            const selected = data?.anchor?.name || '';

            this.clear();
            this.add('');
            anchors.forEach(anchor => this.add(describeAnchor(anchor), anchor.value));

            // An anchor typed by hand, or one that has since been removed from the page, would other-
            // wise silently reset to the empty option and lose the link on save.
            if (selected && !anchors.some(anchor => anchor.value === selected)) {
                this.add(selected, selected);
            }

            this.setValue(selected);
        };

        const stockIdSetup = anchorId.setup;
        anchorId.setup = function (data: CkLinkData) {
            stockIdSetup?.call(this, data);

            // Page anchors are all published through the name select, leaving this one empty. Hiding
            // its table cell — not just the select — lets the remaining one use the whole row.
            const cell = this.getElement().getParent();
            if (getActivePageAnchors().length) {
                cell?.hide();
            } else {
                cell?.show();
            }
        };
    });
}

/**
 * Puts the anchor picker on the same grid as every other link type.
 *
 * Stock CKEditor sizes it as a 260px fieldset centred in the dialog, because it holds two selects
 * side by side — "By Anchor Name" and "By Element Id" — under a "Select an Anchor" legend. Page
 * anchors are published through the name select alone, so the second one is hidden and both the
 * frame and the centring only make the row look unlike the URL, e-mail and phone fields, which all
 * span the dialog.
 *
 * The dialog definition is patched rather than the setup functions, because width and alignment are
 * baked into the container markup when the dialog is built.
 */
function flattenAnchorPicker(info: CkDialogContents): void {
    const options = info.get('anchorOptions');
    if (options) {
        options.width = '100%';
        delete options.align;
    }

    const frame = info.get('selectAnchorText');
    if (frame) {
        // An empty label drops the legend; the fieldset element itself stays, so its default frame
        // has to go through the inline style CKEditor copies onto it.
        frame.label = '';
        frame.style = 'border: none; padding: 0; margin: 0;';
    }

    // The selects size themselves to their table cell, and the cell to the row, so the row is where
    // the full width has to be claimed.
    const row = info.get('selectAnchor');
    if (row) {
        row.style = 'width: 100%;';
    }
}

/** `Hero image (imagexun)`, or just the value when the item has no name of its own. */
function describeAnchor(anchor: PageAnchor): string {
    return anchor.label && anchor.label !== anchor.value
        ? `${anchor.label} (${anchor.value})`
        : anchor.value;
}
