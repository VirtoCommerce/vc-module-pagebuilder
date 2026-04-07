import { useApiClient } from "@vc-shell/framework";
import { PageBuilderPageClient } from "../../../api_client/virtocommerce.pagebuildermodule";

const { getApiClient } = useApiClient(PageBuilderPageClient);

async function getAuthHeaders(): Promise<Record<string, string>> {
  const client = await getApiClient();
  const headers: Record<string, string> = {};
  if ((client as any).authToken) {
    headers["authorization"] = `Bearer ${(client as any).authToken}`;
  }
  return headers;
}

/** Download page content as a file (streaming) */
export async function downloadPageContent(groupId: string, pageName: string): Promise<void> {
  const headers = await getAuthHeaders();
  const url = `/api/page-builder-pages/grouped/${encodeURIComponent(groupId)}/content?draft=true`;
  const response = await fetch(url, { headers });

  if (!response.ok) {
    throw new Error(`Failed to download content: ${response.status}`);
  }

  const blob = await response.blob();
  const a = document.createElement("a");
  a.href = URL.createObjectURL(blob);
  a.download = `${pageName || "page"}-content.json`;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(a.href);
}

/** Upload content from a File to a page (streaming via Request.Body) */
export async function uploadPageContent(groupId: string, file: File): Promise<void> {
  const headers = await getAuthHeaders();
  const url = `/api/page-builder-pages/grouped/${encodeURIComponent(groupId)}/content`;
  const response = await fetch(url, {
    method: "POST",
    headers: {
      ...headers,
      "Content-Type": "text/plain; charset=utf-8",
    },
    body: file,
  });

  if (!response.ok) {
    throw new Error(`Failed to upload content: ${response.status}`);
  }
}
