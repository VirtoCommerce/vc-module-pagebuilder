import type { SharedComponentUsagePage } from "../types";

export function getDistinctUsagePages(pages: SharedComponentUsagePage[] | undefined): SharedComponentUsagePage[] {
  const result: SharedComponentUsagePage[] = [];
  const keys = new Set<string>();

  for (const page of pages ?? []) {
    const key = page.id || [page.permalink, page.cultureName, page.status, page.name].filter(Boolean).join("|");

    if (!key || keys.has(key)) {
      continue;
    }

    keys.add(key);
    result.push(page);
  }

  return result;
}

export function getUsagePageTitle(page: SharedComponentUsagePage): string {
  return page.name || page.permalink || page.id || "";
}
