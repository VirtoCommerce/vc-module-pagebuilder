# AI Assistant for Page Builder (Virto OZ)

The Page Builder ships with an embedded AI assistant powered by **Virto OZ**. Instead of
assembling a page block by block, a content author describes what they want in plain
language and the assistant produces a real Page Builder page — built from the blocks the
store's theme actually exposes, saved through the standard Page Builder API, and editable
afterwards like any other page.

This document explains what the assistant can do, how it works end to end, the tools it
uses, and the guardrails that constrain it.

---

## At a glance

| | |
|---|---|
| **Where it runs** | Inside the Vue 3 Page Builder shell (`Apps/page-builder-shell`), as a side panel hosting the Virto OZ iframe. |
| **Input** | Free-form text prompt ("write a blog post about spring gardening", "add an FAQ to the winter page"). |
| **Output** | A Page Builder page document (`{ settings, content }`) created or updated through the Page Builder REST API. |
| **Intents** | Create · Edit · Diagnose · Bulk. |
| **Persistence** | All writes land as **Draft**. Publishing is always a separate, explicitly confirmed step. |
| **Authority** | Every action is gated by platform permissions (`builder:read/create/update/publish`) enforced by the API, plus per-tool approval prompts on writes. |

---

## How it works

The assistant is a **tool-using agent**: a system prompt (the "PageBuilder Expert" agent)
plus a set of declarative tools that map to existing Page Builder REST endpoints. The LLM
never talks to the database directly — it can only call the tools listed below, and each
tool call is an authenticated, permission-checked API request.

```
┌────────────────────┐   prompt + context    ┌──────────────┐   tool calls (REST)   ┌──────────────────┐
│ page-builder-shell │ ────────────────────▶ │   Virto OZ   │ ────────────────────▶ │ PageBuilder API   │
│  (host, Vue 3)     │ ◀──────────────────── │  (AI agent)  │ ◀──────────────────── │ (+ permissions)   │
└────────────────────┘   messages / results  └──────────────┘    JSON responses     └──────────────────┘
```

### Context the host provides

The host shell injects two pieces of context into every conversation:

- **Current store** — an item `{ id: "<storeId>", objectType: "pagebuilder.store", name: "Current store: <storeId>" }`.
  Every store-scoped tool reads the `storeId` from this item. The agent is instructed
  never to guess or invent a store id.
- **Active page** (when a page is open) — an item `{ id, objectType: "pagebuilder.page", name }`.
  This gives the agent the `groupId` of the page the user is currently looking at, so
  "edit this page" works without a search step.

> The assistant does **not** receive the page's full content through context. When it
> needs the current content of a page (for editing or content-aware filtering), it loads
> it on demand via `pagebuilder_get_page_content`. The context only carries the page's
> identity (`id` + `name`).

---

## The toolset

Eight declarative tools back the assistant. Read-only tools run without prompting; every
tool that writes requires approval **and** is gated by a platform permission.

| Tool | Purpose | Endpoint | Permission | Approval |
|---|---|---|---|:--:|
| `pagebuilder_list_section_schemas` | List the theme catalog: sections, templates, blocks, objects, shared field lists (as `{key, name, description?}`). | `GET /api/pagebuilder/schemas` | `builder:read` | — |
| `pagebuilder_get_section_schema` | Load the full schema of one catalog entry by `kind` + `key`. | `GET /api/pagebuilder/schemas/{kind}/{key}` | `builder:read` | — |
| `pagebuilder_search_pages` | Find pages in a store by keyword; returns `groupId`, name, permalink, status. | `POST /api/page-builder-pages/search` | `builder:read` | — |
| `pagebuilder_get_page_meta` | Load page metadata only (status, visibility, schedule, user groups, version list) — no content. | `GET /api/page-builder-pages/grouped/{groupId}` | `builder:read` | — |
| `pagebuilder_get_page_content` | Load the full page JSON (latest draft if present) for editing. | `GET /api/page-builder-pages/grouped/{groupId}/content` | `builder:read` | — |
| `pagebuilder_create_page` | Create a new page with full content in one call. Always `Draft`. | `POST /api/page-builder-pages/create-group-page` | `builder:create` | ✔ |
| `pagebuilder_save_page_content` | Replace the full content of an existing page; saved as a draft. | `POST /api/page-builder-pages/grouped/{groupId}/content-json` | `builder:update` | ✔ |
| `pagebuilder_publish_page` | Publish a draft, or unpublish the live version. **Irreversible — no rollback API.** | `POST /api/page-builder-pages/grouped/publishing/{groupId}` | `builder:publish` | ✔ |

---

## The page document model

Every page the assistant produces follows the same envelope the Page Builder uses
internally:

```json
{
  "settings": { "type": "settings", "...": "page-level metadata" },
  "content":  [ { "type": "<sectionKey>", "id": "<unique>", "...": "fields" } ]
}
```

- **`settings`** — page-level metadata (title, permalink, SEO fields, …). Its shape is
  derived from the active **template** (e.g. `page`, `blog`, `product`).
- **`content`** — an ordered list of section instances. Each section's `type` must match a
  key the theme exposes, and its fields must conform to that section's resolved schema.

The assistant is schema-driven: it does not hardcode block names or invent fields. It picks
sections by the `description`/`name` hints the theme author publishes, and fills only the
fields the schema declares.

### Schema discovery (two-phase)

To avoid loading every schema upfront, discovery is split:

1. **Index** — `pagebuilder_list_section_schemas` once per session returns a lightweight
   catalog of `{ key, name, description? }` for sections, templates, blocks, objects, and
   shared lists. These `key` values are the single source of truth for what exists.
2. **Full schema** — `pagebuilder_get_section_schema` loads the complete field list only
   for the entries the assistant actually decides to use, with per-session caching.

Field types map predictably to JSON (`string`, `markdown` → `{markdown, html}`, `select`,
`images`, `color`, `number`, `checkbox`, `list`, `object`). Shared field lists
(`shared/_sections`, `shared/_blocks`, and any `includeShared` entries) are merged into a
section's own fields.

---

## Capabilities by intent

The agent classifies each request into one of four intents and runs the matching workflow.

### 1. Create

> *"Write a blog post about sustainable packaging." / "Make a landing page for the spring sale."*

1. Load the theme catalog (index).
2. Choose a template (defaults to `page`; switches if the user clearly names another, e.g.
   `blog`, `product`).
3. Plan a section list (typically 4–7 sections, leading with a hero) using composition
   heuristics and the theme's `description` hints.
4. Fetch full schemas for the chosen template and sections.
5. Generate real, on-topic content (no Lorem ipsum).
6. Self-validate the JSON against the resolved schemas.
7. Call `pagebuilder_create_page` → the page is created as a **Draft**.

The assistant asks only for blocking essentials (store id is taken from context; page name
only if ambiguous). Permalinks are generated from the name.

### 2. Edit

> *"Add an FAQ section to the winter page." / "Change the homepage title." / "Remove the second banner."*

1. Resolve the target page's `groupId` — from the active-page context, from a page list in
   context, or via `pagebuilder_search_pages`.
2. `pagebuilder_get_page_content` to load the current JSON.
3. Plan concrete operations (add / remove / replace field / reorder). Ambiguity ("which
   section?") triggers a clarifying question before any mutation.
4. Mutate **in place, preserving identity**:
   - Existing section `id`s are kept; new ids only for added sections.
   - Sections the user did not ask to change stay byte-for-byte intact — no silent
     rewrites or reordering.
   - Editing a `markdown` field regenerates both `markdown` and `html`.
5. Self-validate, then `pagebuilder_save_page_content` → saved as a **Draft**.
6. Confirm what changed; if a draft already existed, the assistant notes it wrote into it
   (the user may have unrelated changes there).

### 3. Diagnose

> *"Why isn't /winter showing?" / "Is the homepage published?"*

Read-only. The assistant locates the page and reasons about visibility **in order**,
surfacing the first concrete cause:

1. **Status** — `Draft` (end-users don't see drafts; offers to publish), or `Archived`
   (must be restored in the admin UI; no tool for it).
2. **Visibility flag** `false` — hidden; the assistant reports it but does **not** auto-fix
   (no tool wraps the flag).
3. **Schedule / restriction** (via `pagebuilder_get_page_meta`) — `startDate` in the
   future, `endDate` in the past, or non-empty `userGroups`.

If none of these explain it, the assistant says so honestly rather than speculating — the
cause is then outside inspectable data (theme rendering, routing, caching, CDN).

### 4. Bulk

> *"Add a newsletter CTA to every landing page." / "All product pages should start with a hero."*

A request that spans multiple pages (`all`, `every`, `across`, `each`, plural targets) is
treated as Bulk even when the verb sounds like a single edit. See the dedicated section
below.

---

## The Bulk workflow in detail

Bulk is **not** a backend batch endpoint. It is the agent driving the same single-page
tools in a controlled loop, with explicit preview and confirmation gates. The flow:

1. **Enumerate.** `pagebuilder_search_pages` with `take: 100`, `statuses: "Draft,Published"`
   (Archived excluded unless asked). If `totalCount > 100`, the assistant **stops and asks
   the user to narrow scope** — it never paginates silently. The working set must be
   approved by a human.
2. **Pre-filter cheaply.** If the predicate is decidable from search results (name pattern,
   status, permalink shape), apply it now. Use `pagebuilder_get_page_meta` for metadata
   fields the search result strips (`userGroups`, `startDate`, `endDate`).
3. **Content-aware filter.** For predicates that require inspecting sections ("pages
   containing a CTA", "pages whose first section is a hero"), load each remaining candidate
   with `pagebuilder_get_page_content`, parse, and test. Parsed content is cached for step 5.
4. **Preview & confirm.** The assistant presents two explicit lists — **Will change**
   (`name (permalink)` per target) and **Skipped** (one-line reason each) — and waits for
   an explicit go-ahead. Silence is not consent.
5. **Apply, one page at a time.** For each target it mutates the cached content per the Edit
   rules (untouched sections and their ids stay identical), re-fetches only the schemas of
   sections it is actually editing, self-validates, and calls `pagebuilder_save_page_content`.
   It reports one-line progress per page (`✓ Updated 'name' (permalink)`).
6. **Hard cap — 20 pages per turn.** Larger sets are processed in waves of 20; after each
   wave the assistant summarizes and asks *"Continue with the next 20?"*. This keeps a
   runaway loop cheap to interrupt.
7. **Failure policy.** On the first 4xx/5xx it **stops**, shows the failing page, the backend
   error, and how many pages already saved, then asks whether to continue, stop, or revert
   manually. No silent retries; **no automatic rollback** (there is no rollback API).
8. **Summary.** *"Updated K of N pages as drafts. Skipped: …. Failed: …."*

> **Bulk never publishes.** Every page in a bulk run ends as a Draft. Publishing is always
> a separate, per-page, explicitly confirmed action.

---

## Safety and guardrails

The assistant operates under two distinct layers of protection — it is important to know
which guarantees are enforced by code and which are behavioral.

**Enforced by the platform (hard guarantees):**

- **Permissions.** Every tool maps to a platform permission (`builder:read`, `builder:create`,
  `builder:update`, `builder:publish`) checked by the API authorization handler. A user
  without `builder:publish` cannot publish, regardless of what the assistant attempts.
- **Approval prompts.** All write tools (`create_page`, `save_page_content`, `publish_page`)
  declare `require_approval`, so the host surfaces an approval dialog before the call runs.
- **Draft-first writes.** Create and edit always write a Draft; the live version is only
  ever touched by an explicit publish call.
- **Backend validation.** The page JSON envelope is validated server-side; a malformed
  document returns `400` with a precise error pointer (e.g. `content[2].type`).

**Behavioral (instructed in the agent, not enforced by code):**

- The 20-pages-per-turn bulk cap, stop-on-first-error, two-list preview/confirm, and the
  rule that bulk and edits never publish as a side-effect are all driven by the agent's
  system prompt. They depend on the model following instructions.
- For high-stakes bulk operations, the real backstop remains the per-write approval prompt
  and platform permissions — treat the prompt-level limits as strong defaults rather than
  inviolable guarantees.

**Publishing rules (every intent):** publishing is irreversible from the assistant's side
(the previously published version is deleted on publish; there is no rollback API). The
assistant always states in plain language exactly which page and what change is about to go
live and waits for an explicit yes — even when the user said "fix and publish", the fix
lands as a Draft first and publishing is re-confirmed separately. Unpublishing is treated
the same way. Bulk runs never publish.

---

## Setup notes

- **Store context.** The host shell must inject the `pagebuilder.store` context item so
  store-scoped tools can resolve `storeId`. Without it the assistant will ask the user for
  the store id rather than guess.
- **Active-page context.** When a page is open, the shell pushes the `pagebuilder.page`
  item so Edit/Diagnose can target it without a search.
- **Iframe embedding.** Virto OZ is embedded as a cross-origin iframe; the host and the OZ
  dev server must agree on the cross-origin headers (CSP `frame-ancestors`, COEP, CORP) and
  both must be served over HTTPS in development. The OZ URL is configured via `ozAgentUrl`
  in the shell's `settings.json`.
- **Theme resolution.** Schema and content tools accept an optional `theme`; when omitted
  the backend falls back to the store's default theme. Ensure the store's default theme
  resolves to a real theme folder, otherwise schema lookups return empty.

---

## Limitations and roadmap

The current release deliberately focuses on a tight, working slice: **prompt → page built
from the theme's blocks → saved through the Page Builder API → served by the storefront.**
Not yet included (tracked separately):

- Import from external sources (Google Docs link, uploaded file) — input is free-form text
  only for now.
- AI-driven approval/preview workflows and screenshot-based validation of the rendered
  page.
- Page versioning / rollback initiated by the assistant (the platform's own versioning
  still applies; the assistant only writes drafts and publishes on request).

---

## See also

- [Create a landing page](create-landing-page.md)
- [Block library](block-library.md)
- [Schemas](schemas.md)
- [Theme editor](theme-editor.md)
