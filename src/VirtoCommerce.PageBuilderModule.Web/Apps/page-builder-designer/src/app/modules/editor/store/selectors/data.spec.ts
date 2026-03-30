import * as selectors from './data';
import { createTemplate, createSection, createSchema, createSchemasList } from '@app/testing';
import { SectionSchema } from '@models/document';

// ── selectCurrentTemplateModel ────────────────────────────────────

describe('selectCurrentTemplateModel', () => {
    it('returns template by key', () => {
        const t = createTemplate();
        const result = selectors.selectCurrentTemplateModel.projector({ home: t }, 'home');
        expect(result).toBe(t);
    });

    it('returns null when templateKey is empty', () => {
        expect(selectors.selectCurrentTemplateModel.projector({ home: createTemplate() }, '')).toBeNull();
    });

    it('returns undefined for missing key', () => {
        expect(selectors.selectCurrentTemplateModel.projector({}, 'missing')).toBeUndefined();
    });
});

// ── selectFileName ────────────────────────────────────────────────

describe('selectFileName', () => {
    it('extracts file name without extension', () => {
        expect(selectors.selectFileName.projector('templates/home.json')).toBe('home');
    });

    it('handles nested path', () => {
        expect(selectors.selectFileName.projector('a/b/c/layout.liquid')).toBe('layout');
    });

    it('handles file with multiple dots', () => {
        expect(selectors.selectFileName.projector('templates/my.page.json')).toBe('my.page');
    });
});

// ── selectCurrentTemplateName ─────────────────────────────────────

describe('selectCurrentTemplateName', () => {
    it('prefers displayName', () => {
        const model = createTemplate();
        (model.settings as any).displayName = 'Display';
        (model.settings as any).name = 'Name';
        expect(selectors.selectCurrentTemplateName.projector(model, 'file')).toBe('Display');
    });

    it('falls back to name', () => {
        const model = createTemplate();
        (model.settings as any).name = 'Name';
        expect(selectors.selectCurrentTemplateName.projector(model, 'file')).toBe('Name');
    });

    it('falls back to fileName', () => {
        expect(selectors.selectCurrentTemplateName.projector(createTemplate(), 'file')).toBe('file');
    });

    it('falls back to [no name]', () => {
        expect(selectors.selectCurrentTemplateName.projector(null, '')).toBe('[no name]');
    });
});

// ── selectSectionsSchemas ─────────────────────────────────────────

describe('selectSectionsSchemas', () => {
    it('returns empty object when schemas is null', () => {
        expect(selectors.selectSectionsSchemas.projector({ schemas: null, templates: {} })).toEqual({});
    });

    it('returns sections with spread properties', () => {
        const data = {
            schemas: {
                sections: {
                    hero: { group: 'content', groupIcon: 'star', name: 'Hero' },
                    banner: { group: 'content', groupIcon: null, name: 'Banner' },
                },
                blocks: {}, objects: {}, shared: {},
            },
            templates: {},
        } as any;
        const result = selectors.selectSectionsSchemas.projector(data);
        expect(result['banner'].groupIcon).toBe('star');
    });
});

// ── selectCurrentFilteredSectionSchemas ───────────────────────────

describe('selectCurrentFilteredSectionSchemas', () => {
    const sections: SectionSchema[] = [
        createSchema({ type: 'hero', name: 'Hero' }),
        createSchema({ type: 'banner', name: 'Banner' }),
        createSchema({ type: 'header', name: 'Header' }),
    ];

    it('returns all when no filter', () => {
        expect(selectors.selectCurrentFilteredSectionSchemas.projector(sections, null)).toEqual(sections);
    });

    it('filters case-insensitively by type', () => {
        const result = selectors.selectCurrentFilteredSectionSchemas.projector(sections, 'HER');
        expect(result.length).toBe(1);
        expect(result[0].type).toBe('hero');
    });

    it('returns multiple matches', () => {
        const result = selectors.selectCurrentFilteredSectionSchemas.projector(sections, 'he');
        expect(result.length).toBe(2); // hero, header
    });
});

// ── selectGroupedSectionSchemas ───────────────────────────────────

describe('selectGroupedSectionSchemas', () => {
    it('separates grouped and ungrouped items', () => {
        const sections: SectionSchema[] = [
            createSchema({ type: 'hero', name: 'Hero', group: 'content' }),
            createSchema({ type: 'banner', name: 'Banner' }),
        ];
        const result = selectors.selectGroupedSectionSchemas.projector(sections);
        expect(result.groups.length).toBe(1);
        expect(result.groups[0].name).toBe('content');
        expect(result.items.length).toBe(1);
        expect(result.items[0].type).toBe('banner');
    });

    it('returns empty groups and items for empty input', () => {
        const result = selectors.selectGroupedSectionSchemas.projector([]);
        expect(result.groups).toEqual([]);
        expect(result.items).toEqual([]);
    });
});

// ── selectTemplateSettings ────────────────────────────────────────

describe('selectTemplateSettings', () => {
    it('returns settings from template', () => {
        const t = createTemplate();
        expect(selectors.selectTemplateSettings.projector(t)).toBe(t.settings);
    });

    it('returns empty object for null template', () => {
        expect(selectors.selectTemplateSettings.projector(null)).toEqual({});
    });
});

// ── selectSettingsFromRoute ───────────────────────────────────────

describe('selectSettingsFromRoute', () => {
    it('returns null when settingsType is null', () => {
        expect(selectors.selectSettingsFromRoute.projector(null, createTemplate())).toBeNull();
    });

    it('returns template settings when settingsType is not null', () => {
        const t = createTemplate();
        expect(selectors.selectSettingsFromRoute.projector('', t)).toBe(t.settings);
    });
});

// ── selectSettingsSchemaFromRoute ─────────────────────────────────

describe('selectSettingsSchemaFromRoute', () => {
    it('returns null when settingsType is null', () => {
        expect(selectors.selectSettingsSchemaFromRoute.projector(null, {}, null)).toBeNull();
    });

    it('returns settingsSchema for empty settingsType', () => {
        const schema = createSchema({ name: 'Settings' });
        expect(selectors.selectSettingsSchemaFromRoute.projector('', {}, schema)).toBe(schema);
    });

    it('returns schema by type for non-empty settingsType', () => {
        const heroSchema = createSchema({ type: 'hero' });
        const schemas = { hero: heroSchema };
        expect(selectors.selectSettingsSchemaFromRoute.projector('hero', schemas, null)).toBe(heroSchema);
    });
});

// ── selectSectionModelFromRoute ───────────────────────────────────

describe('selectSectionModelFromRoute', () => {
    it('finds section by id', () => {
        const s = createSection({ id: 's1' });
        const t = createTemplate({ content: [s] });
        expect(selectors.selectSectionModelFromRoute.projector('s1', t)).toBe(s);
    });

    it('returns null when sectionId is empty', () => {
        expect(selectors.selectSectionModelFromRoute.projector('', createTemplate())).toBeNull();
    });
});

// ── selectBlockModelFromRoute ─────────────────────────────────────

describe('selectBlockModelFromRoute', () => {
    it('finds block in section', () => {
        const block = createSection({ id: 'b1', type: 'text' });
        const section = createSection({ id: 's1', blocks: [block] });
        expect(selectors.selectBlockModelFromRoute.projector('b1', section)).toBe(block);
    });

    it('returns null when blockId is empty', () => {
        expect(selectors.selectBlockModelFromRoute.projector('', createSection())).toBeNull();
    });

    it('returns null when section is null', () => {
        expect(selectors.selectBlockModelFromRoute.projector('b1', null)).toBeNull();
    });
});

// ── selectCurrentItemForEdit ──────────────────────────────────────

describe('selectCurrentItemForEdit', () => {
    it('prefers block over section', () => {
        const block = createSection({ id: 'b1' });
        const section = createSection({ id: 's1' });
        expect(selectors.selectCurrentItemForEdit.projector(block, section, null)).toBe(block);
    });

    it('falls back to section', () => {
        const section = createSection({ id: 's1' });
        expect(selectors.selectCurrentItemForEdit.projector(null, section, null)).toBe(section);
    });

    it('falls back to settings', () => {
        const settings = createSection({ type: 'settings' });
        expect(selectors.selectCurrentItemForEdit.projector(null, null, settings)).toBe(settings);
    });
});

// ── selectCurrentSchemaForEdit ────────────────────────────────────

describe('selectCurrentSchemaForEdit', () => {
    it('prefers block schema', () => {
        const blockSchema = createSchema({ type: 'text' });
        const sectionSchema = createSchema({ type: 'hero' });
        expect(selectors.selectCurrentSchemaForEdit.projector(blockSchema, sectionSchema, null)).toBe(blockSchema);
    });

    it('falls back to section schema', () => {
        const sectionSchema = createSchema({ type: 'hero' });
        expect(selectors.selectCurrentSchemaForEdit.projector(null, sectionSchema, null)).toBe(sectionSchema);
    });
});

// ── selectRunActionContext ─────────────────────────────────────────

describe('selectRunActionContext', () => {
    it('assembles context from route params', () => {
        const entry = { name: 'Home', key: 'home' } as any;
        const result = selectors.selectRunActionContext.projector('home', entry, '/path', 'page', 'g1');
        expect(result).toEqual({ templateKey: 'home', entry, path: '/path', type: 'page', groupId: 'g1' });
    });
});
