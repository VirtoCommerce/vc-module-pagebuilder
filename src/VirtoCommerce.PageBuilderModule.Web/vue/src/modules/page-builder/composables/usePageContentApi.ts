import { GroupedPageBuilderPage } from "../../../api_client/virtocommerce.pagebuildermodule";

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

export async function downloadPageContent(groupId: string, page: GroupedPageBuilderPage): Promise<void> {
  const url = `/api/page-builder-pages/grouped/${encodeURIComponent(groupId)}/content?draft=true`;
  const response = await fetch(url);

  if (!response.ok) {
    throw new Error(`Failed to download content: ${response.status}`);
  }

  let content: unknown;
  try {
    content = (await response.json()) as unknown;
  } catch (parseError) {
    const detail = parseError instanceof Error ? parseError.message : String(parseError);
    throw new Error(
      `Page content for group ${groupId} is not valid JSON — the page may be corrupted on the server (${detail}).`,
    );
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
