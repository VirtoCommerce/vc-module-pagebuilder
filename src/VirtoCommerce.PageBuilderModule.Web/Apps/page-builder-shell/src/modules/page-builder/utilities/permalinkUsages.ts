import {
  ApiException,
  PageBuilderPermalinkConflict,
  type PageBuilderPermalinkUsage,
} from "../../../api_client/virtocommerce.pagebuildermodule";

// Publishing returns 409 when another page got published at the permalink for the same visitors after the check
// before publishing. Returns the pages it lists, or null for any other error.
export function getConflictsOfRejectedPublish(error: unknown): PageBuilderPermalinkUsage[] | null {
  if (!(error instanceof ApiException) || error.status !== 409) {
    return null;
  }

  try {
    const body = JSON.parse(error.response) as { conflicts?: PageBuilderPermalinkUsage[] };
    return body.conflicts ?? [];
  } catch {
    return [];
  }
}

export interface PublishConflicts {
  // Published pages offered to the same visitors: publishing is refused while they stay published.
  blocking: PageBuilderPermalinkUsage[];
  // Published pages a visitor also gets only when they belong to user groups of both pages.
  possible: PageBuilderPermalinkUsage[];
}

export function getPublishConflicts(usages: PageBuilderPermalinkUsage[] | null | undefined): PublishConflicts {
  const list = usages ?? [];
  return {
    blocking: list.filter((x) => x.conflict === PageBuilderPermalinkConflict.Certain),
    possible: list.filter((x) => x.conflict === PageBuilderPermalinkConflict.Possible),
  };
}

// A page with unpublished changes cannot be unpublished, so the conflict has to be resolved on that page first.
export function canUnpublishAll(usages: PageBuilderPermalinkUsage[]): boolean {
  return usages.every((x) => x.canUnpublish !== false);
}

// Pages in the same language, or in any language, are the ones the storefront can mix up.
export function getUsagesInLanguage(
  usages: PageBuilderPermalinkUsage[] | null | undefined,
  cultureName: string | null | undefined,
): PageBuilderPermalinkUsage[] {
  return (usages ?? []).filter(
    (x) => !x.cultureName || !cultureName || x.cultureName.toLowerCase() === cultureName.toLowerCase(),
  );
}

export function formatPageNames(usages: PageBuilderPermalinkUsage[]): string {
  return usages.map((x) => `"${x.name || x.groupId}"`).join(", ");
}
