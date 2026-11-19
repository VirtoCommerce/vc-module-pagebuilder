import type {
  MultilanguagePageBatchResult,
  MultilanguagePageLanguage,
} from "../../../../api_client/virtocommerce.pagebuildermodule";

// Match MultilanguagePageBatchResult.FailedReason and PermalinkConflictReason on the backend.
const failedReason = "Failed";
const permalinkConflictReason = "PermalinkConflict";

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
        .filter((x) => x.reason !== failedReason && x.reason !== permalinkConflictReason)
        .map((x) => x.cultureName)
        .join(", ") || "—",
    failed: skipped
      .filter((x) => x.reason === failedReason)
      .map((x) => x.cultureName)
      .join(", "),
  };
}

/** Languages not published because another published page would be replaced; empty when there are none. */
export function getConflictedLanguages(result: MultilanguagePageBatchResult): string {
  return (result.skipped ?? [])
    .filter((x) => x.reason === permalinkConflictReason)
    .map((x) => x.cultureName)
    .join(", ");
}

/**
 * Pill text: the language part of the culture ("DE"), or the whole culture when the pill is alone or another culture
 * of the list has the same language part (en-US and en-GB). Empty for a page without a language.
 */
export function getLanguagePillText(cultureName: string | undefined, cultureNames: (string | undefined)[]): string {
  if (!cultureName) {
    return "";
  }

  const language = (name?: string) => (name ?? "").split("-")[0].toLowerCase();
  const isShared = cultureNames.some(
    (x) => x && x.toLowerCase() !== cultureName.toLowerCase() && language(x) === language(cultureName),
  );

  return cultureNames.length === 1 || isShared ? cultureName : language(cultureName).toUpperCase();
}

/** Status of a Multilanguage Page row: published languages out of the store languages, and outdated translations. */
export function summarizeLanguageStatuses(languages?: MultilanguagePageLanguage[]): {
  published: number;
  total: number;
  outdated: number;
} {
  const list = languages ?? [];
  return {
    published: list.filter((x) => x.status === "Published").length,
    total: list.length,
    outdated: list.filter((x) => x.isOutdated).length,
  };
}
