import { appHelpers } from '@integration/helpers';
import { AppConfig } from '@integration/services';
import { SectionModel, TemplateModel } from '@models/document';

import {
    LINKED_COMPONENT_REFERENCE_TYPE,
    LinkedComponentContentCache,
    LinkedComponentReferenceModel,
    LinkedComponentReferenceSection,
} from '@editor/models/linked-component.model';

export interface LinkedComponentResolutionBoundary {
    placementId: string;
    componentRef: string;
    startIndex: number;
    count: number;
    name?: string;
    usageCount?: number;
}

export interface LinkedComponentResolutionResult {
    template: TemplateModel;
    boundaries: LinkedComponentResolutionBoundary[];
    missingComponentIds: string[];
}

export type LinkedComponentInsertionMode = 'linked' | 'copy';

type IdFactory = (section: SectionModel, path: string) => string;

export function canEditLinkedComponentOriginal(appConfig: AppConfig): boolean {
    return appConfig.getValue('canInsertLinkedComponents') === true
        && appConfig.getValue('canEditLinkedComponents') === true;
}

export function isLinkedComponentReference(value: unknown): value is LinkedComponentReferenceSection {
    if (!value || typeof value !== 'object') {
        return false;
    }

    const keys = Object.keys(value);
    if (keys.length !== 3 || !keys.every(key => key === 'id' || key === 'type' || key === 'componentRef')) {
        return false;
    }

    const candidate = value as Partial<LinkedComponentReferenceModel>;
    return candidate.type === LINKED_COMPONENT_REFERENCE_TYPE
        && typeof candidate.id === 'string'
        && candidate.id.trim().length > 0
        && typeof candidate.componentRef === 'string'
        && candidate.componentRef.trim().length > 0;
}

export function createLinkedComponentReference(componentRef: string, id = createPlacementId()): LinkedComponentReferenceSection {
    return {
        id,
        type: LINKED_COMPONENT_REFERENCE_TYPE,
        componentRef,
    } as LinkedComponentReferenceSection;
}

export function createPlacementId(): string {
    return `componentRef${appHelpers.generateUniqueString(8)}`;
}

export function insertLinkedComponentReference(
    template: TemplateModel,
    componentRef: string,
    insertIndex: number,
    placementId?: string,
): TemplateModel {
    const index = normalizeInsertIndex(insertIndex, template.content.length);
    const reference = createLinkedComponentReference(componentRef, placementId);
    return {
        ...template,
        content: [
            ...template.content.slice(0, index),
            reference,
            ...template.content.slice(index),
        ],
    };
}

export function insertLinkedComponentCopy(
    template: TemplateModel,
    component: TemplateModel,
    insertIndex: number,
    idFactory: IdFactory = freshIdFactory,
): TemplateModel {
    const index = normalizeInsertIndex(insertIndex, template.content.length);
    const sections = component.content.map((section, sectionIndex) =>
        cloneSectionWithIds(section, `section-${sectionIndex}`, idFactory));
    return {
        ...template,
        content: [
            ...template.content.slice(0, index),
            ...sections,
            ...template.content.slice(index),
        ],
    };
}

export function replaceSectionsWithLinkedComponent(
    template: TemplateModel,
    sectionIds: string[],
    componentRef: string,
    placementId?: string,
): TemplateModel {
    if (!areSectionsContiguous(template, sectionIds)) {
        return template;
    }

    const selected = new Set(sectionIds);
    const firstIndex = template.content.findIndex(section => selected.has(section.id));
    return {
        ...template,
        content: [
            ...template.content.slice(0, firstIndex),
            createLinkedComponentReference(componentRef, placementId),
            ...template.content.slice(firstIndex + selected.size),
        ],
    };
}

export function areSectionsContiguous(template: TemplateModel, sectionIds: string[]): boolean {
    const selected = new Set(sectionIds);
    if (selected.size === 0 || selected.size !== sectionIds.length) {
        return false;
    }

    const indices = template.content
        .map((section, index) => selected.has(section.id) ? index : -1)
        .filter(index => index >= 0);

    return indices.length === selected.size
        && indices.every((index, position) => position === 0 || index === indices[position - 1] + 1);
}

export function detachLinkedComponent(
    template: TemplateModel,
    placementId: string,
    component: TemplateModel,
    idFactory: IdFactory = freshIdFactory,
): TemplateModel {
    const referenceIndex = template.content.findIndex(section => section.id === placementId && isLinkedComponentReference(section));
    if (referenceIndex < 0) {
        return template;
    }

    const detachedSections = component.content.map((section, sectionIndex) =>
        cloneSectionWithIds(section, `section-${sectionIndex}`, idFactory));
    return {
        ...template,
        content: [
            ...template.content.slice(0, referenceIndex),
            ...detachedSections,
            ...template.content.slice(referenceIndex + 1),
        ],
    };
}

export function resolveLinkedComponents(
    rawTemplate: TemplateModel,
    contents: LinkedComponentContentCache,
): LinkedComponentResolutionResult {
    const resolvedContent: SectionModel[] = [];
    const boundaries: LinkedComponentResolutionBoundary[] = [];
    const missing = new Set<string>();

    rawTemplate.content.forEach(section => {
        if (!isLinkedComponentReference(section)) {
            resolvedContent.push(section);
            return;
        }

        const linkedTemplate = contents[section.componentRef];
        if (!linkedTemplate) {
            missing.add(section.componentRef);
            return;
        }

        const startIndex = resolvedContent.length;
        linkedTemplate.content.forEach((linkedSection, sectionIndex) => {
            const clone = cloneSectionWithIds(
                linkedSection,
                `section-${sectionIndex}`,
                deterministicIdFactory(section.id),
            );
            resolvedContent.push(clone);
        });

        boundaries.push({
            placementId: section.id,
            componentRef: section.componentRef,
            startIndex,
            count: linkedTemplate.content.length,
        });
    });

    return {
        template: { ...rawTemplate, content: resolvedContent },
        boundaries,
        missingComponentIds: [...missing],
    };
}

export function cloneSectionWithFreshIds(section: SectionModel): SectionModel {
    return cloneSectionWithIds(section, 'section', freshIdFactory);
}

function cloneSectionWithIds(section: SectionModel, path: string, idFactory: IdFactory): SectionModel {
    const id = idFactory(section, path);
    const clone = {
        ...section,
        id,
    } as SectionModel;

    if (Array.isArray(section.blocks)) {
        clone.blocks = section.blocks.map((block, index) =>
            cloneSectionWithIds(block, `${path}-block-${index}`, idFactory));
    }

    if (section['__id']) {
        clone['__id'] = id;
    }

    return clone;
}

function deterministicIdFactory(placementId: string): IdFactory {
    const placementToken = Array.from(new TextEncoder().encode(placementId), byte =>
        byte.toString(16).padStart(2, '0')).join('');
    return (_section, path) => `lc${placementToken}${appHelpers.onlyLettersAndDigits(path)}`;
}

function freshIdFactory(section: SectionModel): string {
    return appHelpers.onlyLettersAndDigits(`${section.type || 'item'}${appHelpers.generateUniqueString(8)}`);
}

function normalizeInsertIndex(index: number, length: number): number {
    if (index < 0 || index > length) {
        return length;
    }
    return index;
}
