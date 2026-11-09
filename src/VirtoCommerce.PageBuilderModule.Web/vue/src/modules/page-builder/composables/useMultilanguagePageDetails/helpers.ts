import type {
  MultilanguagePageBatchResult,
  MultilanguagePageLanguage,
} from "../../../../api_client/virtocommerce.pagebuildermodule";

// Matches MultilanguagePageBatchResult.FailedReason on the backend.
const failedReason = "Failed";

export function hasLanguageVersions(languages?: MultilanguagePageLanguage[]): boolean {
  return (languages ?? []).some((x) => !!x.page);
}

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

export function isPermalinkCheckNeeded(saved: IPermalinkTarget | undefined, current: IPermalinkTarget): boolean {
  if (!saved) {
    return true;
  }

  return (
    normalizePermalink(saved.permalink) !== normalizePermalink(current.permalink) ||
    (saved.cultureName ?? "").toLowerCase() !== (current.cultureName ?? "").toLowerCase()
  );
}

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
