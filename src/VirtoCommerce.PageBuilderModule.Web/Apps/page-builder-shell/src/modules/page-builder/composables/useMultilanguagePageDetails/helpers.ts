import type {
  MultilanguagePageBatchResult,
  MultilanguagePageLanguage,
} from "../../../../api_client/virtocommerce.pagebuildermodule";

// Matches MultilanguagePageBatchResult.FailedReason on the backend.
const failedReason = "Failed";

/** A root with versions is archived like a Landing Page; only a root without versions is deleted. */
export function hasLanguageVersions(languages?: MultilanguagePageLanguage[]): boolean {
  return (languages ?? []).some((x) => !!x.page);
}

/** A Multilanguage Page row is archived when it has versions and all of them are archived. */
export function isMultilanguageRowArchived(languages?: MultilanguagePageLanguage[]): boolean {
  const versions = (languages ?? []).filter((x) => !!x.page);
  return versions.length > 0 && versions.every((x) => x.status === "Archived");
}

export interface IPermalinkTarget {
  permalink?: string;
  cultureName?: string;
}

function normalizePermalink(permalink?: string): string {
  return `/${(permalink ?? "").trim().replace(/^\/+/, "")}`;
}

/** Usage is checked for a new page and when the permalink or language changed, not on every save. */
export function isPermalinkCheckNeeded(saved: IPermalinkTarget | undefined, current: IPermalinkTarget): boolean {
  if (!saved) {
    return true;
  }

  return (
    normalizePermalink(saved.permalink) !== normalizePermalink(current.permalink) ||
    (saved.cultureName ?? "").toLowerCase() !== (current.cultureName ?? "").toLowerCase()
  );
}

/** Language lists for the batch summary; failed languages are reported apart from the skipped ones. */
export function summarizeBatchResult(result: MultilanguagePageBatchResult): {
  processed: string;
  skipped: string;
  failed: string;
} {
  const skipped = result.skipped ?? [];
  return {
    processed: result.processedCultureNames?.join(", ") || "—",
    skipped:
      skipped
        .filter((x) => x.reason !== failedReason)
        .map((x) => x.cultureName)
        .join(", ") || "—",
    failed: skipped
      .filter((x) => x.reason === failedReason)
      .map((x) => x.cultureName)
      .join(", "),
  };
}
