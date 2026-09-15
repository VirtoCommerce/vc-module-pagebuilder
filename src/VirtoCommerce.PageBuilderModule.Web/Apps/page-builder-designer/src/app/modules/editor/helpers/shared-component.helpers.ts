import { appHelpers } from '@integration/helpers';
import { AppConfig } from '@integration/services';
import { SectionModel, TemplateModel } from '@models/document';

import {
    SHARED_COMPONENT_REFERENCE_TYPE,
    SharedComponentContentCache,
    SharedComponentReferenceModel,
    SharedComponentReferenceSection,
    SharedComponentUsagePage,
} from '@editor/models/shared-component.model';

export interface SharedComponentResolutionBoundary {
    placementId: string;
    componentRef: string;
    startIndex: number;
    count: number;
    label?: string;
}

export interface SharedComponentResolutionResult {
    template: TemplateModel;
    boundaries: SharedComponentResolutionBoundary[];
    missingComponentIds: string[];
}

export type SharedComponentInsertionMode = 'shared' | 'copy';

type IdFactory = (section: SectionModel, path: string) => string;

export function canEditSharedComponentOriginal(appConfig: AppConfig): boolean {
    return appConfig.getValue('canInsertSharedComponents') === true
        && appConfig.getValue('canEditSharedComponents') === true;
}

export function canOpenSharedComponentUsagePage<T extends Pick<SharedComponentUsagePage, 'id' | 'status'>>(
    page: T,
): page is T & { id: string } {
    return typeof page.id === 'string' && page.id.trim().length > 0 && page.status !== 'Archived';
}

export function isSharedComponentReference(value: unknown): value is SharedComponentReferenceSection {
    if (!value || typeof value !== 'object') {
        return false;
    }

    const keys = Object.keys(value);
    if (keys.length !== 3 || !keys.every(key => key === 'id' || key === 'type' || key === 'componentRef')) {
        return false;
    }

    const candidate = value as Partial<SharedComponentReferenceModel>;
    return candidate.type === SHARED_COMPONENT_REFERENCE_TYPE
        && typeof candidate.id === 'string'
        && candidate.id.trim().length > 0
        && typeof candidate.componentRef === 'string'
        && candidate.componentRef.trim().length > 0;
}

export function createSharedComponentReference(componentRef: string, id = createPlacementId()): SharedComponentReferenceSection {
    return {
        id,
        type: SHARED_COMPONENT_REFERENCE_TYPE,
        componentRef,
    } as SharedComponentReferenceSection;
}

export function createPlacementId(): string {
    return `componentRef${appHelpers.generateUniqueString(8)}`;
}

export function insertSharedComponentReference(
    template: TemplateModel,
    componentRef: string,
    insertIndex: number,
    placementId?: string,
): TemplateModel {
    const index = normalizeInsertIndex(insertIndex, template.content.length);
    const reference = createSharedComponentReference(componentRef, placementId);
    return {
        ...template,
        content: [
            ...template.content.slice(0, index),
            reference,
            ...template.content.slice(index),
        ],
    };
}

export function insertSharedComponentCopy(
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

export function replaceSectionsWithSharedComponent(
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
            createSharedComponentReference(componentRef, placementId),
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

export function detachSharedComponent(
    template: TemplateModel,
    placementId: string,
    component: TemplateModel,
    idFactory: IdFactory = freshIdFactory,
): TemplateModel {
    const referenceIndex = template.content.findIndex(section => section.id === placementId && isSharedComponentReference(section));
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

export function resolveSharedComponents(
    rawTemplate: TemplateModel,
    contents: SharedComponentContentCache,
): SharedComponentResolutionResult {
    const resolvedContent: SectionModel[] = [];
    const boundaries: SharedComponentResolutionBoundary[] = [];
    const missing = new Set<string>();

    rawTemplate.content.forEach(section => {
        if (!isSharedComponentReference(section)) {
            resolvedContent.push(section);
            return;
        }

        const sharedComponentTemplate = contents[section.componentRef];
        if (!sharedComponentTemplate) {
            missing.add(section.componentRef);
            return;
        }

        const startIndex = resolvedContent.length;
        sharedComponentTemplate.content.forEach((sharedComponentSection, sectionIndex) => {
            const clone = cloneSectionWithIds(
                sharedComponentSection,
                `section-${sectionIndex}`,
                deterministicIdFactory(section.id),
            );
            resolvedContent.push(clone);
        });

        boundaries.push({
            placementId: section.id,
            componentRef: section.componentRef,
            startIndex,
            count: sharedComponentTemplate.content.length,
        });
    });

    return {
        template: { ...rawTemplate, content: resolvedContent },
        boundaries,
        missingComponentIds: [...missing],
    };
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
