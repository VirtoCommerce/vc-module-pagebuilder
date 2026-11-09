# Preview protocol, version 2

The designer shows the storefront in an iframe and drives it with `postMessage`. Version 2 adds a handshake that
names the protocol, what the storefront can do and the template rule conditions it knows; a draft layer for the
unsaved theme; one render message; inline text editing; and the light and dark switch.

The same storefront serves both designers: a store with the New Page Builder setting off previews in the Angular
designer, which speaks version 1. The storefront keeps every version 1 message (`page`, `changed`, `update`, `remove`,
`add`, `reload`, `swap`, `preview`, `navigate`, `select`, `hover`, `settings`, `auth`) and answers both designers with
the same `loaded`. The Vue designer never sends `settings`: it does not edit the theme settings
([theme snapshot](theme-snapshot.md#theme-settings)). A storefront whose `loaded` has no `protocol` (an older
vc-frontend, or a fork that did not update) does not work with the Vue designer: the designer says so and does not
render, and the store stays on the Angular designer.

## Frame and channel

- The preview URL is built with `URL`: `new URL(previewPath, storefrontUrl)`, then
  `searchParams.set("ep", designerOrigin)`. `previewPath` is the platform setting `StorePreviewPath`,
  `/designer-preview` by default.
- The iframe is `credentialless`: the preview has no cookies or storage of the admin's browser.
- The storefront takes `ep` only when it runs in a frame, keeps only its origin and accepts messages only when
  `event.source` is `window.parent` and `event.origin` is that origin. It posts to that origin only.
- The storefront's host allows framing only by the admin's origin (`frame-ancestors`).
- The designer accepts messages only when `event.source` is its iframe's window, `event.origin` is the storefront's
  origin and `data.source` is `"preview"`. It posts to the storefront's origin only, never to `"*"`.
- Each side drops a message whose `type` it does not know or whose payload does not have the shape below, and logs a
  warning.
- The storefront handles messages one at a time, in the order they arrive.

Designer to preview: `{ source: "builder", type, ...payload }`. Preview to designer: `{ source: "preview", type, data }`.

## Handshake

1. The designer loads the iframe.
2. On every iframe `load` the designer sends `connect`.
3. The storefront answers `loaded` once its schemas are ready. It also sends `loaded` unasked when it starts, in case
   `connect` came before its listener.
4. On every `loaded` the designer uploads the catalog when the server does not have its `hash`, then sends `auth`,
   `inspector`, `colorScheme`, `draft` and `render`, in that order: after a reload the frame has lost them all.
5. The storefront shows the page after the first `render`; until then the frame stays blank. Until told otherwise
   it is anonymous, light, with the inspector off: no outlines, clicks blocked.

```ts
// designer → preview
interface Connect {
  type: "connect";
  protocol: 2;
}

// preview → designer
interface Loaded {
  type: "loaded";
  data: Catalog & {
    protocol: 2;
    capabilities: Capability[];
    conditions: ConditionDescriptor[];
  };
}

type Capability =
  /** renders product, category and page templates on real pages from the draft layer */
  | "templates"
  /** applies `settings.layout` of page templates and pages */
  | "layouts"
  /** renders the header and footer groups; without it the designer hides them */
  | "groups"
  /** sends `edit` for texts edited in place */
  | "inline-edit"
  /** switches between light and dark on `colorScheme` */
  | "color-scheme"
  /** turns the section outlines and the click blocker on and off on `inspector` */
  | "inspector";

interface ConditionDescriptor {
  /** as in the rules: `inCategory`, `loyalty.tier` */
  name: string;
  pageTypes: ("product" | "category" | "page")[];
  label?: string;
  /** the condition's value as a schema field: the designer builds the condition's form from it */
  value: Field;
}

/** the schemas `loaded` has always carried, plus a hash and the palette tokens */
interface Catalog {
  /** changes whenever anything else in the catalog changes */
  hash: string;
  sections: Record<string, ItemSchema>;
  blocks: Record<string, ItemSchema>;
  shared: Record<string, ItemSchema>;
  objects: Record<string, ItemSchema>;
  templates: Record<string, TemplateEntry>;
  /** the theme settings' schema, which only the Angular designer reads */
  settingsSchema?: { settings_schema?: { name: string; icon?: string; settings: Field[] }[] };
  /** the token names a palette has, from the storefront's built-in presets */
  paletteTokens: { light: string[]; dark: string[] };
}
```

`Field`, `ItemSchema` and `TemplateEntry` are the schema types the storefront ships today (`schemas/**/*.json`), with
the additions in [Schemas](#schemas). The Angular designer reads the schema keys of `loaded.data` and ignores the
others. The Vue designer offers only the conditions in `conditions`, builds their forms from `value`, and never
evaluates rules itself: the storefront reports what it chose in `resolved`. A descriptor's `value` is a schema field
like any other: an `object` value (`property`, `setting`: `{ name, equals }`) describes its fields under `element`.

## Designer to preview

### `draft`

The unsaved theme, whole: every template, the rules, the groups, the brand and the palette as the designer has them,
saved or not. The storefront shows it in place of the published snapshot; a part the draft leaves out comes from the
snapshot. Each `draft` replaces the previous one entirely.

`templates` holds every template of the theme, or is left out: the storefront takes it in place of all the published
templates. Each is the template as last saved, its draft, else its live file; the open template is as the designer
has it, saved or not, and is in `render` too. The designer leaves `templates` out while no template has a saved draft
(the snapshot then holds them as they are live, and `render` carries the open one) and for a storefront without the
`templates` capability.

```ts
interface Draft {
  type: "draft";
  theme: {
    rules?: TemplateRules;
    templates?: Record<string, PageDocument>;
    header?: SectionGroup;
    footer?: SectionGroup;
    brand?: Brand;
    palette?: Palette;
  };
}
```

The types are those of the [theme snapshot](theme-snapshot.md#snapshot), except that translatable values may be
dictionaries: the storefront shows the current language. A menu the draft names and the published snapshot does not
carry, the storefront reads with the x-cms `menu` query. When `render.cultureName` is not the language of the snapshot
the storefront holds, it requests the snapshot in that language.

A version from the history is previewed the same way: the designer sends the theme as of that version as the draft and
its page as the render.

### `render`

```ts
interface Render {
  type: "render";
  /** the page to show; for a template, the template, which wins over `draft.theme.templates[templateKey]` */
  document: PageDocument;
  /** set while a theme template is open: the storefront shows `document` as that template, on the page at `url` */
  templateKey?: string;
  /** the storefront route to show the page or template on; absent: the designer's own preview route */
  url?: string;
  /** what a content page's rules see when the page is not at `url` */
  page?: { permalink: string; objectType: "Pages" | "ContentFile" };
  cultureName: string;
  selection?: { sectionId: string; blockId?: string; scroll: boolean };
  /** the section shown on hover in the add-section list, appended to the end */
  model?: Item;
  /** labels of the outlines, by section id */
  sectionLabels?: Record<string, string>;
  sharedComponentBoundaries?: SharedComponentBoundary[];
}

interface SharedComponentBoundary {
  placementId: string;
  componentRef: string;
  startIndex: number;
  count: number;
  label?: string;
}
```

Navigation and rendering are one message, so no state is kept between messages: a template shows on the storefront page
at `url`; a content page shows on the designer's preview route, and its rules see `page`, else `url` as its permalink.
`document` holds the page's own sections only: header and footer sections travel in `draft`. A page from the database is
`Pages`, a file page `ContentFile`.

### Other messages

| Type | Payload | What the storefront does |
|---|---|---|
| `hover` | `{ sectionId: string \| null }` | outlines the section, `null` removes the outline |
| `inspector` | `{ enabled: boolean }` | shows or hides the outlines; while an impersonation token is in use, clicks stay blocked with the inspector off |
| `colorScheme` | `{ scheme: "light" \| "dark" }` | switches the theme; without a dark palette, stays light |
| `auth` | `{ token: { access_token: string } \| null, userId: string \| null }` | renders as that user; `null` renders as an anonymous visitor |

`auth` carries the access token only, never a refresh token. The storefront keeps it in memory and sends it only to
its own `/graphql` and `/api`. The designer offers the accounts of the store setting
`VirtoCommerce.PageBuilderModule.Store.PreviewUserIds` to an editor with `platform:security:loginOnBehalf`, gets the
token from the platform's `/connect/token` `impersonate` grant (without `offline_access`), and sends it again, renewed,
before it expires.

## Preview to designer

| Type | Data | When |
|---|---|---|
| `loaded` | see [Handshake](#handshake) | in answer to `connect`, and once unasked |
| `select` | `{ sectionId: string, blockId?: string }` | the visitor clicked a section or a block while the inspector is on |
| `hover` | `{ sectionId: string \| null }` | the pointer entered or left a section |
| `edit` | `{ itemId: string, path: (string \| number)[], value: string, cultureName?: string }` | a text edited in place changed |
| `resolved` | see below | after every `render`, and when the page's data changes the choice |

```ts
interface Resolved {
  type: "resolved";
  data: {
    /** the route the choice was made for */
    url: string;
    pageType: "product" | "category" | "page";
    /** the template key the storefront rendered: while a template is open, that template */
    template: string;
    /** the rule that chose it; null for an open template, the page's own, the page type's or a built-in one */
    ruleId: string | null;
    /** what the store's rules pick for the page at `url`, the open template aside */
    rules: { template: string; ruleId: string | null };
    /** the theme fields white-labeling set for this visitor, which the theme does not change */
    overriddenByWhiteLabeling: WhiteLabelingField[];
  };
}

type WhiteLabelingField =
  | "brand.logoUrl"
  | "brand.secondaryLogoUrl"
  | "brand.faviconUrl"
  | "palette"
  /** the white-labeling main menu replaced the header's category menu (`catalog_menu` of `site-header`) */
  | "menus.header"
  /** the white-labeling footer links replaced the footer's columns (the `footer-menu` blocks of `site-footer`) */
  | "menus.footer";
```

`rules` is the template the store's rules, as in the current draft, else the snapshot, pick for the page at `url`
(steps 1–4 of [Template rules](theme-snapshot.md#template-rules)), whichever template is open; its `ruleId` is null
when no rule chose it. Every `resolved` carries it; with no template open it equals `template` and `ruleId`. The
designer's "Site uses … (rule N)" chip, a content page's Template hint and the rules panel's Test read `rules`.

The designer marks the fields in `overriddenByWhiteLabeling`, so the editor sees why a change to them does not show:
the brand fields and the palette on the Theme tab, `menus.header` on the `catalog_menu` setting of `site-header`,
`menus.footer` on each of the footer's `footer-menu` blocks, which the footer links replace whole (heading, menu and
links). White-labeling replaces no other menu: the header's icons menu is the theme's. The Angular designer drops `resolved`.

## Inline editing

With `inline-edit` and the inspector on, a click on a text of a section makes it editable. Each change sends `edit`;
the designer writes it to the draft, adds it to undo, and selects the section. While a text is being edited the
storefront holds back `render` and `draft` and applies the latest of each when editing ends, the draft first, so the edit
is not overwritten by its own echo.

- The storefront edits an element only when it knows the setting: the element is marked
  `data-pb-field="{setting path}"` inside an element marked `data-pb-section="{item id}"`, or exactly one plain-text
  setting of the item has the text it shows (for a translatable setting, its text in the current language).
- `itemId` is the innermost item, the block when the text is a block's; `path` is the setting's path inside it:
  `["title"]`, `["links", 0, "label"]`.
- For a translatable setting, `cultureName` is the language edited; the designer writes that language's text only.
  When the setting still held a plain string, the designer keeps that string as the store default language's text.
- The designer applies `edit` only to a setting the item's schema declares as plain text: `string`, with or without
  `multiline`. Rich text (`text`), `markdown` and links are edited in the settings panel.
- Outside the designer preview the storefront's markup does not change: no `data-pb-*` attributes, no editing code.

## Schemas

Schemas are data, never code.

- `visibility` is the only expression a schema runs. It is an expression of the designer's expression language
  (the `safe-expression` evaluator, the same in both designers): literals, `this.…` members, `!`, arithmetic,
  comparisons, `&&`, `||`, `??`, `?:`, array literals and the evaluator's helper functions; or one of its known forms,
  matched as written. Anything else is not evaluated, and the field shows.
- A field with `request` fetches options; `request.response.result` is a dotted path to the rows, not an expression.
  A URL on the platform's origin is requested with the admin token; a URL on any other origin is requested without
  the `Authorization` header, and a 401 from it does not end the designer's session. Expression keys only the Angular designer reads (`selector`, `optionsSelector`, `previewRule`) are
  ignored by the Vue designer.
- A field may be `"translatable": true` ([theme snapshot](theme-snapshot.md#translatable-fields)).
- An `object` field and a row of a `list` field hold the fields under `element`; a `list` field without `element` is a
  list of strings.
- Field types added for the theme:
  - `menu`: the name of a store link list; the designer lists the store's menus;
  - `color-token`: a palette token; the designer lists `paletteTokens`.
- `search`, as in the Angular designer: a typed query (or a `button`) runs the field's `request`, or its `requests` in
  turn, with the query as `__searchQuery`; the value is `{ __nodata, __searchQuery, value }` (with `requests`, an
  answer per key) and `displayInfo` lists what of it to show. The origin rule of `request` applies.
