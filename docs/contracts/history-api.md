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
| `POST api/pagebuilder/history/{versionId}/restore?storeId&item` | see below | `{ saveId }` |

Restore writes the items as of the version as their drafts, in one save under the theme lock, all or nothing, and
records it as one version, "Restored {the first 7 characters of the versionId}". It names at most one page, and needs
`builder:update` for the page, `builder:templates` for templates and the rules, and `builder:theme` for the other
theme files. The editor then publishes as usual. To preview a version, the designer reads the page and the theme files
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
| 400 | `item` is missing or malformed, or names a path outside the theme or under `snapshots/`; a `cursor` the list did not return; a restore naming two pages |
| 403 | no permission, or no access to the store or the page, or the page is another store's |
| 404 | the page does not exist, or the item did not exist as of the version |
| 412 | restore: the page was saved by someone else while it was being restored |
| 502 `{ error }` | GitHub did not answer or refused |

Restore and `GET` of a version take the `versionId` the list returned; any other value is 404.

A database page's unpublished changes are discarded with `POST api/page-builder-pages/grouped/{groupId}/discard`
(`builder:update`; 400 for a page never published), which records the published content as one version, "Discarded
unpublished changes". A theme file's draft is discarded with `POST api/pagebuilder/discard-draft`, recorded the same way.

## Save

A designer Save is one request and one version: the page and the theme files it changed, written as drafts.

| Method and route | Permission | Returns |
|---|---|---|
| `POST api/pagebuilder/designer/save?storeId&catalogHash`, `DesignerSave` as the body | `builder:read`, and per part: `builder:update` for the page, `builder:templates` for templates and the rules, `builder:theme` for the other theme files | `{ saveId, eTag }` |

```ts
interface DesignerSave {
  /** a database page by its group and the version it was read at, or a page kept as a file */
  page?: { groupId: string; eTag: string; content: string } | { path: string; type: "pages" | "blogs"; content: string };
  /** a template is a theme file, as are the rules, the header and footer groups, the theme settings, the brand and the palette */
  themeFiles?: { path: string; content: string }[];
}
```

- Contents are JSON text, saved as they are: the history tells the published version by these bytes.
- Everything is checked before anything is written: the store and each part's permission (403), the paths and the
  JSON (400), the page's envelope and shared components as `grouped/{groupId}/content` checks them (400; 403 without
  `builder:shared-components:read`), its version (428 without one; 412 when the page was saved since it was read), and
  the theme files against the catalog named (400 `{ error, errors }`; not checked without `catalogHash`).
- Under the store's theme lock the theme drafts are written first and the page last. When the page is refused (412) or
  fails, the theme drafts are put back as they were: a draft that existed gets its content back, a new one is removed.
  Nothing else sees them meanwhile: a theme publication takes the same lock.
- The page keeps its own semantics: a database page is saved as `grouped/{groupId}/content` saves it and raises the same
  page events; a file page is a commit on the editor's work branch on a store on the git flow, else a blob draft.
- After the lock, one version records the database page and the theme files, named by the request's
  `X-PageBuilder-Save` id (a new one without it), which `saveId` returns. A file page's history stays the git flow's.

The per-file endpoints (`api/pagebuilder/save`, `grouped/{groupId}/content`) keep working for the Angular designer and
agents; a call of `api/pagebuilder/save` is one version as well.
