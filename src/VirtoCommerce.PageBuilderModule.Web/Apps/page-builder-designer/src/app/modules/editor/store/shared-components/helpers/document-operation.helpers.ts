import { areSectionsContiguous, isSharedComponentReference } from '@editor/helpers';
import { SectionModel, TemplateModel } from '@models/document';

export interface DocumentOperationContext {
  templateKey: string;
  sharedComponentId: string;
}

export interface InsertionAnchor {
  beforeId: string | null;
  afterId: string | null;
}

export interface SelectedSectionsResult {
  sections: SectionModel[];
  error: string | null;
}

export function isSameDocument(
  origin: DocumentOperationContext,
  templateKey: string,
  sharedComponentId: string,
): boolean {
  return origin.templateKey === templateKey && origin.sharedComponentId === sharedComponentId;
}

export function createInsertionAnchor(template: TemplateModel, insertIndex: number): InsertionAnchor {
  const index = insertIndex < 0 || insertIndex > template.content.length ? template.content.length : insertIndex;
  return {
    beforeId: index > 0 ? template.content[index - 1].id : null,
    afterId: index < template.content.length ? template.content[index].id : null,
  };
}

export function resolveInsertionAnchor(template: TemplateModel, anchor: InsertionAnchor): number | null {
  const beforeIndex =
    anchor.beforeId === null ? -1 : template.content.findIndex((section) => section.id === anchor.beforeId);
  const afterIndex =
    anchor.afterId === null ? -1 : template.content.findIndex((section) => section.id === anchor.afterId);

  if (anchor.beforeId !== null && beforeIndex < 0) {
    return null;
  }
  if (anchor.afterId !== null && afterIndex < 0) {
    return null;
  }
  if (anchor.beforeId !== null && anchor.afterId !== null) {
    return afterIndex === beforeIndex + 1 ? afterIndex : null;
  }
  if (anchor.beforeId !== null) {
    return beforeIndex + 1;
  }
  if (anchor.afterId !== null) {
    return afterIndex;
  }
  return template.content.length === 0 ? 0 : null;
}

export function getSelectedSections(template: TemplateModel, selectedIds: string[]): SelectedSectionsResult {
  const selectedSet = new Set(selectedIds);
  const sections = template.content.filter((section) => selectedSet.has(section.id));
  if (sections.length !== selectedIds.length || sections.some(isSharedComponentReference)) {
    return {
      sections: [],
      error: 'Select only independent sections to create a Shared Component',
    };
  }

  if (!areSectionsContiguous(template, selectedIds)) {
    return {
      sections: [],
      error: 'Select adjacent sections to create a Shared Component',
    };
  }

  return { sections, error: null };
}

export function sameIds(left: string[], right: string[]): boolean {
  return left.length === right.length && left.every((id, index) => id === right[index]);
}

export function sameSectionRevision(submitted: SectionModel[], current: SectionModel[]): boolean {
  return submitted.length === current.length && submitted.every((section, index) => section === current[index]);
}

export function hasSharedComponentPlacement(template: TemplateModel, sectionId: string, componentId: string): boolean {
  const section = template.content.find((item) => item.id === sectionId);
  return isSharedComponentReference(section) && section.componentRef === componentId;
}
