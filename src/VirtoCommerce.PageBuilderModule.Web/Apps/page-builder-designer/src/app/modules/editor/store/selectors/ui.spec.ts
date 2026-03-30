import * as selectors from './ui';
import { createSection, createSchema, createTemplate } from '@app/testing';
import { SectionStatesList } from '@editor/models';

// ── selectAddItemTitle ────────────────────────────────────────────

describe('selectAddItemTitle', () => {
    it('returns "Add block" when no section', () => {
        expect(selectors.selectAddItemTitle.projector(null, null)).toBe('Add block');
    });

    it('returns section name in title', () => {
        const section = createSection({ id: 's1', type: 'hero', name: 'My Hero' } as any);
        const schema = createSchema({ displayField: 'name' });
        const result = selectors.selectAddItemTitle.projector(section, schema);
        expect(result).toContain('My Hero');
        expect(result).toContain('Add block');
    });
});

// ── hoveredSectionId ──────────────────────────────────────────────

describe('hoveredSectionId', () => {
    it('returns hoveredSectionId from state', () => {
        expect(selectors.hoveredSectionId.projector({ hoveredSectionId: 's1' } as any)).toBe('s1');
    });

    it('returns null when not hovered', () => {
        expect(selectors.hoveredSectionId.projector({ hoveredSectionId: null } as any)).toBeNull();
    });
});

// ── isLoading ─────────────────────────────────────────────────────

describe('isLoading', () => {
    it('returns true when template is loading', () => {
        expect(selectors.isLoading.projector({ isTemplateLoading: true, isSchemasLoading: false } as any)).toBe(true);
    });

    it('returns true when schemas is loading', () => {
        expect(selectors.isLoading.projector({ isTemplateLoading: false, isSchemasLoading: true } as any)).toBe(true);
    });

    it('returns false when nothing is loading', () => {
        expect(selectors.isLoading.projector({ isTemplateLoading: false, isSchemasLoading: false } as any)).toBe(false);
    });
});

// ── selectSectionsState ───────────────────────────────────────────

describe('selectSectionsState', () => {
    it('builds section states with canHaveChildren', () => {
        const model = createTemplate({
            content: [createSection({ id: 's1', type: 'hero' })],
        });
        const schemas = { hero: { blocks: ['text'] } } as any;
        const state = { sections: {} } as any;

        const result = selectors.selectSectionsState.projector(state, model, schemas, [], false, undefined);

        expect(result['s1']).toBeTruthy();
        expect(result['s1'].canHaveChildren).toBe(true);
        expect(result['s1'].expanded).toBe(true);
    });

    it('sets canHaveChildren false when no blocks defined', () => {
        const model = createTemplate({
            content: [createSection({ id: 's1', type: 'hero' })],
        });
        const schemas = { hero: {} } as any;
        const state = { sections: {} } as any;

        const result = selectors.selectSectionsState.projector(state, model, schemas, [], false, undefined);

        expect(result['s1'].canHaveChildren).toBe(false);
    });

    it('marks dragging sections', () => {
        const model = createTemplate({
            content: [createSection({ id: 's1', type: 'hero' })],
        });
        const schemas = { hero: {} } as any;
        const state = { sections: {} } as any;

        const result = selectors.selectSectionsState.projector(state, model, schemas, ['s1'], false, undefined);

        expect(result['s1'].isDragging).toBe(true);
    });

    it('returns empty when schemas is null', () => {
        const model = createTemplate({ content: [createSection()] });
        const result = selectors.selectSectionsState.projector(null, model, null as any, [], false, undefined);
        expect(result).toEqual({});
    });

    it('returns empty when model is null', () => {
        const result = selectors.selectSectionsState.projector(null, null, {}, [], false, undefined);
        expect(result).toEqual({});
    });

    it('sets selectable=false when a block in another section is selected', () => {
        const model = createTemplate({
            content: [
                createSection({ id: 's1', type: 'hero' }),
                createSection({ id: 's2', type: 'banner' }),
            ],
        });
        const schemas = { hero: {}, banner: {} } as any;
        const state = { sections: {} } as any;

        // sectionKeyWithSelectedBlock = 's1' means s1 has a selected block
        const result = selectors.selectSectionsState.projector(state, model, schemas, [], false, 's1');
        // s2 should not be selectable because a block in s1 is selected
        expect(result['s2'].selectable).toBe(false);
    });

    it('sets selectable=true for section when no block is selected anywhere', () => {
        const model = createTemplate({
            content: [createSection({ id: 's1', type: 'hero' })],
        });
        const schemas = { hero: {} } as any;
        const state = { sections: {} } as any;

        const result = selectors.selectSectionsState.projector(state, model, schemas, [], false, undefined);
        expect(result['s1'].selectable).toBe(true);
    });

    it('builds block states with selectable flag based on section selection', () => {
        const block = createSection({ id: 'b1', type: 'text' });
        const model = createTemplate({
            content: [createSection({ id: 's1', type: 'hero', blocks: [block] })],
        });
        const schemas = { hero: { blocks: ['text'] } } as any;
        const state = { sections: {} } as any;

        // isSectionSelected=true means blocks should not be selectable
        const result = selectors.selectSectionsState.projector(state, model, schemas, [], true, undefined);
        expect(result['s1'].blocks['b1'].selectable).toBe(false);
    });

    it('blocks are selectable when no section is selected and block is in correct section', () => {
        const block = createSection({ id: 'b1', type: 'text' });
        const model = createTemplate({
            content: [createSection({ id: 's1', type: 'hero', blocks: [block] })],
        });
        const schemas = { hero: { blocks: ['text'] } } as any;
        const state = { sections: {} } as any;

        const result = selectors.selectSectionsState.projector(state, model, schemas, [], false, undefined);
        expect(result['s1'].blocks['b1'].selectable).toBe(true);
    });

    it('blocks are not selectable when selected block is in different section', () => {
        const block1 = createSection({ id: 'b1', type: 'text' });
        const block2 = createSection({ id: 'b2', type: 'text' });
        const model = createTemplate({
            content: [
                createSection({ id: 's1', type: 'hero', blocks: [block1] }),
                createSection({ id: 's2', type: 'hero', blocks: [block2] }),
            ],
        });
        const schemas = { hero: { blocks: ['text'] } } as any;
        const state = { sections: {} } as any;

        // sectionKeyWithSelectedBlock = 's1' means s1 has a selected block
        const result = selectors.selectSectionsState.projector(state, model, schemas, [], false, 's1');
        // blocks in s2 should not be selectable
        expect(result['s2'].blocks['b2'].selectable).toBe(false);
        // blocks in s1 should be selectable
        expect(result['s1'].blocks['b1'].selectable).toBe(true);
    });

    it('marks block as dragging', () => {
        const block = createSection({ id: 'b1', type: 'text' });
        const model = createTemplate({
            content: [createSection({ id: 's1', type: 'hero', blocks: [block] })],
        });
        const schemas = { hero: { blocks: ['text'] } } as any;
        const state = { sections: {} } as any;

        const result = selectors.selectSectionsState.projector(state, model, schemas, ['b1'], false, undefined);
        expect(result['s1'].blocks['b1'].isDragging).toBe(true);
    });

    it('preserves existing section state from domain', () => {
        const model = createTemplate({
            content: [createSection({ id: 's1', type: 'hero' })],
        });
        const schemas = { hero: {} } as any;
        const state = { sections: { s1: { expanded: false, selected: true, selectable: true, isDragging: false, blocks: {} } } } as any;

        const result = selectors.selectSectionsState.projector(state, model, schemas, [], false, undefined);
        expect(result['s1'].selected).toBe(true);
        expect(result['s1'].expanded).toBe(false);
    });

    it('skips sections without type or id', () => {
        const model = createTemplate({
            content: [
                createSection({ id: 's1', type: 'hero' }),
                { id: '', type: '', hidden: false, blocks: [] } as any,
            ],
        });
        const schemas = { hero: {} } as any;
        const state = { sections: {} } as any;

        const result = selectors.selectSectionsState.projector(state, model, schemas, [], false, undefined);
        expect(Object.keys(result)).toEqual(['s1']);
    });
});

// ── editTemplateContext ───────────────────────────────────────────

describe('editTemplateContext', () => {
    it('returns null when template is missing', () => {
        const result = selectors.editTemplateContext.projector(
            null,
            { template: null, sectionsSchemas: {}, blocksSchemas: {}, settings: {}, settingsSchemas: {} } as any,
            {},
            [],
        );
        expect(result).toBeNull();
    });

    it('returns context when all data available', () => {
        const template = createTemplate({ content: [createSection({ id: 's1', type: 'hero' })] });
        const sectionsSchemas = { hero: createSchema() };
        const blocksSchemas = { text: createSchema({ type: 'text' }) };
        const sectionsState: SectionStatesList = { s1: { expanded: true, canHaveChildren: true, isDragging: false, selectable: true, selected: false, blocks: {} } };

        const result = selectors.editTemplateContext.projector(
            { sections: {} } as any,
            { template, sectionsSchemas, blocksSchemas, settings: template.settings, settingsSchemas: {} } as any,
            sectionsState,
            [],
        );

        expect(result).not.toBeNull();
        expect(result!.template).toBe(template);
        expect(result!.sectionsSchemas).toBe(sectionsSchemas);
        expect(result!.selectMode).toBe(false);
    });

    it('enters selectMode when sections are selected', () => {
        const template = createTemplate({ content: [createSection({ id: 's1', type: 'hero' })] });
        const sectionsState: SectionStatesList = { s1: { expanded: true, canHaveChildren: false, isDragging: false, selectable: true, selected: true, blocks: {} } };

        const result = selectors.editTemplateContext.projector(
            { sections: {} } as any,
            { template, sectionsSchemas: { hero: {} }, blocksSchemas: { text: {} }, settings: template.settings, settingsSchemas: {} } as any,
            sectionsState,
            [],
        );

        expect(result!.selectMode).toBe(true);
        expect(result!.selectedSectionsCount).toBe(1);
    });
});

// ── selectToolbarButtonsState ─────────────────────────────────────

describe('selectToolbarButtonsState', () => {
    it('always includes Save button', () => {
        const selector = selectors.selectToolbarButtonsState({ useTheme: false, useDrafts: false, useExternalPreview: false });
        const result = selector.projector(false, null);
        const allButtons = result.flat();
        expect(allButtons.find(b => b.alias === 'save')).toBeTruthy();
    });

    it('includes theme-settings when useTheme is true', () => {
        const selector = selectors.selectToolbarButtonsState({ useTheme: true, useDrafts: false, useExternalPreview: false });
        const result = selector.projector(false, null);
        const allButtons = result.flat();
        expect(allButtons.find(b => b.alias === 'theme-settings')).toBeTruthy();
    });

    it('includes preview when useExternalPreview is true', () => {
        const selector = selectors.selectToolbarButtonsState({ useTheme: false, useDrafts: false, useExternalPreview: true });
        const result = selector.projector(false, null);
        const allButtons = result.flat();
        expect(allButtons.find(b => b.alias === 'external-preview')).toBeTruthy();
    });

    it('includes publish/unpublish when useDrafts is true and not loading', () => {
        const selector = selectors.selectToolbarButtonsState({ useTheme: false, useDrafts: true, useExternalPreview: false });
        const state = { isLoading: false, error: undefined, published: true, hasChanges: false } as any;
        const result = selector.projector(false, state);
        const allButtons = result.flat();
        expect(allButtons.find(b => b.alias === 'publish')).toBeTruthy();
        expect(allButtons.find(b => b.alias === 'unpublish')).toBeTruthy();
    });

    it('hides publish/unpublish when loading', () => {
        const selector = selectors.selectToolbarButtonsState({ useTheme: false, useDrafts: true, useExternalPreview: false });
        const state = { isLoading: true } as any;
        const result = selector.projector(false, state);
        const allButtons = result.flat();
        expect(allButtons.find(b => b.alias === 'publish')).toBeFalsy();
    });

    it('Save canAction is true when hasDirty', () => {
        const selector = selectors.selectToolbarButtonsState({ useTheme: false, useDrafts: false, useExternalPreview: false });
        const result = selector.projector(true, null);
        const saveBtn = result.flat().find(b => b.alias === 'save');
        expect(saveBtn!.canAction).toBe(true);
    });

    it('Save canAction is false when not dirty', () => {
        const selector = selectors.selectToolbarButtonsState({ useTheme: false, useDrafts: false, useExternalPreview: false });
        const result = selector.projector(false, null);
        const saveBtn = result.flat().find(b => b.alias === 'save');
        expect(saveBtn!.canAction).toBe(false);
    });
});

// ── selectCurrentItemName ─────────────────────────────────────────

describe('selectCurrentItemName', () => {
    it('returns "Edit" + section name', () => {
        const section = createSection({ id: 's1', name: 'Hero' } as any);
        const schema = createSchema({ displayField: 'name' });
        const result = selectors.selectCurrentItemName.projector(null, section, null, schema);
        expect(result).toBe('Edit Hero');
    });

    it('returns "Edit settings" for settings', () => {
        const settings = createSection({ type: 'settings' });
        const result = selectors.selectCurrentItemName.projector(null, null, settings, null);
        expect(result).toBe('Edit settings');
    });
});
