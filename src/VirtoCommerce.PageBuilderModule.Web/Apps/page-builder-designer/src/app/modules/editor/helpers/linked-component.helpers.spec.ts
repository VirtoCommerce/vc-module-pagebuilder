import { createSection, createTemplate } from '@app/testing';
import { SectionModel } from '@models/document';

import {
    areSectionsContiguous,
    canOpenLinkedComponentUsagePage,
    createLinkedComponentReference,
    detachLinkedComponent,
    insertLinkedComponentCopy,
    insertLinkedComponentReference,
    isLinkedComponentReference,
    replaceSectionsWithLinkedComponent,
    resolveLinkedComponents,
} from './linked-component.helpers';
import { prepareTemplateForSave } from './editor.helpers';

describe('linked component helpers', () => {
    it('opens only usage pages that have an identity and are not archived', () => {
        expect(canOpenLinkedComponentUsagePage({ id: 'page-1', status: 'Draft' })).toBe(true);
        expect(canOpenLinkedComponentUsagePage({ id: 'page-1', status: 'Draft, Published' })).toBe(true);
        expect(canOpenLinkedComponentUsagePage({ id: 'page-1', status: 'Archived' })).toBe(false);
        expect(canOpenLinkedComponentUsagePage({ id: '  ', status: 'Draft' })).toBe(false);
        expect(canOpenLinkedComponentUsagePage({ id: null, status: 'Draft' })).toBe(false);
    });

    it('creates the strict raw marker contract', () => {
        const marker = createLinkedComponentReference('component-1', 'placement-1');

        expect(marker).toEqual({
            id: 'placement-1',
            type: 'componentRef',
            componentRef: 'component-1',
        });
        expect(isLinkedComponentReference(marker)).toBe(true);
    });

    it('rejects a marker with any additional field', () => {
        expect(isLinkedComponentReference({
            id: 'placement-1',
            type: 'componentRef',
            componentRef: 'component-1',
            name: 'not allowed',
        })).toBe(false);
    });

    it('rejects markers with whitespace-only ids', () => {
        expect(isLinkedComponentReference({
            id: '   ',
            type: 'componentRef',
            componentRef: 'component-1',
        })).toBe(false);
        expect(isLinkedComponentReference({
            id: 'placement-1',
            type: 'componentRef',
            componentRef: '\t',
        })).toBe(false);
    });

    it('keeps the raw marker while resolving preview content with stable instance ids', () => {
        const marker = createLinkedComponentReference('component-1', 'placement-1');
        const raw = createTemplate({ content: [createSection({ id: 'inline' }), marker] });
        const linked = createTemplate({
            content: [createSection({
                id: 'shared-section',
                blocks: [createSection({ id: 'shared-block', type: 'text' })],
            })],
        });

        const first = resolveLinkedComponents(raw, { 'component-1': linked });
        const second = resolveLinkedComponents(raw, { 'component-1': linked });

        expect(raw.content[1]).toBe(marker);
        expect(first.template.content).toHaveLength(2);
        expect(first.template.content[1].id).toBe(second.template.content[1].id);
        expect(first.template.content[1].id).not.toBe('shared-section');
        expect(first.template.content[1].blocks[0].id).not.toBe('shared-block');
        expect(first.boundaries).toEqual([{ placementId: 'placement-1', componentRef: 'component-1', startIndex: 1, count: 1 }]);
        expect(prepareTemplateForSave(raw)).toBe(raw);
        expect(isLinkedComponentReference(raw.content[1])).toBe(true);
    });

    it('resolves the preview shape without adding blocks or provenance', () => {
        const raw = createTemplate({
            content: [createLinkedComponentReference('component-1', 'placement-1')],
        });
        const linked = createTemplate({
            content: [{ id: 'shared-section', type: 'hero', name: 'Shared hero' } as unknown as SectionModel],
        });

        const result = resolveLinkedComponents(raw, { 'component-1': linked });

        expect(result.template.content[0]).toEqual({
            id: 'lc706c6163656d656e742d31section0',
            type: 'hero',
            name: 'Shared hero',
        });
        expect(Object.prototype.hasOwnProperty.call(result.template.content[0], 'blocks')).toBe(false);
        expect(Object.keys(result.template.content[0]).some(key => key.startsWith('__linkedComponent'))).toBe(false);
    });

    it('keeps placements distinct when their punctuation would sanitize to the same value', () => {
        const raw = createTemplate({
            content: [
                createLinkedComponentReference('component-1', 'hero-a'),
                createLinkedComponentReference('component-1', 'heroa'),
            ],
        });
        const linked = createTemplate({ content: [createSection({ id: 'shared' })] });

        const result = resolveLinkedComponents(raw, { 'component-1': linked });

        expect(result.template.content[0].id).not.toBe(result.template.content[1].id);
        expect(result.boundaries.map(boundary => boundary.placementId)).toEqual(['hero-a', 'heroa']);
    });

    it('uses lowercase UTF-8 hex placement tokens across ASCII and Unicode golden vectors', () => {
        const linked = createTemplate({ content: [createSection({ id: 'shared' })] });
        const goldenVectors = [
            ['placement-1', 'lc706c6163656d656e742d31section0'],
            ['a-b', 'lc612d62section0'],
            ['Компонент-🙂', 'lcd09ad0bed0bcd0bfd0bed0bdd0b5d0bdd1822df09f9982section0'],
        ] as const;

        goldenVectors.forEach(([placementId, expectedId]) => {
            const raw = createTemplate({
                content: [createLinkedComponentReference('component-1', placementId)],
            });

            const result = resolveLinkedComponents(raw, { 'component-1': linked });

            expect(result.template.content[0].id).toBe(expectedId);
        });
    });

    it('omits unresolved references and reports each missing component once', () => {
        const raw = createTemplate({
            content: [
                createLinkedComponentReference('missing', 'placement-1'),
                createLinkedComponentReference('missing', 'placement-2'),
            ],
        });

        const result = resolveLinkedComponents(raw, {});

        expect(result.template.content).toEqual([]);
        expect(result.missingComponentIds).toEqual(['missing']);
    });

    it('inserts linked references without embedding component content', () => {
        const raw = createTemplate({ content: [createSection({ id: 'before' })] });

        const result = insertLinkedComponentReference(raw, 'component-1', 0, 'placement-1');

        expect(result.content).toHaveLength(2);
        expect(result.content[0]).toEqual({ id: 'placement-1', type: 'componentRef', componentRef: 'component-1' });
        expect(raw.content).toHaveLength(1);
    });

    it('creates independent copies with regenerated section and nested block ids', () => {
        const sourceSection = createSection({
            id: 'source-section',
            blocks: [createSection({ id: 'source-block', type: 'text' })],
        });
        const linked = createTemplate({ content: [sourceSection] });
        const raw = createTemplate();

        const result = insertLinkedComponentCopy(raw, linked, 0);

        expect(result.content[0].id).not.toBe(sourceSection.id);
        expect(result.content[0].blocks[0].id).not.toBe(sourceSection.blocks[0].id);
        expect(result.content[0]).not.toBe(sourceSection);
    });

    it('replaces selected sections with one reference at the first selected position', () => {
        const raw = createTemplate({
            content: [
                createSection({ id: 'first' }),
                createSection({ id: 'selected-1' }),
                createSection({ id: 'selected-2' }),
                createSection({ id: 'last' }),
            ],
        });

        const result = replaceSectionsWithLinkedComponent(
            raw,
            ['selected-1', 'selected-2'],
            'component-1',
            'placement-1',
        );

        expect(result.content.map(section => section.id)).toEqual(['first', 'placement-1', 'last']);
    });

    it('rejects non-adjacent sections instead of reordering page content', () => {
        const raw = createTemplate({
            content: [
                createSection({ id: 'first' }),
                createSection({ id: 'middle' }),
                createSection({ id: 'last' }),
            ],
        });

        expect(areSectionsContiguous(raw, ['first', 'last'])).toBe(false);
        expect(replaceSectionsWithLinkedComponent(raw, ['first', 'last'], 'component-1')).toBe(raw);
        expect(raw.content.map(section => section.id)).toEqual(['first', 'middle', 'last']);
    });

    it('detaches a reference into fresh independent sections', () => {
        const raw = createTemplate({
            content: [createLinkedComponentReference('component-1', 'placement-1')],
        });
        const linked = createTemplate({
            content: [createSection({ id: 'shared', blocks: [createSection({ id: 'block' })] })],
        });

        const result = detachLinkedComponent(raw, 'placement-1', linked);

        expect(result.content).toHaveLength(1);
        expect(isLinkedComponentReference(result.content[0])).toBe(false);
        expect(result.content[0].id).not.toBe('shared');
        expect(result.content[0].blocks[0].id).not.toBe('block');
    });
});
