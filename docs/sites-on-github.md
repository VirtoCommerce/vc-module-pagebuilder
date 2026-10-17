# Sites on GitHub

Page Builder can keep each site's pages in **its own GitHub repository** — the admin calls it *version history*. Every
change becomes a commit, and any earlier version can be previewed and restored from the designer's **Version history**
panel.

- **Pages in a site's page list** (kept in the platform's database) stay in the database, which the storefront is
  served from. Each save, publish, restore and discard is also committed to the site repository's base branch as
  `database-pages/{pageId}.json`: one linear history per page. The published version is the newest commit whose file
  is byte for byte the published content, so nothing else has to record it. Recording is best effort: a GitHub
  outage costs the history an entry, never the editor a save (the platform logs a warning).
- **File pages** (a site's Files › Pages) move into the repository entirely: a save is a commit on the editor's work
  branch, publishing merges it into the base branch, and the platform then writes the published page to the live
  storage itself, so the repositories need no CI.

## How it is organised

| Level | What | Where it lives |
|---|---|---|
| Environment (once) | The GitHub connection: a GitHub App and the organization that holds the site repositories | **Sites → GitHub** in the admin (the one-click setup), or `PageBuilder:GitContent` in configuration — which wins when complete |
| Site | Its repository, `{Organization}/{RepositoryPrefix}{storeId}` (e.g. `Acme-Sites/site-b2b-store`), and the on/off switch | Created by the module; the switch is the store setting *Publish pages through Git* |

Use **one organization per environment** (for example `Acme-Sites` and `Acme-Sites-Staging`): a production database
restored into staging must not create or archive repositories in production's organization.

## Set it up in one click

1. Open **Sites → GitHub** in the admin (the GitHub button on the Sites panel). A site's **History** panel points there
   while the platform has no connection.
2. Enter the GitHub organization for this environment's sites (create one on GitHub first if needed) and select
   **Create GitHub App**. It takes the *Update settings* permission, since it connects the whole platform.
3. GitHub opens in a new tab with the App ready to create — private, with Administration, Contents and Pull requests
   *Read and write* and no webhook. Select **Create GitHub App**. You must be an owner of the organization.
4. GitHub hands the App's id and private key to the platform and asks you to **install** the App: install it on the
   organization with access to **all repositories**, so repositories created later are covered.
5. GitHub brings you back to the Sites panel, where **GitHub** now shows the organization, the App and every site
   with its repository. Open a site's **History** and select **Enable history**: the site's repository is created and
   every later save is a version.

From then on, every new site gets its repository and history straight away; existing sites are turned on one by one
from their **History** panel, or from **Enable history** in the designer.

**Where the App is kept.** The platform keeps the App's id, name, organization and private key in one hidden platform
setting, encrypted with the platform's ASP.NET data protection keys. Those keys are not in the database — the platform
keeps them in Redis when it has Redis, else in the server's own key folder — so a database restored on another
environment cannot use the App there (the panel says so and offers the setup again). On a server without Redis whose
key folder is not kept across deployments (a container without a volume for it), every redeployment loses the keys and
the setup has to be run again; give such servers Redis, or configure the connection instead (below). The callback
addresses GitHub is given are built from the address the admin is opened on; behind a reverse proxy, make sure the
platform sees the public scheme and host (forwarded headers).

## Set it up in configuration

Configuration takes precedence over the one-click setup whenever it holds a complete connection, and the admin then
does not offer the setup.

1. Create a GitHub organization for the sites of this environment.
2. In it, create a **GitHub App** with these repository permissions, all *Read and write*: **Administration** (to create
   and archive repositories), **Contents** and **Pull requests**. It needs no webhook.
3. Install the App on the organization with access to **all repositories** (repositories created later must be
   covered), and generate a private key.
4. Configure the platform and restart it:

   ```
   PageBuilder__GitContent__Enabled=true
   PageBuilder__GitContent__Organization=Acme-Sites
   PageBuilder__GitContent__AppId=123456
   PageBuilder__GitContent__PrivateKey=-----BEGIN PRIVATE KEY-----\n...\n-----END PRIVATE KEY-----
   PageBuilder__GitContent__BaseBranch=main
   PageBuilder__GitContent__AutoCreateRepositories=true   # optional: new sites get their repository at once
   PageBuilder__GitContent__RepositoryPrefix=site-         # optional, the default
   ```

   The private key may be written on one line with `\n` escapes.
5. Open **Sites → a site → History** in the admin and select **Test connection**, then **Enable history**.

Without a `BaseBranch` in configuration, the one-click setup uses `main`.

## What happens when

- **Enable history** (or a new site, with the one-click setup or `AutoCreateRepositories`): the site's repository is created — private, with a first
  commit — or adopted when it exists, and the base branch (and `ReleaseBranch`, when configured) is made ready. The
  repository's description, `Page Builder content of store {storeId}`, marks it as the module's.
- **First save** of a site that is on but has no repository yet creates it, so nothing fails on a missing branch.
- **Publish / Unpublish**: the work branch is merged into the base branch, then the platform writes (or removes) the
  live page from what the base branch now holds. Publishing again repairs a live write that failed after its merge.
- **Turn off history**: pages are saved to the platform's storage again; the repository and its history stay.
- **Site deleted**: its repository is archived — only when the module created it for that site. Restoring the site
  under the same id brings the repository back.

## A site with its own repository

A site can use an existing repository instead: set the store setting **Git repository** to `owner/name`. The module
uses it but never creates, adopts or archives it. With the App, the App must be installed on that owner too.

## Earlier installations: one shared repository

Setting `Repository` (instead of `Organization`) keeps the earlier model: one repository for every store, with a token
(`Token`) or the App. To move to one repository per site, first set each git-enabled store's **Git repository** to the
shared repository, then replace `Repository` with `Organization`. The two cannot be set together.

## Good to know

- Auto-merge stays off on the site repositories: the merge then happens inside the publish request, which is where the
  live page is written. A repository with required checks finishes publishing on the next Publish.
- Changing `RepositoryPrefix` after repositories exist points sites without their own setting at new, empty
  repositories.
- GitHub's rate limit (about 5,000 requests per hour) is shared by all sites of an installation; page reads and
  version lists are cached.
