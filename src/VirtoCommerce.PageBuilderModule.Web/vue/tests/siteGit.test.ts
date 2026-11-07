import assert from "node:assert/strict";
import test from "node:test";
import { gitHubAppSetupUrl, siteGitUrl, siteGitView } from "../src/modules/widgets/siteGit";

test("the Git panel shows setup, an error, the switch, a repository to make, or the live connection", () => {
  assert.equal(siteGitView({ configured: false, enabled: false }), "setup");
  assert.equal(siteGitView({ configured: true, enabled: true, error: "App not installed" }), "error");
  assert.equal(siteGitView({ configured: true, enabled: false, repository: "acme/site-s1" }), "off");
  assert.equal(siteGitView({ configured: true, enabled: true, repository: "acme/site-s1" }), "missing");
  assert.equal(siteGitView({ configured: true, enabled: true, repository: "acme/site-s1", url: "https://github.com/acme/site-s1" }), "on");
});

test("a site's git endpoints carry its id escaped", () => {
  assert.equal(siteGitUrl("B2B store"), "/api/pagebuilder/git/site?storeId=B2B%20store");
  assert.equal(siteGitUrl("s1", "/connect"), "/api/pagebuilder/git/site/connect?storeId=s1");
});

test("the GitHub setup is asked for the organization as typed, trimmed, and the site to come back to", () => {
  assert.equal(gitHubAppSetupUrl(" acme ", "B2B store"), "/api/pagebuilder/git/app/setup?organization=acme&storeId=B2B+store");
  assert.equal(gitHubAppSetupUrl("acme"), "/api/pagebuilder/git/app/setup?organization=acme");
});
