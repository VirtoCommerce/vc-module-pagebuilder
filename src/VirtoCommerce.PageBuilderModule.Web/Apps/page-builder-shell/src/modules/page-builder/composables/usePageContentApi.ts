import { useApiClient } from "@vc-shell/framework";
import { PageBuilderPageClient, GroupedPageBuilderPage } from "../../../api_client/virtocommerce.pagebuildermodule";

const { getApiClient } = useApiClient(PageBuilderPageClient);

export interface PageExportData {
  name?: string;
  permalink?: string;
  cultureName?: string;
  visibility?: boolean;
  userGroups?: string;
  organizationId?: string;
  startDate?: Date;
  endDate?: Date;
  content: unknown;
}

async function getAuthHeaders(): Promise<Record<string, string>> {
  const client = await getApiClient();
  const headers: Record<string, string> = {};
  if ((client as any).authToken) {
    headers["authorization"] = `Bearer ${(client as any).authToken}`;
  }
  return headers;
}

/** Download page metadata + content as a single JSON file */
export async function downloadPageContent(groupId: string, page: GroupedPageBuilderPage): Promise<void> {
  const headers = await getAuthHeaders();
  const url = `/api/page-builder-pages/grouped/${encodeURIComponent(groupId)}/content?draft=true`;
  const response = await fetch(url, { headers });

  if (!response.ok) {
    throw new Error(`Failed to download content: ${response.status}`);
  }

  const contentText = await response.text();
  let content: unknown;
  try {
    content = JSON.parse(contentText);
  } catch {
    content = contentText;
  }

  const exportData: PageExportData = {
    name: page.name,
    permalink: page.permalink,
    cultureName: page.cultureName,
    visibility: page.visibility,
    userGroups: page.userGroups,
    organizationId: page.organizationId,
    startDate: page.startDate,
    endDate: page.endDate,
    content,
  };

  const blob = new Blob([JSON.stringify(exportData, null, 2)], { type: "application/json" });
  const a = document.createElement("a");
  a.href = URL.createObjectURL(blob);
  a.download = `${page.name || "page"}.json`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(a.href);
}

/** Parse an imported JSON file into metadata + content */
export async function parseImportFile(file: File): Promise<PageExportData> {
  const text = await file.text();
  const data = JSON.parse(text);

  return {
    name: data.name,
    permalink: data.permalink,
    cultureName: data.cultureName,
    visibility: data.visibility,
    userGroups: data.userGroups,
    organizationId: data.organizationId,
    startDate: data.startDate ? new Date(data.startDate) : undefined,
    endDate: data.endDate ? new Date(data.endDate) : undefined,
    content: data.content,
  };
}

/** Upload content to a page */
export async function uploadPageContent(groupId: string, content: unknown): Promise<void> {
  const headers = await getAuthHeaders();
  const url = `/api/page-builder-pages/grouped/${encodeURIComponent(groupId)}/content`;
  const body = typeof content === "string" ? content : JSON.stringify(content);
  const response = await fetch(url, {
    method: "POST",
    headers: {
      ...headers,
      "Content-Type": "text/plain; charset=utf-8",
    },
    body,
  });

  if (!response.ok) {
    throw new Error(`Failed to upload content: ${response.status}`);
  }
}
