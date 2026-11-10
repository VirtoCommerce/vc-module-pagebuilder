import { ApiException, type GitContentSiteStatus } from "../../api_client/virtocommerce.pagebuildermodule";

export type SiteGitView = "setup" | "error" | "off" | "missing" | "on";

export function siteGitView(status: GitContentSiteStatus): SiteGitView {
  if (!status.configured) return "setup";
  if (status.error) return "error";
  if (!status.enabled) return "off";
  return status.url ? "on" : "missing";
}

/** The Git endpoints say what went wrong in `{ error }`, except a refusal, which has no body. */
export function gitErrorMessage(error: unknown, forbidden: string): string {
  if (!(error instanceof ApiException)) return error instanceof Error ? error.message : String(error);
  return reasonOf(error.response) ?? (error.status === 403 ? forbidden : `${error.status} ${error.message}`);
}

function reasonOf(body: string): string | undefined {
  try {
    return (JSON.parse(body) as { error?: string }).error;
  } catch {
    return undefined;
  }
}
