import { appHelpers } from '@integration/helpers';
import { PageAnchor } from '@core/services';
import { SectionModel, TemplateModel } from '@models/document';
import { SectionsSchemasList } from '@editor/models';

import { getSectionName } from './editor.helpers';

/**
 * Well-known setting id a theme can declare (usually in `shared/_sections.json`) to let a content
 * manager give a section a readable anchor instead of the generated section id.
 */
export const ANCHOR_SETTING_ID = 'anchor';

/**
 * Collects every anchor a link in a rich-text field can target on the page being edited, in page
 * order and without duplicates.
 *
 * CKEditor only knows the anchors of the field it is attached to, which makes cross-section
 * navigation (tables of contents, jump links, "back to top") impossible to build from the UI —
 * see VCST-5704. Walking the page model instead gives the same list the storefront will render.
 *
 * Every anchor carries the name of the item it belongs to, so the picker can show
 * "Hero image (imagexun)" instead of a bare generated id.
 */
export function collectPageAnchors(
    template: TemplateModel | null | undefined,
    sectionsSchemas: SectionsSchemasList,
    blocksSchemas: SectionsSchemasList
): PageAnchor[] {
    const result: PageAnchor[] = [];

    // Hidden items are not rendered, so their anchors do not exist on the page.
    const visible = (item: SectionModel) => !item.hidden;

    for (const section of (template?.content || []).filter(visible)) {
        collectItemAnchors(section, sectionsSchemas, result);

        for (const block of (section.blocks || []).filter(visible)) {
            // A block type may be described either as a block or as a section schema.
            collectItemAnchors(block, blocksSchemas[block.type] ? blocksSchemas : sectionsSchemas, result);
        }
    }

    return result;
}

function collectItemAnchors(item: SectionModel, schemas: SectionsSchemasList, result: PageAnchor[]): void {
    const label = getSectionName(item, schemas?.[item.type] || null);

    addAnchor(result, getItemAnchor(item), label);

    // `blocks` is walked by the caller, so it is left out to avoid parsing every block twice.
    const settings = Object.entries(item).filter(([key]) => key !== 'blocks').map(([, value]) => value);
    for (const anchor of collectValueAnchors(settings)) {
        addAnchor(result, anchor, label);
    }
}

/**
 * The anchor the storefront renders for a section or block: the readable one a content manager
 * typed, falling back to the generated id that is always present in the markup.
 */
export function getItemAnchor(item: SectionModel): string {
    const custom = item[ANCHOR_SETTING_ID];
    if (typeof custom === 'string' && custom.trim()) {
        // Slugifying drops everything outside `[\w\s-]`, so a value made only of such characters
        // (Cyrillic, for one) collapses to an empty string — the generated id still works then.
        const slug = appHelpers.generateAnchor(custom);
        if (slug) {
            return slug;
        }
    }
    return '' + (item.id || '');
}

/**
 * Rich text can sit anywhere in a setting value — directly on the section, inside an `object`
 * control or inside every item of a `list` control — so the whole value tree is walked.
 */
function collectValueAnchors(value: unknown): string[] {
    if (typeof value === 'string') {
        return extractHtmlAnchors(value);
    }
    if (Array.isArray(value)) {
        return value.flatMap(collectValueAnchors);
    }
    if (value && typeof value === 'object') {
        return Object.values(value).flatMap(collectValueAnchors);
    }
    return [];
}

/**
 * In-text anchors: the ones the CKEditor `Anchor` button produces (`<a name="...">`) and any
 * element the author gave an id to through the source view.
 */
function extractHtmlAnchors(html: string): string[] {
    // Cheap guard — most settings hold plain text and never need parsing.
    if (!html || html.indexOf('<') === -1 || typeof DOMParser === 'undefined') {
        return [];
    }

    const parsed = new DOMParser().parseFromString(html, 'text/html');

    return [
        ...readAttribute(parsed.querySelectorAll('a[name]'), 'name'),
        ...readAttribute(parsed.querySelectorAll('[id]'), 'id')
    ];
}

function readAttribute(elements: ArrayLike<Element>, attribute: string): string[] {
    return Array.from(elements).map(element => (element.getAttribute(attribute) || '').trim());
}

function addAnchor(result: PageAnchor[], value: string, label: string): void {
    if (value && !result.some(anchor => anchor.value === value)) {
        result.push({ value, label });
    }
}
