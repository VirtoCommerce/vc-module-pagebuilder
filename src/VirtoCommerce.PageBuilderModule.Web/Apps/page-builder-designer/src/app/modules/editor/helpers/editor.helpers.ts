import { appHelpers } from '@integration/helpers';
import { FilesDescriptor, SectionPropertyDescriptor } from '@models/controls';
import { PageModel, SectionModel, SectionSchema, TemplateModel } from '@models/document';
import { ObjectsSchemasList, SchemasList, } from '@editor/models';

// Immutable array helpers for section/block manipulation

function replaceAt<T>(arr: T[], index: number, item: T): T[] {
  return arr.map((el, i) => i === index ? item : el);
}

function insertAt<T>(arr: T[], index: number, ...items: T[]): T[] {
  return [...arr.slice(0, index), ...items, ...arr.slice(index)];
}

function removeAt<T>(arr: T[], index: number): T[] {
  return arr.filter((_, i) => i !== index);
}

export function addItemToTemplate(schema: SectionSchema, template: TemplateModel, section: SectionModel | null, insertIndex: number): {
  template: TemplateModel,
  sectionId: string,
  blockId?: string
} {
  const model = generateModelBySchema(schema);
  model.id = generateSectionId(model);

  if (!section) {
    const index = insertIndex === -1 ? template.content.length : insertIndex;
    return {
      template: { ...template, content: insertAt(template.content, index, model) },
      sectionId: model.id
    };
  }

  const sectionIndex = template.content.findIndex(item => item.id === section.id);
  if (sectionIndex === -1) {
    return { template, sectionId: section.id };
  }

  const newSection = { ...section, blocks: [...(section.blocks || []), model] };
  return {
    template: { ...template, content: replaceAt(template.content, sectionIndex, newSection) },
    sectionId: section.id,
    blockId: model.id
  };
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
      if (!sectionIds.includes(list[i].id)) {
        firstUntouchedElement = list[i];
        break;
      }
    }

    const elementsToPaste = list.filter(x => sectionIds.includes(x.id));
    const newList = list.filter(x => !sectionIds.includes(x.id));
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
  const sectionIndex = template.content.findIndex(item => item.id === section.id);
  const newSection = { ...section, blocks: reorderSectionsInList(section.blocks, currentIndex, previousIndex, blockIds) };
  return { ...template, content: replaceAt(template.content, sectionIndex, newSection) };
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
  const newSection = <SectionModel>{ ...template.content[sectionIndex], ...changes };
  return { ...template, content: replaceAt(template.content, sectionIndex, newSection) };
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
  const newBlock = <SectionModel>{ ...section.blocks[blockIndex], ...changes };
  const newSection = { ...section, blocks: replaceAt(section.blocks, blockIndex, newBlock) };
  return { ...template, content: replaceAt(template.content, sectionIndex, newSection) };
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
  const newBlock = <any>{ ...block, id: newId, __id: block['__id'] ? newId : undefined };
  const newSection = { ...section, blocks: insertAt(section.blocks, blockIndex + 1, newBlock) };
  return {
    template: { ...template, content: replaceAt(template.content, sectionIndex, newSection) },
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
  const newSection = <any>{ ...section, id: newId, __id: section['__id'] ? newId : undefined };
  return {
    template: { ...template, content: insertAt(template.content, sectionIndex + 1, newSection) },
    sectionId: newSection.id
  };
}

export function removeBlock(template: TemplateModel, sectionId: string, blockId: string): TemplateModel {
  const sectionIndex = template.content.findIndex(item => item.id === sectionId);
  const section = template.content[sectionIndex];
  const blockIndex = section.blocks.findIndex(item => item.id === blockId);
  const newSection = { ...section, blocks: removeAt(section.blocks, blockIndex) };
  return { ...template, content: replaceAt(template.content, sectionIndex, newSection) };
}

export function removeSection(template: TemplateModel, sectionId: string): TemplateModel {
  const sectionIndex = template.content.findIndex(item => item.id === sectionId);
  return { ...template, content: removeAt(template.content, sectionIndex) };
}

export function removeSections(template: TemplateModel, sectionIds: string[]): TemplateModel {
  return { ...template, content: template.content.filter(item => !sectionIds.includes(item.id)) };
}

export function removeBlocks(template: TemplateModel, sectionId: string, blockIds: string[]): TemplateModel {
  const sectionIndex = template.content.findIndex(item => item.id === sectionId);
  if (sectionIndex < 0) {
    return template;
  }
  const section = template.content[sectionIndex];
  const newSection = { ...section, blocks: section.blocks.filter(block => !blockIds.includes(block.id)) };
  return { ...template, content: replaceAt(template.content, sectionIndex, newSection) };
}

function generateModelBySettings(settings: SectionPropertyDescriptor[], mode: 'default' | 'preview' = 'default'): any {
  return (settings || []).map(x => {
    if (isElementType(x)) {
      const e = x as { element: SectionPropertyDescriptor[] };
      if (isListType(x)) {
        // For collections: use existing default/preview array, or generate one item from element schema
        const existingValue = x[mode] || x.default;
        if (existingValue) {
          return { ...x, [mode]: existingValue };
        }
        const itemDefaults = generateModelBySettings(e.element, mode);
        return { ...x, [mode]: Object.keys(itemDefaults).length ? [itemDefaults] : [] };
      } else {
        // For objects: merge element defaults into current value
        const currentValue = x[mode] || x.default || {};
        const valueFromProps = generateModelBySettings(e.element, mode);
        return { ...x, [mode]: { ...currentValue, ...valueFromProps } };
      }
    }
    return x;
  }).reduce((result, value) => {
    if (value.hasOwnProperty(mode) || value.hasOwnProperty('default')) {
      return { ...result, [value.id]: value[mode] || value.default };
    }
    return result;
  }, {});
}



export function prepareTemplate(template: TemplateModel): TemplateModel {
  return {
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
    return template;
  } else if (Array.isArray(template)) {
    // this is the old template format
    // convert it to the new format
    const [settings, ...content] = template;
    return { settings: settings || {}, content: content || [], version: 1 };
  } else if ('pageContent' in template) {
    const { pageContent, id, storeId, permalink, name, cultureName } = template;

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
      if (result) {
        resultName = <string>result;
      }
    } else {
      const result = <string>item['name'];
      if (result) {
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
  const newBlock = { ...block, id: generateSectionId(block, true) };
  const sectionIndex = template.content.findIndex(item => item.id == sectionId);
  if (sectionIndex === -1) {
    return { template, sectionId };
  }

  const section = template.content[sectionIndex];
  const blocks = section.blocks || [];
  const blockIndex = direction === -1 ? -1 : blocks.findIndex(item => item.id == blockId);
  const insertIdx = blockIndex === -1 ? blocks.length : blockIndex + direction;
  const newSection = { ...section, blocks: insertAt(blocks, insertIdx, newBlock) };
  return {
    template: { ...template, content: replaceAt(template.content, sectionIndex, newSection) },
    sectionId,
    blockId: newBlock.id
  };
}

export function insertSection(template: TemplateModel, sectionId: string | null, section: SectionModel, direction: number): {
  template: TemplateModel,
  sectionId: string,
  blockId?: string
} {
  const newSection = { ...section, id: generateSectionId(section, true) };
  const sectionIndex = direction === -1 ? -1 : template.content.findIndex(item => item.id == sectionId);
  const insertIdx = sectionIndex === -1 ? template.content.length : sectionIndex + direction;
  return {
    template: { ...template, content: insertAt(template.content, insertIdx, newSection) },
    sectionId: newSection.id
  };
}

function isElementType(setting: SectionPropertyDescriptor): boolean {
  return ['object', 'list', 'images', 'files'].includes(setting.type) &&
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
  if (result.elementDescriptor) {
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

  return {
    sections: { ...lowPrioritySchemas.sections, ...highPrioritySchemas.sections },
    objects: { ...lowPrioritySchemas.objects, ...highPrioritySchemas.objects },
    blocks: { ...lowPrioritySchemas.blocks, ...highPrioritySchemas.blocks },
    shared: { ...lowPrioritySchemas.shared, ...highPrioritySchemas.shared },
  };
}

export function prepareSchema(schema: SectionSchema,
  shared: ObjectsSchemasList,
  objects: ObjectsSchemasList,
  itemType: '_sections' | '_blocks',
  templateControls?: Record<string, any> | null): SectionSchema {
  try {
    const generalSettings = schema.excludeShared === true
      ? []
      : shared?.[itemType]?.settings?.filter(x => !schema.settings?.find(s => s.id === x.id));
    const itemSettings = schema?.includeShared?.map(name => shared?.[name]?.settings)?.flat(1)
      .filter(x => !!x && !schema.settings?.find(s => s.id === x.id));

    const orderedResult = {
      ...schema,
      settings: [
        ...schema.settings,
        ...(generalSettings || []),
        ...(itemSettings || []),
      ].filter(x =>
        schema.excludeShared === true || !(<string[]>schema.excludeShared || []).includes(x.id)
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

    // Control overrides (3 levels, lowest to highest priority):
    // 1. shared._controls   — global defaults for all control types
    // 2. templateControls   — per-template overrides (e.g. page vs blog)
    // 3. x (local)          — inline setting in the section/block schema
    return {
      ...orderedResult,
      settings: orderedResult.settings.map(x => ({
        ...(<any>shared?.['_controls'])?.[x.type],
        ...templateControls?.[x.type],
        ...x,
      })),
    };
  } catch (e) {
    console.log('Error during schema preparation', e);
    return schema;
  }
}
