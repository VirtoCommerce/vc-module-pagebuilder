import {
  addItemToTemplate,
  applySectionChanges,
  applyBlockChanges,
  applySettingsChanges,
  reorderSections,
  reorderBlocks,
  duplicateSection,
  duplicateBlock,
  removeSection,
  removeBlock,
  insertSection,
  insertBlock,
  prepareTemplate,
  convertTemplateIntoCorrectVersion,
  prepareTemplateForSave,
  getSectionName,
  mergeSchemas,
  prepareSchema,
  generateModelBySchema,
  generateSectionId,
} from './editor.helpers';
import { createTemplate, createSection, createBlock, createSchema } from '@app/testing';
import { SectionModel } from '@models/document/section.model';
import { SchemasList } from '@editor/models';

// ── addItemToTemplate ──────────────────────────────────────────────

describe('addItemToTemplate', () => {
  it('adds section at end when insertIndex is -1', () => {
    const template = createTemplate({
      content: [createSection({ id: 's1', type: 'hero' })],
    });
    const schema = createSchema({ type: 'banner', settings: [] });

    const result = addItemToTemplate(schema, template, null, -1);

    expect(result.template.content.length).toBe(2);
    expect(result.template.content[0].id).toBe('s1');
    expect(result.template.content[1].type).toBe('banner');
    expect(result.sectionId).toBeTruthy();
  });

  it('inserts section at specific index', () => {
    const template = createTemplate({
      content: [
        createSection({ id: 's1', type: 'hero' }),
        createSection({ id: 's2', type: 'footer' }),
      ],
    });
    const schema = createSchema({ type: 'banner', settings: [] });

    const result = addItemToTemplate(schema, template, null, 1);

    expect(result.template.content.length).toBe(3);
    expect(result.template.content[0].id).toBe('s1');
    expect(result.template.content[1].type).toBe('banner');
    expect(result.template.content[2].id).toBe('s2');
  });

  it('adds block to existing section', () => {
    const section = createSection({ id: 's1', type: 'hero', blocks: [] });
    const template = createTemplate({ content: [section] });
    const schema = createSchema({ type: 'text', settings: [] });

    const result = addItemToTemplate(schema, template, section, -1);

    expect(result.template.content[0].blocks.length).toBe(1);
    expect(result.sectionId).toBe('s1');
    expect(result.blockId).toBeTruthy();
  });

  it('returns unchanged template when section not found', () => {
    const template = createTemplate({ content: [createSection({ id: 's1' })] });
    const schema = createSchema({ type: 'text', settings: [] });
    const missingSection = createSection({ id: 'missing' });

    const result = addItemToTemplate(schema, template, missingSection, -1);

    expect(result.template).toBe(template);
    expect(result.sectionId).toBe('missing');
  });

  it('does not mutate original template', () => {
    const template = createTemplate({ content: [] });
    const schema = createSchema({ type: 'banner', settings: [] });

    const result = addItemToTemplate(schema, template, null, -1);

    expect(result.template).not.toBe(template);
    expect(template.content.length).toBe(0);
  });
});

// ── applySectionChanges ────────────────────────────────────────────

describe('applySectionChanges', () => {
  it('merges changes into the correct section', () => {
    const template = createTemplate({
      content: [
        createSection({ id: 's1', type: 'hero' }),
        createSection({ id: 's2', type: 'banner' }),
      ],
    });

    const result = applySectionChanges(template, { hidden: true } as Partial<SectionModel>, 's1');

    expect((result.content[0] as any).hidden).toBe(true);
    expect(result.content[1].id).toBe('s2');
  });

  it('does not mutate the original', () => {
    const template = createTemplate({
      content: [createSection({ id: 's1', type: 'hero' })],
    });

    const result = applySectionChanges(template, { hidden: true } as Partial<SectionModel>, 's1');

    expect(result).not.toBe(template);
    expect(result.content[0]).not.toBe(template.content[0]);
  });
});

// ── applyBlockChanges ──────────────────────────────────────────────

describe('applyBlockChanges', () => {
  it('merges changes into the correct block', () => {
    const block = createBlock({ id: 'b1', type: 'text' });
    const section = createSection({ id: 's1', blocks: [block] });
    const template = createTemplate({ content: [section] });

    const result = applyBlockChanges(template, { hidden: true } as Partial<SectionModel>, 's1', 'b1');

    expect((result.content[0].blocks[0] as any).hidden).toBe(true);
    expect(result.content[0].blocks[0].id).toBe('b1');
  });

  it('does not mutate the original', () => {
    const block = createBlock({ id: 'b1' });
    const section = createSection({ id: 's1', blocks: [block] });
    const template = createTemplate({ content: [section] });

    const result = applyBlockChanges(template, { hidden: true } as Partial<SectionModel>, 's1', 'b1');

    expect(result.content[0].blocks[0]).not.toBe(block);
  });
});

// ── applySettingsChanges ───────────────────────────────────────────

describe('applySettingsChanges', () => {
  it('merges changes into template settings', () => {
    const template = createTemplate();

    const result = applySettingsChanges(template, { hidden: true } as Partial<SectionModel>);

    expect((result.settings as any).hidden).toBe(true);
    expect(result.settings.type).toBe('settings');
  });

  it('does not mutate the original', () => {
    const template = createTemplate();
    const result = applySettingsChanges(template, { hidden: true } as Partial<SectionModel>);
    expect(result).not.toBe(template);
    expect(result.settings).not.toBe(template.settings);
  });
});

// ── reorderSections ────────────────────────────────────────────────

describe('reorderSections', () => {
  it('moves a single section (empty sectionIds)', () => {
    const template = createTemplate({
      content: [
        createSection({ id: 'a' }),
        createSection({ id: 'b' }),
        createSection({ id: 'c' }),
      ],
    });

    const result = reorderSections(template, 0, 2, []);

    expect(result.content.map(s => s.id)).toEqual(['c', 'a', 'b']);
  });

  it('moves multiple selected sections', () => {
    const template = createTemplate({
      content: [
        createSection({ id: 'a' }),
        createSection({ id: 'b' }),
        createSection({ id: 'c' }),
        createSection({ id: 'd' }),
      ],
    });

    // Move selected [a, c] to after position 1 (b is at previousIndex=0 after removing)
    const result = reorderSections(template, 3, 0, ['a', 'c']);

    expect(result.content.map(s => s.id)).toEqual(['b', 'd', 'a', 'c']);
  });

  it('does not mutate the original', () => {
    const template = createTemplate({
      content: [createSection({ id: 'a' }), createSection({ id: 'b' })],
    });

    const result = reorderSections(template, 0, 1, []);

    expect(result).not.toBe(template);
    expect(result.content).not.toBe(template.content);
  });
});

// ── reorderBlocks ──────────────────────────────────────────────────

describe('reorderBlocks', () => {
  it('reorders blocks within a section', () => {
    const section = createSection({
      id: 's1',
      blocks: [
        createBlock({ id: 'b1' }),
        createBlock({ id: 'b2' }),
        createBlock({ id: 'b3' }),
      ],
    });
    const template = createTemplate({ content: [section] });

    const result = reorderBlocks(template, section, 0, 2, []);

    expect(result.content[0].blocks.map(b => b.id)).toEqual(['b3', 'b1', 'b2']);
  });
});

// ── duplicateSection ───────────────────────────────────────────────

describe('duplicateSection', () => {
  it('inserts duplicate after original', () => {
    const template = createTemplate({
      content: [
        createSection({ id: 's1', type: 'hero' }),
        createSection({ id: 's2', type: 'banner' }),
      ],
    });

    const result = duplicateSection(template, 's1');

    expect(result.template.content.length).toBe(3);
    expect(result.template.content[0].id).toBe('s1');
    expect(result.template.content[1].type).toBe('hero');
    expect(result.template.content[1].id).not.toBe('s1');
    expect(result.template.content[2].id).toBe('s2');
    expect(result.sectionId).toBe(result.template.content[1].id);
  });

  it('does not mutate original', () => {
    const template = createTemplate({
      content: [createSection({ id: 's1' })],
    });

    duplicateSection(template, 's1');

    expect(template.content.length).toBe(1);
  });
});

// ── duplicateBlock ─────────────────────────────────────────────────

describe('duplicateBlock', () => {
  it('inserts duplicate block after original', () => {
    const section = createSection({
      id: 's1',
      blocks: [createBlock({ id: 'b1', type: 'text' }), createBlock({ id: 'b2', type: 'image' })],
    });
    const template = createTemplate({ content: [section] });

    const result = duplicateBlock(template, 's1', 'b1');

    expect(result.template.content[0].blocks.length).toBe(3);
    expect(result.template.content[0].blocks[0].id).toBe('b1');
    expect(result.template.content[0].blocks[1].type).toBe('text');
    expect(result.template.content[0].blocks[1].id).not.toBe('b1');
    expect(result.template.content[0].blocks[2].id).toBe('b2');
    expect(result.blockId).toBe(result.template.content[0].blocks[1].id);
  });
});

// ── removeSection ──────────────────────────────────────────────────

describe('removeSection', () => {
  it('removes section by id', () => {
    const template = createTemplate({
      content: [
        createSection({ id: 's1' }),
        createSection({ id: 's2' }),
        createSection({ id: 's3' }),
      ],
    });

    const result = removeSection(template, 's2');

    expect(result.content.length).toBe(2);
    expect(result.content.map(s => s.id)).toEqual(['s1', 's3']);
  });

  it('does not mutate original', () => {
    const template = createTemplate({
      content: [createSection({ id: 's1' })],
    });

    removeSection(template, 's1');

    expect(template.content.length).toBe(1);
  });
});

// ── removeBlock ────────────────────────────────────────────────────

describe('removeBlock', () => {
  it('removes block from section', () => {
    const section = createSection({
      id: 's1',
      blocks: [createBlock({ id: 'b1' }), createBlock({ id: 'b2' })],
    });
    const template = createTemplate({ content: [section] });

    const result = removeBlock(template, 's1', 'b1');

    expect(result.content[0].blocks.length).toBe(1);
    expect(result.content[0].blocks[0].id).toBe('b2');
  });
});

// ── insertSection ──────────────────────────────────────────────────

describe('insertSection', () => {
  it('appends section when direction is -1', () => {
    const template = createTemplate({
      content: [createSection({ id: 's1' })],
    });
    const section = createSection({ type: 'banner' });

    const result = insertSection(template, null, section, -1);

    expect(result.template.content.length).toBe(2);
    expect(result.template.content[1].type).toBe('banner');
    expect(result.sectionId).toBeTruthy();
  });

  it('inserts section after target when direction is 1', () => {
    const template = createTemplate({
      content: [
        createSection({ id: 's1' }),
        createSection({ id: 's2' }),
      ],
    });
    const section = createSection({ type: 'banner' });

    const result = insertSection(template, 's1', section, 1);

    expect(result.template.content.length).toBe(3);
    expect(result.template.content[1].type).toBe('banner');
  });

  it('inserts section before target when direction is 0', () => {
    const template = createTemplate({
      content: [
        createSection({ id: 's1' }),
        createSection({ id: 's2' }),
      ],
    });
    const section = createSection({ type: 'banner' });

    const result = insertSection(template, 's1', section, 0);

    expect(result.template.content.length).toBe(3);
    expect(result.template.content[0].type).toBe('banner');
  });

  it('generates new id for inserted section', () => {
    const template = createTemplate({ content: [] });
    const section = createSection({ id: 'old-id', type: 'banner' });

    const result = insertSection(template, null, section, -1);

    expect(result.sectionId).not.toBe('old-id');
  });
});

// ── insertBlock ────────────────────────────────────────────────────

describe('insertBlock', () => {
  it('appends block when direction is -1', () => {
    const section = createSection({ id: 's1', blocks: [createBlock({ id: 'b1' })] });
    const template = createTemplate({ content: [section] });
    const block = createBlock({ type: 'text' });

    const result = insertBlock(template, 's1', null, block, -1);

    expect(result.template.content[0].blocks.length).toBe(2);
  });

  it('inserts block after target when direction is 1', () => {
    const section = createSection({
      id: 's1',
      blocks: [createBlock({ id: 'b1' }), createBlock({ id: 'b2' })],
    });
    const template = createTemplate({ content: [section] });
    const block = createBlock({ type: 'new' });

    const result = insertBlock(template, 's1', 'b1', block, 1);

    expect(result.template.content[0].blocks.length).toBe(3);
    expect(result.template.content[0].blocks[1].type).toBe('new');
  });

  it('returns unchanged template when section not found', () => {
    const template = createTemplate({ content: [] });
    const block = createBlock({ type: 'text' });

    const result = insertBlock(template, 'missing', null, block, -1);

    expect(result.template).toBe(template);
  });
});

// ── prepareTemplate ────────────────────────────────────────────────

describe('prepareTemplate', () => {
  it('assigns ids to sections without ids', () => {
    const template = createTemplate({
      content: [{ type: 'hero', hidden: false, blocks: [] } as any],
    });

    const result = prepareTemplate(template);

    expect(result.content[0].id).toBeTruthy();
  });

  it('assigns ids to blocks', () => {
    const template = createTemplate({
      content: [
        createSection({
          id: 's1',
          blocks: [{ type: 'text', hidden: false, blocks: [] } as any],
        }),
      ],
    });

    const result = prepareTemplate(template);

    expect(result.content[0].blocks[0].id).toBeTruthy();
  });

  it('preserves existing ids', () => {
    const template = createTemplate({
      content: [createSection({ id: 'my-id' })],
    });

    const result = prepareTemplate(template);

    expect(result.content[0].id).toBe('my-id');
  });

  it('returns empty content for null content', () => {
    const template = { settings: createSection(), content: null } as any;

    const result = prepareTemplate(template);

    expect(result.content).toEqual([]);
  });
});

// ── convertTemplateIntoCorrectVersion ──────────────────────────────

describe('convertTemplateIntoCorrectVersion', () => {
  it('returns TemplateModel as-is', () => {
    const template = createTemplate();

    const result = convertTemplateIntoCorrectVersion(template);

    expect(result).toBe(template);
  });

  it('converts old array format', () => {
    const settings = createSection({ id: 'settings' });
    const section = createSection({ id: 's1' });
    const arr = [settings, section];

    const result = convertTemplateIntoCorrectVersion(arr);

    expect(result!.settings).toBe(settings);
    expect(result!.content).toEqual([section]);
    expect(result!.version).toBe(1);
  });

  it('converts PageModel format', () => {
    const pageContent = JSON.stringify({
      settings: { type: 'settings' },
      content: [{ id: 's1', type: 'hero' }],
    });
    const page = {
      id: 'page-1',
      name: 'Test Page',
      storeId: 'store-1',
      permalink: '/test',
      cultureName: 'en-US',
      pageContent,
    };

    const result = convertTemplateIntoCorrectVersion(page as any);

    expect(result!.settings.id).toBe('page-1');
    expect((result!.settings as any).permalink).toBe('/test');
    expect(result!.content[0].type).toBe('hero');
  });

  it('returns empty template for null', () => {
    const result = convertTemplateIntoCorrectVersion(null);

    expect(result!.content).toEqual([]);
    expect(result!.settings.type).toBe('settings');
  });

  it('parses JSON string', () => {
    const template = createTemplate({ content: [createSection({ id: 's1' })] });
    const json = JSON.stringify(template);

    const result = convertTemplateIntoCorrectVersion(json as any);

    expect(result!.content[0].id).toBe('s1');
  });

  it('returns null for unrecognized object', () => {
    const result = convertTemplateIntoCorrectVersion({ foo: 'bar' } as any);

    expect(result).toBeNull();
  });
});

// ── prepareTemplateForSave ─────────────────────────────────────────

describe('prepareTemplateForSave', () => {
  it('converts version 1 to array format', () => {
    const template = createTemplate({
      version: 1,
      content: [createSection({ id: 's1' })],
    });

    const result = prepareTemplateForSave(template);

    expect(Array.isArray(result)).toBe(true);
    expect((result as SectionModel[]).length).toBe(2);
    expect((result as SectionModel[])[0]).toBe(template.settings);
  });

  it('passes through non-version-1 templates', () => {
    const template = createTemplate();

    const result = prepareTemplateForSave(template);

    expect(result).toBe(template);
  });
});

// ── getSectionName ─────────────────────────────────────────────────

describe('getSectionName', () => {
  it('returns displayField value when schema has displayField', () => {
    const item = createSection({ id: 's1', type: 'hero', title: 'My Hero' } as any);
    const schema = createSchema({ displayField: 'title' });

    expect(getSectionName(item, schema)).toBe('My Hero');
  });

  it('falls back to item.name', () => {
    const item = createSection({ id: 's1', type: 'hero', name: 'Named' } as any);
    const schema = createSchema();

    expect(getSectionName(item, schema)).toBe('Named');
  });

  it('falls back to defaultValue', () => {
    const item = createSection({ id: 's1', type: 'hero' });
    const schema = createSchema();

    expect(getSectionName(item, schema, 'Default')).toBe('Default');
  });

  it('falls back to schema.name', () => {
    const item = createSection({ id: 's1', type: 'hero' });
    const schema = createSchema({ name: 'Hero Section' });

    expect(getSectionName(item, schema)).toBe('Hero Section');
  });

  it('falls back to item.type when no schema', () => {
    expect(getSectionName(null, null, null)).toBe('[no name]');
  });

  it('strips HTML tags', () => {
    const item = createSection({ id: 's1', type: 'hero', name: '<b>Bold</b>' } as any);
    const schema = createSchema();

    expect(getSectionName(item, schema)).toBe(' Bold ');
  });
});

// ── generateSectionId ──────────────────────────────────────────────

describe('generateSectionId', () => {
  it('returns existing id when present', () => {
    const section = createSection({ id: 'existing' });

    expect(generateSectionId(section)).toBe('existing');
  });

  it('generates new id when force is true', () => {
    const section = createSection({ id: 'existing' });

    const result = generateSectionId(section, true);

    expect(result).not.toBe('existing');
    expect(result.length).toBeGreaterThan(0);
  });

  it('generates id when section has no id', () => {
    const section = createSection({ id: '' });

    const result = generateSectionId(section);

    expect(result).toBeTruthy();
  });
});

// ── generateModelBySchema ──────────────────────────────────────────

describe('generateModelBySchema', () => {
  it('generates model with defaults from settings', () => {
    const schema = createSchema({
      type: 'hero',
      default: { title: 'Default Title' } as any,
      settings: [
        { id: 'heading', type: 'string', default: 'Hello' } as any,
      ],
    });

    const result = generateModelBySchema(schema);

    expect(result.type).toBe('hero');
    expect(result['heading']).toBe('Hello');
    expect((result as any).title).toBe('Default Title');
  });

  it('returns type from schema even with empty settings', () => {
    const schema = createSchema({ type: 'banner', settings: [] });

    const result = generateModelBySchema(schema);

    expect(result.type).toBe('banner');
  });

  it('generates default item for list settings with element schema', () => {
    const schema = createSchema({
      type: 'features',
      settings: [
        {
          id: 'columns',
          type: 'list',
          element: [
            { id: 'title', type: 'string', default: 'Default Title' },
            { id: 'text', type: 'string', default: 'Default Text' },
          ],
        } as any,
      ],
    });

    const result = generateModelBySchema(schema);

    expect(result['columns']).toEqual([{ title: 'Default Title', text: 'Default Text' }]);
  });

  it('uses explicit default for list settings when provided', () => {
    const schema = createSchema({
      type: 'features',
      settings: [
        {
          id: 'items',
          type: 'list',
          element: [{ id: 'name', type: 'string', default: 'X' }],
          default: [{ name: 'A' }, { name: 'B' }],
        } as any,
      ],
    });

    const result = generateModelBySchema(schema);

    expect(result['items']).toEqual([{ name: 'A' }, { name: 'B' }]);
  });

  it('generates empty array for list with no element defaults', () => {
    const schema = createSchema({
      type: 'features',
      settings: [
        {
          id: 'tags',
          type: 'list',
          element: [{ id: 'label', type: 'string' }],
        } as any,
      ],
    });

    const result = generateModelBySchema(schema);

    expect(result['tags']).toEqual([]);
  });
});

// ── mergeSchemas ───────────────────────────────────────────────────

describe('mergeSchemas', () => {
  it('returns low priority when high priority is null', () => {
    const low: SchemasList = { sections: { hero: {} as any }, blocks: {}, objects: {}, shared: {} };

    expect(mergeSchemas(low, null)).toBe(low);
  });

  it('returns high priority when low priority is null', () => {
    const high: SchemasList = { sections: { hero: {} as any }, blocks: {}, objects: {}, shared: {} };

    expect(mergeSchemas(null, high)).toBe(high);
  });

  it('merges with high priority overriding per key', () => {
    const low: SchemasList = {
      sections: { hero: { name: 'low' } as any },
      blocks: { text: { name: 'low' } as any },
      objects: {},
      shared: {},
    };
    const high: SchemasList = {
      sections: { hero: { name: 'high' } as any },
      blocks: {},
      objects: {},
      shared: {},
    };

    const result = mergeSchemas(low, high);

    expect((result.sections['hero'] as any).name).toBe('high');
    expect((result.blocks['text'] as any).name).toBe('low');
  });
});

// ── prepareSchema ──────────────────────────────────────────────────

describe('prepareSchema', () => {
  it('adds shared settings to schema', () => {
    const schema = createSchema({
      settings: [{ id: 'title', type: 'string' } as any],
    });
    const shared = {
      _sections: { settings: [{ id: 'visibility', type: 'checkbox' }] },
    } as any;

    const result = prepareSchema(schema, shared, {}, '_sections');

    expect(result.settings.length).toBe(2);
    expect(result.settings.map(s => s.id)).toContain('visibility');
  });

  it('excludeShared=true skips general shared settings', () => {
    const schema = createSchema({
      excludeShared: true,
      settings: [{ id: 'title', type: 'string' } as any],
    });
    const shared = {
      _sections: { settings: [{ id: 'visibility', type: 'checkbox' }] },
    } as any;

    const result = prepareSchema(schema, shared, {}, '_sections');

    expect(result.settings.length).toBe(1);
    expect(result.settings[0].id).toBe('title');
  });

  it('excludeShared as array excludes specific settings', () => {
    const schema = createSchema({
      excludeShared: ['visibility'],
      settings: [{ id: 'title', type: 'string' } as any],
    });
    const shared = {
      _sections: { settings: [{ id: 'visibility', type: 'checkbox' }, { id: 'anchor', type: 'string' }] },
    } as any;

    const result = prepareSchema(schema, shared, {}, '_sections');

    expect(result.settings.map(s => s.id)).not.toContain('visibility');
    expect(result.settings.map(s => s.id)).toContain('anchor');
  });

  it('includeShared adds named shared groups', () => {
    const schema = createSchema({
      includeShared: ['seo'],
      settings: [{ id: 'title', type: 'string' } as any],
    });
    const shared = {
      seo: { settings: [{ id: 'metaTitle', type: 'string' }] },
    } as any;

    const result = prepareSchema(schema, shared, {}, '_sections');

    expect(result.settings.map(s => s.id)).toContain('metaTitle');
  });

  it('does not duplicate settings already in schema', () => {
    const schema = createSchema({
      settings: [{ id: 'title', type: 'string' } as any],
    });
    const shared = {
      _sections: { settings: [{ id: 'title', type: 'text' }] },
    } as any;

    const result = prepareSchema(schema, shared, {}, '_sections');

    const titleSettings = result.settings.filter(s => s.id === 'title');
    expect(titleSettings.length).toBe(1);
    expect(titleSettings[0].type).toBe('string'); // original preserved
  });

  it('sorts settings by sort property', () => {
    const schema = createSchema({
      settings: [
        { id: 'c', type: 'string', sort: 3 } as any,
        { id: 'a', type: 'string', sort: 1 } as any,
        { id: 'b', type: 'string', sort: 2 } as any,
      ],
    });

    const result = prepareSchema(schema, {}, {}, '_sections');

    expect(result.settings.map(s => s.id)).toEqual(['a', 'b', 'c']);
  });

  it('settings without sort go after those with sort', () => {
    const schema = createSchema({
      settings: [
        { id: 'no-sort', type: 'string' } as any,
        { id: 'sorted', type: 'string', sort: 1 } as any,
      ],
    });

    const result = prepareSchema(schema, {}, {}, '_sections');

    expect(result.settings[0].id).toBe('sorted');
    expect(result.settings[1].id).toBe('no-sort');
  });

  it('applies _controls overrides', () => {
    const schema = createSchema({
      settings: [{ id: 'body', type: 'text' } as any],
    });
    const shared = {
      _controls: { text: { config: { language: 'ru' } } },
    } as any;

    const result = prepareSchema(schema, shared, {}, '_sections');

    expect((result.settings[0] as any).config).toEqual({ language: 'ru' });
  });

  it('applies template controls between _controls and local settings', () => {
    const schema = createSchema({
      settings: [{ id: 'body', type: 'text' } as any],
    });
    const shared = {
      _controls: { text: { config: { language: 'ru' }, globalOnly: true } },
    } as any;
    const templateControls = { text: { config: { language: 'de' }, templateOnly: true } };

    const result = prepareSchema(schema, shared, {}, '_sections', templateControls);

    expect((result.settings[0] as any).config).toEqual({ language: 'de' });
    expect((result.settings[0] as any).globalOnly).toBe(true);     // from _controls
    expect((result.settings[0] as any).templateOnly).toBe(true);   // from template
  });

  it('local settings override template controls', () => {
    const schema = createSchema({
      settings: [{ id: 'body', type: 'text', config: { language: 'en' } } as any],
    });
    const templateControls = { text: { config: { language: 'de' } } };

    const result = prepareSchema(schema, {}, {}, '_sections', templateControls);

    expect((result.settings[0] as any).config).toEqual({ language: 'en' });
  });

  it('works without templateControls (backward compatible)', () => {
    const schema = createSchema({
      settings: [{ id: 'body', type: 'text' } as any],
    });
    const shared = {
      _controls: { text: { config: { language: 'ru' } } },
    } as any;

    const result = prepareSchema(schema, shared, {}, '_sections');

    expect((result.settings[0] as any).config).toEqual({ language: 'ru' });
  });

  it('returns schema unchanged on error', () => {
    const schema = createSchema({
      settings: null as any,
    });

    const result = prepareSchema(schema, {}, {}, '_sections');

    expect(result).toBe(schema);
  });
});
