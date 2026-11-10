/** GET /api/pagebuilder/git/site asks GitHub live, so it is also the connection test. */
export interface SiteGitStatus {
  configured: boolean;
  organization?: string;
  enabled: boolean;
  repository?: string;
  baseBranch?: string;
  url?: string;
  lastCommitSha?: string;
  error?: string;
  source?: "configuration" | "app";
  installUrl?: string;
}

export type SiteGitView = "setup" | "error" | "off" | "missing" | "on";

export function siteGitView(status: SiteGitStatus): SiteGitView {
  if (!status.configured) return "setup";
  if (status.error) return "error";
  if (!status.enabled) return "off";
  return status.url ? "on" : "missing";
}

export const siteGitUrl = (storeId: string, action: "" | "/connect" | "/disconnect" = "") =>
  `/api/pagebuilder/git/site${action}?storeId=${encodeURIComponent(storeId)}`;

/** GET /api/pagebuilder/git/connection asks GitHub live, so it is also the connection test. */
export interface GitHubConnection {
  configured: boolean;
  source?: "configuration" | "app";
  organization?: string;
  organizationUrl?: string;
  /** Set only on an installation where every site shares one repository. */
  repository?: string;
  appSlug?: string;
  appUrl?: string;
  installUrl?: string;
  autoCreateRepositories?: boolean;
  sites?: { storeId: string; name: string; repository?: string; url?: string; enabled: boolean }[];
  error?: string;
}

export const gitHubConnectionUrl = "/api/pagebuilder/git/connection";

/** `returnUrl` is the admin page GitHub sends the browser back to once the app is installed. */
export const gitHubAppSetupUrl = (organization: string, returnUrl: string) =>
  `/api/pagebuilder/git/app/setup?${new URLSearchParams({ organization: organization.trim(), returnUrl })}`;
