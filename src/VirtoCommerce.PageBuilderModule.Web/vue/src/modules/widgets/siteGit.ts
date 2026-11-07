/** A site's GitHub connection, as `GET /api/pagebuilder/git/site` reports it (asked live: it is also the connection test). */
export interface SiteGitStatus {
  /** the platform has a GitHub connection at all */
  configured: boolean;
  organization?: string;
  /** the site has its pages in git */
  enabled: boolean;
  repository?: string;
  baseBranch?: string;
  /** the repository on GitHub; missing while it does not exist */
  url?: string;
  lastCommitSha?: string;
  error?: string;
  /** where the connection comes from: the platform's configuration, or the one-click setup's GitHub App */
  source?: "configuration" | "app";
  /** where to install the setup's App on its organization */
  installUrl?: string;
}

/**
 * What the Git panel shows: how to set the connection up, why it fails, the switch to turn it on, a repository that
 * still has to be made, or the live connection.
 */
export type SiteGitView = "setup" | "error" | "off" | "missing" | "on";

export function siteGitView(status: SiteGitStatus): SiteGitView {
  if (!status.configured) return "setup";
  if (status.error) return "error";
  if (!status.enabled) return "off";
  return status.url ? "on" : "missing";
}

/** The site's git endpoints: its status, and switching it on or off. */
export const siteGitUrl = (storeId: string, action: "" | "/connect" | "/disconnect" = "") =>
  `/api/pagebuilder/git/site${action}?storeId=${encodeURIComponent(storeId)}`;

/** The platform's GitHub connection, as `GET /api/pagebuilder/git/connection` reports it (asked live: it is the test). */
export interface GitHubConnection {
  configured: boolean;
  source?: "configuration" | "app";
  organization?: string;
  organizationUrl?: string;
  /** the one repository every site shares, on an installation set up before one per site */
  repository?: string;
  appSlug?: string;
  appUrl?: string;
  installUrl?: string;
  autoCreateRepositories?: boolean;
  /** every site: its repository, and whether its history is on */
  sites?: { storeId: string; name: string; repository?: string; url?: string; enabled: boolean }[];
  error?: string;
}

export const gitHubConnectionUrl = "/api/pagebuilder/git/connection";

/**
 * Starts the one-click GitHub setup for the organization; the answer is the page that takes the browser to GitHub.
 * GitHub comes back to the site, else to the Sites panel.
 */
export const gitHubAppSetupUrl = (organization: string, storeId?: string) =>
  `/api/pagebuilder/git/app/setup?${new URLSearchParams({ organization: organization.trim(), ...(storeId ? { storeId } : {}) })}`;
