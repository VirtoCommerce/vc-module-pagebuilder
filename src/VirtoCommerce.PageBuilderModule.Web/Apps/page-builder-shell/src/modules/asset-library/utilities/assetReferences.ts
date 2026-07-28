import type {
  AssetReference,
  AssetReferenceDetails,
  AssetReferenceLinkedComponent,
  AssetReferencePage,
} from "../types";

export function createAssetReferenceDetails(references: AssetReference[]): AssetReferenceDetails {
  const referencePages = getDistinctReferencePages(references.flatMap((reference) => reference.pages ?? []));
  const referenceLinkedComponents = getDistinctReferenceLinkedComponents(
    references.flatMap((reference) => reference.linkedComponents ?? []),
  );
  const pageReferencesCount =
    referencePages.length || references.reduce((count, reference) => count + (reference.pageReferencesCount ?? 0), 0);
  const linkedComponentReferencesCount =
    referenceLinkedComponents.length ||
    references.reduce((count, reference) => count + (reference.linkedComponentReferencesCount ?? 0), 0);
  const categorizedReferencesCount = pageReferencesCount + linkedComponentReferencesCount;

  return {
    referencesCount:
      categorizedReferencesCount ||
      references.reduce((count, reference) => count + (reference.referencesCount ?? 0), 0),
    pageReferencesCount,
    linkedComponentReferencesCount,
    referencePages,
    referenceLinkedComponents,
  };
}

export function getDistinctReferencePages(pages: AssetReferencePage[]): AssetReferencePage[] {
  return getDistinctReferences(
    pages,
    (page) => page.id || page.permalink || `${page.name ?? ""}:${page.cultureName ?? ""}`,
  );
}

export function getDistinctReferenceLinkedComponents(
  components: AssetReferenceLinkedComponent[],
): AssetReferenceLinkedComponent[] {
  return getDistinctReferences(components, (component) => component.id || component.name || "");
}

export function getReferencePageNames(pages: AssetReferencePage[]): string[] {
  return getDistinctNames(pages.map((page) => page.name || page.permalink || page.id));
}

export function getReferenceLinkedComponentNames(components: AssetReferenceLinkedComponent[]): string[] {
  return getDistinctNames(components.map((component) => component.name || component.id));
}

function getDistinctReferences<T>(references: T[], getKey: (reference: T) => string): T[] {
  const result: T[] = [];
  const seen = new Set<string>();

  for (const reference of references) {
    const key = getKey(reference);

    if (key && !seen.has(key)) {
      seen.add(key);
      result.push(reference);
    }
  }

  return result;
}

function getDistinctNames(names: Array<string | undefined>): string[] {
  return [...new Set(names.filter((name): name is string => !!name))];
}
