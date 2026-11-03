# History API

The designer and the agents read the history of database pages and theme files through one endpoint. A version is
one save: when a save changed a page and theme files, it is one version listing all of them. Versions are named by an
opaque `versionId`; no git sha, branch or GitHub cursor reaches the caller.

File pages on the git flow keep their own history, `GET api/pagebuilder/git/history`
([version history](../version-history.md)).

## Where versions come from

History is kept for a store whose repository the GitHub App created for it, `{Organization}/site-{storeId}`. Each save,
publication, restore and discard is one commit on the repository's base branch, holding every file it changed:

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

`GitHubContentHistory` implements it over the store repository. A history kept elsewhere is another implementation of
the same interface; the endpoint, the designer and the agents do not change.

## Endpoints

All endpoints take `storeId` and check the store as the other Page Builder endpoints do. `item` is `page:{groupId}`,
a page of that store, or `theme:{path}`, a theme file outside `snapshots/`; it may repeat.

| Method and route | Permission | Returns |
|---|---|---|
| `GET api/pagebuilder/history?storeId&item&cursor&take` | `builder:read` | `HistoryPage` |
| `GET api/pagebuilder/history/{versionId}?storeId&item` | `builder:read` | the item as of the version, `application/json` |
| `POST api/pagebuilder/history/{versionId}/restore?storeId&item` | see below | `{ saveId }` |

Restore writes the items as of the version as their drafts, in one save, and records it as one version
("Restored …"). To preview a version, the designer reads every theme file and the page as of it. It needs `builder:update` for pages, `builder:templates` for templates and the rules, and
`builder:theme` for the other theme files. The editor then publishes as usual.

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
| 400 | `item` is missing or malformed, or names a path outside the theme or under `snapshots/` |
| 403 | no permission, or no access to the store or the page |
| 404 | the item did not exist as of the version |
| 502 `{ error }` | GitHub did not answer or refused |

Restore and `GET` of a version take the `versionId` the list returned; any other value is 404.
