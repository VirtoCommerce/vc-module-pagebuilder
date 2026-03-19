import { appHelpers } from '@integration/helpers';
import { FilesDescriptor, SectionPropertyDescriptor } from '@models/controls';
import { PageModel, SectionModel, SectionSchema, TemplateModel } from '@models/document';
import { ObjectsSchemasList, SchemasList, } from '@editor/models';

// todo: refactor these
// replace section/block in collection can be extracted and done with lodash

export function addItemToTemplate(schema: SectionSchema, template: TemplateModel, section: SectionModel | null, insertIndex: number): {
    template: TemplateModel,
    sectionId: string,
    blockId?: string
} {
    const model = generateModelBySchema(schema);
    model.id = generateSectionId(model); // id generator center requried

    if (!section) {
        const index = insertIndex === -1 ? template.content.length : insertIndex;
        return {
            template: {
                ...template,
                content: [
                    ...template.content.slice(0, index),
                    model,
                    ...template.content.slice(index),
                ]
            },
            sectionId: model.id
        };
    } else {
        const sectionIndex = template.content.findIndex(item => item.id === section.id);
        if (sectionIndex === -1) {
            return { template, sectionId: section.id };
        }
        const blocks = section.blocks || [];
        return {
            template: {
                ...template,
                content: [
                    ...template.content.slice(0, sectionIndex),
                    {
                        ...section,
                        blocks: [
                            ...blocks,
                            model
                        ]
                    },
                    ...template.content.slice(sectionIndex + 1)
                ]
            },
            sectionId: section.id,
            blockId: model.id
        };
    }
}

function reorderSectionsInList(list: SectionModel[], currentIndex: number, previousIndex: number, sectionIds: string[]): SectionModel[] {
    if (sectionIds.length === 0) {
        const newList = [...list];
        const item = newList[previousIndex];
        newList.splice(previousIndex, 1);
        newList.splice(currentIndex, 0, item);
        return newList
    } else {
        const delta = previousIndex > currentIndex ? -1 : 0;

        let firstUntouchedElement: SectionModel | null = null;
        for (let i = currentIndex + delta; i >= 0; i--) {
            if (sectionIds.indexOf(list[i].id) === -1) {
                firstUntouchedElement = list[i];
                break;
            }
        }

        const elementsToPaste = list.filter(x => sectionIds.indexOf(x.id) !== -1);
        const newList = list.filter(x => sectionIds.indexOf(x.id) === -1);
        const newIndex = firstUntouchedElement === null ? 0 : newList.indexOf(firstUntouchedElement) + 1;
        newList.splice(newIndex, 0, ...elementsToPaste);
        return newList;
    }
}

export function reorderSections(template: TemplateModel, currentIndex: number, previousIndex: number, sectionIds: string[]): TemplateModel {
    return {
        ...template,
        content: reorderSectionsInList(template.content, currentIndex, previousIndex, sectionIds)
    };
}

export function reorderBlocks(template: TemplateModel, section: SectionModel, currentIndex: number, previousIndex: number, blockIds: string[]): TemplateModel {
    const sectionIndex = template.content.findIndex(item => (item.id) === section.id);
    return {
        ...template,
        content: [
            ...template.content.slice(0, sectionIndex),
            {
                ...section,
                blocks: reorderSectionsInList(section.blocks, currentIndex, previousIndex, blockIds)
            },
            ...template.content.slice(sectionIndex + 1)
        ]
    };
}

export function generateSectionId(section: SectionModel, force: boolean = false): string {
    let result = section.id ? '' + section.id : undefined;
    if (force || !result) {
        result = appHelpers.onlyLettersAndDigits(`${section.type}${appHelpers.generateUniqueString(4)}`);
    }
    return result;
}

export function generateModelBySchema(schema: SectionSchema): SectionModel {
    const result: SectionModel = {
        ...schema.default,
        ...generateModelBySettings(schema.settings, 'default'),
        type: schema.type
    };
    return result;
}

export function generatePreviewBySchema(schema: SectionSchema): SectionModel {
    const result: SectionModel = {
        ...schema.preview || schema.default,
        ...generateModelBySettings(schema.settings, 'preview'),
        type: schema.type
    };
    return result;
}

export function applySectionChanges(template: TemplateModel, changes: Partial<SectionModel>, sectionId: string): TemplateModel {
    const sectionIndex = template.content.findIndex(item => item.id === sectionId);
    const section = template.content[sectionIndex];
    return {
        ...template,
        content: [
            ...template.content.slice(0, sectionIndex),
            <SectionModel>{
                ...section,
                ...changes
            },
            ...template.content.slice(sectionIndex + 1)
        ]
    };
}

export function applySettingsChanges(template: TemplateModel, changes: Partial<SectionModel>): TemplateModel {
    return {
        ...template,
        settings: <SectionModel>{
            ...template.settings,
            ...changes
        }
    };
}

export function applyBlockChanges(template: TemplateModel, changes: Partial<SectionModel>, sectionId: string, blockId: string): TemplateModel {
    const sectionIndex = template.content.findIndex(item => item.id === sectionId);
    const section = template.content[sectionIndex];
    const blockIndex = section.blocks.findIndex(item => item.id === blockId);
    const block = section.blocks[blockIndex];
    const newSection = {
        ...section,
        blocks: [
            ...section.blocks.slice(0, blockIndex),
            <SectionModel>{
                ...block,
                ...changes
            },
            ...section.blocks.slice(blockIndex + 1)
        ]
    };
    return {
        ...template,
        content: [
            ...template.content.slice(0, sectionIndex),
            newSection,
            ...template.content.slice(sectionIndex + 1)
        ]
    };
}

export function duplicateBlock(
    template: TemplateModel,
    sectionId: string,
    blockId?: string): {
        template: TemplateModel,
        sectionId: string,
        blockId: string
    } {
    const sectionIndex = template.content.findIndex(item => item.id === sectionId);
    const section = template.content[sectionIndex];
    const blockIndex = section.blocks.findIndex(item => item.id === blockId);
    const block = section.blocks[blockIndex];
    const newId = generateSectionId(block, true);
    const new__id = block['__id'] ? newId : undefined;
    const newBlock = <any>{ ...block, id: newId, __id: new__id };
    const newSection = {
        ...section,
        blocks: [
            ...section.blocks.slice(0, blockIndex + 1),
            newBlock,
            ...section.blocks.slice(blockIndex + 1)
        ]
    };
    return {
        template: {
            ...template,
            content: [
                ...template.content.slice(0, sectionIndex),
                newSection,
                ...template.content.slice(sectionIndex + 1)
            ]
        },
        sectionId,
        blockId: newBlock.id
    };
}

export function duplicateSection(template: TemplateModel, sectionId: string): {
    template: TemplateModel,
    sectionId: string,
    blockId?: string
} {
    const sectionIndex = template.content.findIndex(item => item.id === sectionId);
    const section = template.content[sectionIndex];
    const newId = generateSectionId(section, true);
    const new__id = section['__id'] ? newId : undefined;
    const newSection = <any>{ ...section, id: generateSectionId(section, true), __id: new__id };
    return {
        template: {
            ...template,
            content: [
                ...template.content.slice(0, sectionIndex + 1),
                newSection,
                ...template.content.slice(sectionIndex + 1)
            ]
        },
        sectionId: newSection.id
    };
}

export function removeBlock(template: TemplateModel, sectionId: string, blockId: string): TemplateModel {
    const sectionIndex = template.content.findIndex(item => item.id === sectionId);
    const section = template.content[sectionIndex];
    const blockIndex = section.blocks.findIndex(item => item.id === blockId);
    const newSection = {
        ...section,
        blocks: [
            ...section.blocks.slice(0, blockIndex),
            ...section.blocks.slice(blockIndex + 1)
        ]
    };
    return {
        ...template,
        content: [
            ...template.content.slice(0, sectionIndex),
            newSection,
            ...template.content.slice(sectionIndex + 1)
        ]
    };
}

export function removeSection(template: TemplateModel, sectionId: string): TemplateModel {
    const sectionIndex = template.content.findIndex(item => item.id === sectionId);
    return {
        ...template,
        content: [
            ...template.content.slice(0, sectionIndex),
            ...template.content.slice(sectionIndex + 1)
        ]
    };
}

function generateModelBySettings(settings: SectionPropertyDescriptor[], mode: 'default' | 'preview' = 'default'): any {
    // todo: consder object and collections too
    return (settings || []).map(x => {
        if (isElementType(x) && !isListType(x)) {
            let e = x as { element: SectionPropertyDescriptor[] };
            let currentValue = x[mode] || x.default || {};
            let valueFromProps = generateModelBySettings(e.element, mode);
            return {
                ...x,
                [mode]: {
                    ...currentValue,
                    ...valueFromProps
                }
            }
        }
        return x;
    }).reduce((result, value) => {
        let res = result;
        if (value.hasOwnProperty(mode) || value.hasOwnProperty('default')) {
            res = {
                ...res,
                [value.id]: value[mode] || value.default
            };
        }
        return res;
    }, {});
}


// export function getTemplateName(templateSchema: TemplateSchema | null, key: string | null = null) {
//     return templateSchema?.name || key || 'Select template';
// }

export function prepareTemplate(template: TemplateModel): TemplateModel {
    const result = {
        ...template,
        content: template?.content?.map(section => {
            const res = {
                ...section,
                id: generateSectionId(section)
            };
            if (section.blocks) {
                res.blocks = section.blocks.map((block, _) => ({
                    ...block,
                    id: generateSectionId(block)
                }));
            }
            return res;
        }) || []
    };

    return result;
}

export function convertTemplateIntoCorrectVersion(template: TemplateModel | SectionModel[] | PageModel | null): TemplateModel | null {
    if (typeof template === 'string') {
        template = JSON.parse(template);
    }

    if (!template) {
        return { settings: { id: '', type: 'settings', displayName: '', hidden: false, blocks: [] }, content: [] };
    }

    // If template is already a TemplateModel, return it
    if ('settings' in template && 'content' in template) {
        return template as TemplateModel;
    } else if (Array.isArray(template)) {
        // this is the old template format
        // convert it to the new format
        const [settings, ...content] = template;
        return { settings: settings || {}, content: content || [], version: 1 };
    } else if ('pageContent' in template) {
        const { pageContent, id, storeId, permalink, name, cultureName } = template as PageModel;

        const parsedContent = JSON.parse(pageContent || '{}');
        const { settings, content } = parsedContent;

        const result: TemplateModel = {
            settings: {
                type: 'settings',
                displayName: name,
                ...settings,
                id, storeId, permalink, name, cultureName,
            } as SectionModel,
            content,
        };

        return result;
    }

    return null;
}

export function prepareTemplateForSave(template: TemplateModel): TemplateModel | SectionModel[] {
    if (template.version === 1) {
        return [template.settings, ...template.content];
    }
    return template;
}

export function getSectionName(item: SectionModel | null, schema: SectionSchema | null, defaultValue: string | null = null): string {
    let resultName = defaultValue || schema?.name || item?.type || '[no name]';
    if (!!schema && !!item) {
        if (schema.displayField) {
            const result = appHelpers.getValueByPath(item, schema.displayField);
            if (!!result) {
                resultName = <string>result;
            }
        } else {
            const result = <string>item['name'];
            if (!!result) {
                resultName = result;
            }
        }
    }
    return appHelpers.stripHtmlTags(resultName);
}

export function insertBlock(template: TemplateModel, sectionId: string, blockId: string | null, block: SectionModel, direction: number): {
    template: TemplateModel,
    sectionId: string,
    blockId?: string
} {

    const newBlock = {
        ...block,
        id: generateSectionId(block, true)
    };

    const sectionIndex = template.content.findIndex(item => item.id == sectionId);
    if (sectionIndex !== -1) {
        const section = template.content[sectionIndex];
        const blockIndex = direction === -1 ? -1 : section.blocks.findIndex(item => item.id == blockId);
        const blocks = section.blocks || [];
        const newSection = blockIndex !== -1
            ? {
                ...section,
                blocks: [
                    ...blocks.slice(0, blockIndex + direction),
                    newBlock,
                    ...blocks.slice(blockIndex + direction)
                ]
            }
            : {
                ...section,
                blocks: [
                    ...blocks,
                    newBlock
                ]
            };
        return {
            template: {
                ...template,
                content: [
                    ...template.content.slice(0, sectionIndex),
                    newSection,
                    ...template.content.slice(sectionIndex + 1)
                ]
            },
            sectionId,
            blockId: newBlock.id
        };
    }
    return { template, sectionId };
}

export function insertSection(template: TemplateModel, sectionId: string | null, section: SectionModel, direction: number): {
    template: TemplateModel,
    sectionId: string,
    blockId?: string
} {

    const newSection = {
        ...section,
        id: generateSectionId(section, true)
    };

    const sectionIndex = direction === -1 ? -1 : template.content.findIndex(item => item.id == sectionId);
    const changedTemplate = {
        ...template,
        content: sectionIndex !== -1
            ? [
                ...template.content.slice(0, sectionIndex + direction),
                newSection,
                ...template.content.slice(sectionIndex + direction)
            ]
            : [
                ...template.content,
                newSection
            ]
    };
    return {
        template: changedTemplate,
        sectionId: newSection.id
    };
}

function isElementType(setting: SectionPropertyDescriptor): boolean {
    return ['object', 'list', 'images', 'files'].indexOf(setting.type) !== -1 &&
        (!!(<any>setting).element || !!(<any>setting).elementDescriptor);
}

function isListType(setting: SectionPropertyDescriptor): boolean {
    if (setting.type === 'list') return true;
    const fileSetting = <FilesDescriptor>setting;
    if ((setting.type === 'files' || setting.type === 'images') && fileSetting.multiple !== false) {
        return true;
    }
    return false;
}


function fillElementProperty(setting: SectionPropertyDescriptor, objects: ObjectsSchemasList): SectionPropertyDescriptor {
    if (!isElementType(setting)) return setting;
    let result = setting as { element: SectionPropertyDescriptor[], elementDescriptor?: string };
    if (!!result.elementDescriptor) {
        const shared = objects[result.elementDescriptor] || { settings: [] };
        const given = result.element || [];
        result = {
            element: [
                ...shared.settings.filter(x => !given.some(y => y.id === x.id)),
                ...given
            ]
        };
    }

    const element = result.element?.map(x => fillElementProperty(x, objects));

    return {
        ...setting,
        element
    };
}

export function mergeSchemas(lowPrioritySchemas: SchemasList | null, highPrioritySchemas: SchemasList | null): SchemasList {
    if (!highPrioritySchemas) {
        return lowPrioritySchemas!;
    }
    if (!lowPrioritySchemas) {
        return highPrioritySchemas;
    }

    const result = {
        sections: { ...lowPrioritySchemas.sections, ...highPrioritySchemas.sections },
        objects: { ...lowPrioritySchemas.objects, ...highPrioritySchemas.objects },
        blocks: { ...lowPrioritySchemas.blocks, ...highPrioritySchemas.blocks },
        shared: { ...lowPrioritySchemas.shared, ...highPrioritySchemas.shared },
    };
    return result;
}

export function prepareSchema(schema: SectionSchema,
    shared: ObjectsSchemasList,
    objects: ObjectsSchemasList,
    itemType: '_sections' | '_blocks'): SectionSchema {
    try {
        const generalSettings = schema.excludeShared !== true
            ? shared?.[itemType]?.settings?.filter(x => !schema.settings?.find(s => s.id === x.id))
            : [];
        const itemSettings = schema?.includeShared?.map(name => shared?.[name]?.settings)?.flat(1)
            .filter(x => !!x && !schema.settings?.find(s => s.id === x.id));

        const orderedResult = {
            ...schema,
            settings: [
                ...schema.settings,
                ...generalSettings || [],
                ...itemSettings || []
            ].filter(x =>
                schema.excludeShared === true || (<string[]>schema.excludeShared || []).indexOf(x.id) === -1
            ).map(x => fillElementProperty(x, objects)).sort((a, b) => {
                if (a.sort !== undefined && b.sort !== undefined) {
                    return a.sort - b.sort;
                }
                if (a.sort !== undefined) {
                    return -1;
                }
                if (b.sort !== undefined) {
                    return 1;
                }
                return 0;
            })
        };

        // here we should take general settings for shared["_controls"] by names
        // example: the editor with type 'text' for all block should have one property changed
        // So _controls may look like
        // {
        //    "text": {
        //        "config": {
        //        "language": "ru"
        //        }
        //    }
        // }
        // after the next operation all editors will have this setting
        const result = {
            ...orderedResult,
            settings: orderedResult.settings.map(x => ({
                ...(<any>shared?.['_controls'])?.[x.type],
                ...x,
            })),
        };

        return result;
    } catch (e) {
        return schema;
    }
}
