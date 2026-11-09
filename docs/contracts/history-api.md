# History API

The designer and the agents read the history of database pages and theme files through one endpoint. A version is
one save: when a save changed a page and theme files, it is one version listing all of them. Versions are named by an
opaque `versionId`; no git sha, branch or GitHub cursor reaches the caller.

File pages on the git flow keep their own history, `GET api/pagebuilder/git/history`
([version history](../version-history.md)).

## Where versions come from

History is kept for a store whose repository the GitHub App created for it, `{Organization}/site-{storeId}`. Each save,
publication, restore and discard is one commit on the repository's base branch, holding every file it changed (a tree
and a commit through the Git Data API; when the branch moved meanwhile, the commit is made again on its new head):

| Item | File in the repository |
|---|---|
| `page:{groupId}` | `database-pages/{groupId}.json` |
| `theme:{path}` | `theme/{path}`, `path` starting with the theme folder: `theme/default/templates/product.json` |

A commit is written after the save itself. When GitHub fails, the history loses that version and the save stands.

A store whose repository was assigned by hand (the shared `Repository`, or the store setting
`GitContentRepository`) keeps the git flow it has today, with no history of database pages or theme files.
Publications are not part of the history items: `snapshots/` is never committed.

## `IContentHistory`

```csharp
public interface IContentHistory
{
    Task<ContentHistoryStatus> GetStatusAsync(string storeId, CancellationToken cancellationToken = default);

    // One version: every entry in one commit.
    Task RecordAsync(string storeId, ContentHistoryRecord record, CancellationToken cancellationToken = default);

    // Versions that changed any of the items, newest first.
    Task<ContentVersionPage> ListAsync(string storeId, IList<HistoryItem> items, string cursor, int take,
        CancellationToken cancellationToken = default);

    // The item as of the version, changed by it or not; null when the item did not exist then.
    Task<string> ReadAsync(string storeId, HistoryItem item, string versionId,
        CancellationToken cancellationToken = default);
}
```

`GitHubSiteHistory` implements it over the store repository. A version id is the commit's id, encoded; callers keep it
as it is. A history kept elsewhere is another implementation of
the same interface; the endpoint, the designer and the agents do not change.

## Endpoints

All endpoints take `storeId` and check the store as the other Page Builder endpoints do. `item` is `page:{groupId}`,
a page of that store, or `theme:{path}`, a theme file outside `snapshots/`; it may repeat.

| Method and route | Permission | Returns |
|---|---|---|
| `GET api/pagebuilder/history/status?storeId` | `builder:read` | `{ status }`, without asking GitHub |
| `GET api/pagebuilder/history?storeId&item&cursor&take` | `builder:read` | `HistoryPage` |
| `GET api/pagebuilder/history/{versionId}?storeId&item` | `builder:read` | the item as of the version, `application/json` |
| `POST api/pagebuilder/history/{versionId}/restore?storeId&item` | `builder:update`, and see below | `{ saveId }` |

Restore writes the items as of the version as their drafts, in one save under the theme lock, all or nothing, and
records it as one version, "Restored {the first 7 characters of the versionId}". It names at most one page, and needs
`builder:templates` for templates and the rules, and `builder:theme` for the other
theme files. A theme item must be a file the builder saves: not under `catalogs/`, and in `config/` only the builder's
own files. The order of the checks: the store (403), the items (400), the permission of every item (403), the page
(404, or 403 for another store's), the items as of the version (404), and the page's shared components as a save
checks them (403 without `builder:shared-components:read`, 400). Restore takes no versions: it replaces the items as
they are now, and the designer reads them again. The editor then publishes as usual. To preview a version, the designer reads the page and the theme files
it shows as of it.

```ts
interface HistoryPage {
  status: "enabled" | "setupRequired" | "unavailable";
  versions: ContentVersion[];
  /** pass back as `cursor` for older versions; absent when there are no more */
  cursor?: string;
}

interface ContentVersion {
  versionId: string;
  /** ISO 8601 */
  date: string;
  author: { name: string; login?: string; email?: string };
  /** "Saved", "Published", "Restored …", "Discarded unpublished changes" */
  description: string;
  /** the save the designer sent in the X-PageBuilder-Save header */
  saveId?: string;
  /** the version holds what is live for every item it changed */
  published: boolean;
  /** the requested items this version changed */
  items: string[];
}
```

- `enabled`: history is kept; `versions` may still be empty.
- `setupRequired`: the GitHub App is not connected, or it has not created the store's repository yet. `versions` is
  empty; the designer offers to set up history (the `PageBuilderGit` blade).
- `unavailable`: the store's repository was assigned by hand. `versions` is empty and the designer offers nothing.

| Status | When |
|---|---|
| 400 | `item` is missing or malformed, or names a path outside the theme or under `snapshots/`; a `cursor` the list did not return; a restore naming two pages, or a theme file the builder does not save |
| 403 | no permission, or no access to the store or the page, or the page is another store's |
| 404 | the page does not exist, or the item did not exist as of the version |
| 412 | restore: the page was saved by someone else while it was being restored |
| 502 `{ error }` | GitHub did not answer or refused |

Restore and `GET` of a version take the `versionId` the list returned; any other value is 404.

The designer discards what it shows in one request ([Discard](#discard)). A database page's unpublished changes alone
are discarded with `POST api/page-builder-pages/grouped/{groupId}/discard` (`builder:update`; 400 for a page never
published), which records the published content as one version, "Discarded unpublished changes". A theme file's draft
alone is discarded with `POST api/pagebuilder/discard-draft`, recorded the same way.

## Save

A designer Save is one request and one version: the page and the theme files it changed, written as drafts. A database
page and each theme file go with the version they were read at, so a Save never overwrites what someone else saved to
them meanwhile.

| Method and route | Permission | Returns |
|---|---|---|
| `GET api/pagebuilder/designer/theme-file?storeId&path` | `builder:read` | `{ path, content?, eTag }`: the theme file as the designer edits it, its draft else what is live, as text; `content` is left out when the file does not exist |
| `POST api/pagebuilder/designer/save?storeId&catalogHash`, `DesignerSave` as the body | `builder:update`, and per theme file: `builder:templates` for templates and the rules, `builder:theme` for the other theme files | `{ saveId, eTag, themeFiles: { path, eTag }[] }`: the new versions of the database page and of each theme file |

```ts
interface DesignerSave {
  /** a database page by its group and the version it was read at, or a page kept as a file */
  page?: { groupId: string; eTag: string; content: string } | { path: string; type: "pages" | "blogs"; content: string };
  /** a template is a theme file, as are the rules, the header and footer groups, the brand and the palettes */
  themeFiles?: { path: string; eTag: string; content: string }[];
}
```

- Contents are JSON text, saved as they are: the history tells the published version by these bytes.
- A theme file's `eTag` is the one `designer/theme-file` returned, or the one the last Save returned for it. It is the
  page's version format over the store, the path and the content; a file that does not exist has a version of its own,
  so creating a file is checked as well. A theme publication moves the bytes live unchanged, and keeps the version.
- Everything is checked before anything is written: the store and each part's permission (403), the database page
  (404, or 403 for another store's), the paths and the JSON (400), the page's envelope and shared components as `grouped/{groupId}/content` checks them (400; 403 without
  `builder:shared-components:read`), its version (428 without one; 412 when the page was saved since it was read), each
  theme file's version (428 without one, 400 when it is not one the server gives), and the theme files against the
  catalog named: their section and block types and the values of fields with a closed set of values, a `color-token`'s
  palette token and a `select`'s option (400 `{ error, errors }`, each naming the file, the item and the field; see
  [Section schema catalog](theme-snapshot.md#section-schema-catalog)). Theme files need `catalogHash`: a Save with
  theme files that names none is refused (400 `{ error }`); a Save of the page alone needs none.
- Under the store's theme lock every theme file's version is compared with its draft, else what is live; when one
  differs, the Save is refused (412) and nothing is written. Sending the content a file already holds passes: a Save
  retried after a lost response. Then the theme drafts are written and the page last. When the page is refused (412) or
  fails, the theme drafts are put back as they were: a draft that existed gets its content back, a new one is removed.
  Nothing else sees them meanwhile: a theme publication and the builder's other theme writes (Discard, restore,
  `api/pagebuilder/save`, `api/pagebuilder/discard-draft`) take the same lock. A write through the content module's own
  endpoints does not.
- Theme files stay in blob storage when the store's pages are in git; the site's history records the Save after the
  lock. The check is what keeps a stale Save out of the drafts and so out of the history: the history's commit moves
  the branch only from the head it was built on, so it never loses a version, but it records whatever the Save wrote.
- The page keeps its own semantics: a database page is saved as `grouped/{groupId}/content` saves it and raises the same
  page events; a file page is a commit on the editor's work branch on a store on the git flow, else a blob draft.
- After the lock, one version records the database page and the theme files, named by the request's
  `X-PageBuilder-Save` id, 8 to 64 letters, digits or `-` (a new one without it, or instead of one in another form), which
  `saveId` returns. A file page's history stays the git flow's.

The per-file endpoints (`api/pagebuilder/save`, `grouped/{groupId}/content`) keep working for the Angular designer and
agents; a call of `api/pagebuilder/save` is one version as well.

## Discard

A designer Discard is one request and one version: the drafts of the page and of the theme files it shows go, and what
is published is current again.

| Method and route | Permission | Returns |
|---|---|---|
| `POST api/pagebuilder/designer/discard?storeId`, `DesignerDiscard` as the body | `builder:update`, and per theme file as for [Save](#save) | `{ saveId }` |

```ts
interface DesignerDiscard {
  /** a database page by its group, or a page kept as a file */
  page?: { groupId: string } | { path: string; type: "pages" | "blogs" };
  /** the theme files, by their path with the theme folder */
  themeFiles?: string[];
}
```

- The store, each part's permission (403), the database page (404, or 403 for another store's) and the paths (400) are
  checked before anything is deleted. A Discard takes no versions: it is how the designer gets past a 412, reading the
  files again afterwards.
- Under the store's theme lock the theme drafts are deleted first and the page's last. A theme file never published
  keeps its draft, which is its only copy. A database page never published is refused (400) and the theme drafts are
  put back. A file page's draft is its work branch on the git flow, else its blob draft.
- After the lock, one version, "Discarded unpublished changes", records the published content of the database page and
  of each theme file whose draft went, named as a Save is; nothing is recorded when no draft went.
