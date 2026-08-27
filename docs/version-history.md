# Page Version History

The designer can show every version of the page you have open — what is published, and what somebody has
saved but not published yet — and let you continue editing from any of them.

This works only for stores whose pages are kept in a git repository (the content git flow). Without it a
draft overwrites the page and no versions are kept, so the **Version history** button is not shown at all.

## Where it comes from

A version is a commit that touched the page's file. Versions are found by the file's history, not by who
made them or where, which is what makes edits done outside the designer visible here:

| Edited in | Saved as |
|---|---|
| Page Builder | a commit on your own work branch, `designer/<user>/<page>` |
| Claude Code (the content manager guide flow) | a commit on a work branch of its own, `content/<task>` |
| A developer, in git | a commit on any branch |

All three end up in the same list. Before this panel existed, only the first was visible in the designer:
a page edited through Claude looked unchanged in the builder until it was published, and the builder even
reported "no changes" while an unpublished draft sat in the repository.

## Reading the list

Open a page in the designer and click **Version history**. A number on the button — *Version history (2)*
— means somebody else has unpublished work on this page.

Each row carries:

- **Published** — the production branch has this commit: this is what visitors see.
- **Unpublished** — a saved draft that has not been published. Somebody's work in progress.
- **Mine** — the version is on your own work branch.
- **Bulk change** — the commit touched far more than this page (a seeding or bulk-reformat commit). It is a
  truthful version of the page and rarely an interesting one, so it is played down and left out of the
  count on the button.

Plus the author, when it was made, the short commit sha, and the branch it lives on. A hover shows the full
list of branches when a commit is reachable from several.

Saving is a commit, so an afternoon of editing produces a stack of near-identical entries. Consecutive
saves by the same author on the same branch are folded into one row — expand it with *Show N earlier saves*.

## Preview a version

**Preview** opens the storefront rendering of that exact commit in a new tab. The link is pinned to a
commit sha, so it keeps showing what it showed when it was made, and it never changes the page.

## Continue editing from a version

**Continue from this** takes the content of the chosen version and saves it as a **new version on your own
work branch**. Then you edit and publish as usual.

Nothing is overwritten and nothing disappears:

- the version you continued from stays where it was;
- **your current draft is not lost** — it stays in the history as the version before this one;
- other people's branches are never written to.

Two things to know:

- **Save or discard your unsaved changes first.** Continuing re-reads the page, so unsaved edits in the
  editor would be lost. The button stays disabled until the editor is clean.
- **You are asked to confirm.** The editor is about to show different content, which is worth a deliberate
  click when the version came from somebody else.

## Rolling back a published page

Continue from the last good version, then press **Publish**. That is the whole rollback: the restored
content goes through the usual pull request and deploy, and the bad version stays in history rather than
being erased. No git client, and no developer, required.

## What the list does not do

- **No diff between versions.** Use the preview of a version to see what it looked like.
- **A renamed or moved page starts a new history.** The history follows the file's path, so the versions of
  the old path stay with the old path.
- **Media is not versioned.** Git is the source of truth for pages, not for the images they reference: an
  old version of a page may point at an image that has since been replaced.
- **Deleted branches take their versions with them.** Abandoned work branches are cleaned up eventually,
  and versions that existed only there disappear from the list.
- **The list can be incomplete on a large repository.** The panel says so, with a **Scan more branches**
  button, rather than implying it has seen everything.

## For developers

The panel is configured by the server: `GET /api/pagebuilder/settings` includes a `history` descriptor for
a store on the git flow and omits it otherwise, which is what shows or hides the button.

| Endpoint | What it does |
|---|---|
| `GET api/pagebuilder/git/history?storeId&path&type&take&after` | the page's versions, published and unpublished, deduplicated by sha |
| `POST api/pagebuilder/git/restore-version?storeId&path&type&sha` | appends the content of `sha` to the caller's work branch → `{ branch, commitSha, restoredFrom }` |
| `GET api/pagebuilder/git/preview?storeId&path&type&ref=<sha>` | redirects to the storefront preview of one commit |

The list is assembled with a single GitHub GraphQL query — the production branch's history of the file plus
every branch's history of the same file — and cached briefly. Measured against a real content repository,
that query costs 1 point of the GitHub hourly budget, and the module drops the cached list whenever it
commits to the page.

`mine` is computed from the work branch name, which is derived from the platform login. An edit made
outside the builder therefore shows its author but not the *Mine* mark; mapping a person's git addresses to
their platform account is a separate step.

Restore only ever appends. Writing into the branch a version came from would mean force-pushing over
whatever came after it — destroying the history the panel exists to show — or writing into a branch that
belongs to somebody else, which is the shared draft slot the git flow removed.
