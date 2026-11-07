import type { GroupedPageBuilderPage } from "../../api_client/virtocommerce.pagebuildermodule";

/** Where a page is in its life, as the lifecycle filters (drafts, pending, active, archived) put it. */
export type PageLifecycle = "Draft" | "Pending" | "Active" | "Archived";

/** One lifecycle per page, mirroring GroupedPageSearchService's filters: a published page waiting for its start date is pending. */
export function pageLifecycle(
  page: Pick<GroupedPageBuilderPage, "status" | "startDate" | "endDate">,
  now = new Date(),
): PageLifecycle {
  if (page.status === "Archived" || (page.endDate && new Date(page.endDate) < now)) {
    return "Archived";
  }
  if (page.status !== "Published") {
    return "Draft";
  }
  return page.startDate && new Date(page.startDate) > now ? "Pending" : "Active";
}
