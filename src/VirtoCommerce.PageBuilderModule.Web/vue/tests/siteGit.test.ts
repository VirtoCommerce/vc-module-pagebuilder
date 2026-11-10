import assert from "node:assert/strict";
import test from "node:test";
import { siteGitView } from "../src/modules/widgets/siteGit";

test("the Git panel shows setup, an error, the switch, a repository to make, or the live connection", () => {
  assert.equal(siteGitView({ configured: false, enabled: false }), "setup");
  assert.equal(siteGitView({ configured: true, enabled: true, error: "App not installed" }), "error");
  assert.equal(siteGitView({ configured: true, enabled: false, repository: "acme/site-s1" }), "off");
  assert.equal(siteGitView({ configured: true, enabled: true, repository: "acme/site-s1" }), "missing");
  assert.equal(siteGitView({ configured: true, enabled: true, repository: "acme/site-s1", url: "https://github.com/acme/site-s1" }), "on");
});
