import { collectPageAnchors, getItemAnchor } from './anchors.helpers';
import { createBlock, createSchema, createSection, createTemplate } from '@app/testing';
import { SectionsSchemasList } from '@editor/models';

const noSchemas: SectionsSchemasList = {};

function collect(template: Parameters<typeof collectPageAnchors>[0], schemas: SectionsSchemasList = noSchemas) {
    return collectPageAnchors(template, schemas, noSchemas);
}

function values(template: Parameters<typeof collectPageAnchors>[0]) {
    return collect(template).map(anchor => anchor.value);
}

// ── collectPageAnchors ─────────────────────────────────────────────

describe('collectPageAnchors', () => {
    it('returns an empty list for a missing template', () => {
        expect(collect(null)).toEqual([]);
    });

    it('returns section ids in page order', () => {
        const template = createTemplate({
            content: [createSection({ id: 'hero1' }), createSection({ id: 'text2' })]
        });

        expect(values(template)).toEqual(['hero1', 'text2']);
    });

    it('prefers the anchor setting over the generated id', () => {
        const template = createTemplate({
            content: [createSection({ id: 'text2', anchor: 'Technical Specifications' } as never)]
        });

        expect(values(template)).toEqual(['technical-specifications']);
    });

    it('lists block anchors right after their section', () => {
        const template = createTemplate({
            content: [createSection({ id: 'hero1', blocks: [createBlock({ id: 'title3' })] })]
        });

        expect(values(template)).toEqual(['hero1', 'title3']);
    });

    it('skips hidden sections and blocks because they are not rendered', () => {
        const template = createTemplate({
            content: [
                createSection({ id: 'hidden1', hidden: true }),
                createSection({ id: 'hero1', blocks: [createBlock({ id: 'hidden2', hidden: true })] })
            ]
        });

        expect(values(template)).toEqual(['hero1']);
    });

    it('collects in-text anchors created by the CKEditor Anchor button', () => {
        const template = createTemplate({
            content: [createSection({ id: 'text2', content: '<p><a name="chapter-one">One</a></p>' } as never)]
        });

        expect(values(template)).toEqual(['text2', 'chapter-one']);
    });

    it('collects element ids authored through the source view', () => {
        const template = createTemplate({
            content: [createSection({ id: 'text2', content: '<h2 id="pricing">Pricing</h2>' } as never)]
        });

        expect(values(template)).toEqual(['text2', 'pricing']);
    });

    it('finds rich text nested in list and object settings', () => {
        const template = createTemplate({
            content: [createSection({
                id: 'faq1',
                items: [{ answer: '<p id="shipping">Ships in a day</p>' }],
                footer: { note: { html: '<a name="legal">Legal</a>' } }
            } as never)]
        });

        expect(values(template)).toEqual(['faq1', 'shipping', 'legal']);
    });

    it('keeps every anchor once', () => {
        const template = createTemplate({
            content: [
                createSection({ id: 'text2', content: '<p id="dup"></p><p id="dup"></p>' } as never),
                createSection({ id: 'text3', content: '<p id="dup"></p>' } as never)
            ]
        });

        expect(values(template)).toEqual(['text2', 'dup', 'text3']);
    });

    it('ignores plain text settings', () => {
        const template = createTemplate({
            content: [createSection({ id: 'hero1', title: 'No markup here' } as never)]
        });

        expect(values(template)).toEqual(['hero1']);
    });
});

// ── labels ─────────────────────────────────────────────────────────

describe('collectPageAnchors labels', () => {
    it('names an anchor after the schema displayField the theme declares', () => {
        const template = createTemplate({
            content: [createSection({ id: 'imagexun', type: 'image', name: 'Hero image' } as never)]
        });
        const schemas = <SectionsSchemasList>{ image: createSchema({ type: 'image', name: 'Image', displayField: 'name' }) };

        expect(collect(template, schemas)).toEqual([{ value: 'imagexun', label: 'Hero image' }]);
    });

    it('falls back to the schema name when the item was never named', () => {
        const template = createTemplate({ content: [createSection({ id: 'imagexun', type: 'image' })] });
        const schemas = <SectionsSchemasList>{ image: createSchema({ type: 'image', name: 'Image', displayField: 'name' }) };

        expect(collect(template, schemas)).toEqual([{ value: 'imagexun', label: 'Image' }]);
    });

    it('labels an in-text anchor with the section it lives in', () => {
        const template = createTemplate({
            content: [createSection({ id: 'text2', type: 'text', name: 'Intro', content: '<a name="chapter">C</a>' } as never)]
        });
        const schemas = <SectionsSchemasList>{ text: createSchema({ type: 'text', name: 'Text', displayField: 'name' }) };

        expect(collect(template, schemas)).toEqual([
            { value: 'text2', label: 'Intro' },
            { value: 'chapter', label: 'Intro' }
        ]);
    });
});

// ── getItemAnchor ──────────────────────────────────────────────────

describe('getItemAnchor', () => {
    it('falls back to the id when no anchor is set', () => {
        expect(getItemAnchor(createSection({ id: 'hero1' }))).toBe('hero1');
    });

    it('falls back to the id when the anchor is blank', () => {
        expect(getItemAnchor(createSection({ id: 'hero1', anchor: '   ' } as never))).toBe('hero1');
    });

    it('slugifies the authored anchor so it is valid in a hash link', () => {
        expect(getItemAnchor(createSection({ id: 'hero1', anchor: 'Back To Top!' } as never))).toBe('back-to-top');
    });

    it('falls back to the id when the anchor slugifies to nothing', () => {
        expect(getItemAnchor(createSection({ id: 'hero1', anchor: 'Спецификация' } as never))).toBe('hero1');
    });
});
