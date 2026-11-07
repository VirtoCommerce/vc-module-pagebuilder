# Theme snapshot

The storefront gets the published theme of a store in one anonymous GraphQL request: the snapshot. It holds the
template rules, every template, the header and footer groups, the brand and the palette, with texts in one language.
The storefront does not read theme files from `/cms-content`.

The snapshot exists for stores with the store setting **New Page Builder** on. For any other store the request returns
`null`, and the storefront renders its built-in templates. Turning the setting on publishes a first snapshot from the
theme's live files, so a store with the setting on always has one.

## Theme files

A theme is a folder in the Content module's `themes` storage of the store. The active theme is the one named by the
store's `DefaultThemeName` property, `default` when it is empty. Paths below are relative to the theme folder.

| File | What it holds |
|---|---|
| `templates/{key}.json` | a template document, one per template key |
| `config/template_rules.json` | the template rules |
| `sections/header-group.json`, `sections/footer-group.json` | the header and footer groups |
| `config/brand.json` | the logos and the favicon |
| `config/palette.json`, `config/palette.dark.json` | the light and the dark palette |
| `snapshots/{publication}/{cultureName}.json` | a published snapshot in one language, never changed after it is written |
| `snapshots/{publication}/publication.json` | the publication's record, `{ "defaultLanguage": "en-US" }`: the store default language it was written with |
| `snapshots/current.json` | the pointer to the current publication: `{ "publication": 12 }` |
| `catalogs/{hash}.json`, `catalogs/current.json` | the storefront schema catalogs, by hash, and the latest one's hash |

Every file except the snapshots has a draft next to it, `{file}-draft`. The designer and the agents write drafts only:
on a store with New Page Builder on, the builder's save refuses a live write of a theme file (`draft=false`) with 400.
The designer saves the page and the theme files it changed in one request, all or nothing
([Save](history-api.md#save)).
When `config/brand.json` does not exist yet, the designer starts the brand from `config/settings_data.json`:
`logo_image`, `logo_inverted_image` and `favicon_image`.

## Publishing

Saves and publications of a store's theme run one at a time, under a distributed lock per store, so a save never lands
inside a publication and two publications never take the same number. A publication puts all the theme's drafts live
at once:

1. The server reads every theme file, the draft when there is one, else the live file.
2. It validates them against the section schema catalog the publication names (see
   [Section schema catalog](#section-schema-catalog)). A file that fails stops the publication; nothing is written.
3. For each store language it writes `snapshots/{n}/{cultureName}.json`, where `n` is one more than the highest
   publication of the theme; the translatable texts are resolved there
   ([Translatable fields](#translatable-fields)). Then it writes the record, `snapshots/{n}/publication.json`.
4. It rewrites `snapshots/current.json` to `n`. This is the moment the storefront sees the new theme.
5. It writes the drafts over the live files and deletes the drafts.

Steps 1–5 hold the lock. The publication is then recorded in the history, after the lock, as one version holding every
file it put live: the history lives on GitHub, and a slow answer from it must not hold up the store's theme saves.

The snapshots and the record are written before the pointer, so the pointer never names a publication that is not
complete. A pointer that exists but is not valid JSON (a write cut off on a file system) is read as the highest
complete publication: the highest one whose record is whole. When none is, the server publishes the live files again,
as on the first request. When step 5 fails, the publication stands; publishing again writes the live files.

To go back to an earlier theme, the editor restores its version from the history and publishes it. The header, the
footer and the rules are published this way only, not through the Content module's publishing endpoint. Publishing a
page does not publish the theme.

## Request

`index.html` sends the request with an inline script before the application JavaScript loads, so the snapshot is
usually there before the first render. The script is in `index.html` only when the storefront is built with the Page
Builder flag of `settings_data.json`; it is marked `data-cfasync="false"` so that Cloudflare Rocket Loader does not
defer it.

```js
fetch("/graphql", {
  method: "POST",
  headers: { "content-type": "application/json" },
  body: JSON.stringify({
    operationName: "ThemeSnapshot",
    query: THEME_SNAPSHOT_QUERY,
    variables: { domain, cultureName },
  }),
});
```

- `domain` is the value the storefront passes to `pageContext` as `domain`, in development too.
- `cultureName` is the language prefix of the URL when there is one, else the pinned locale (`localStorage`
  `pinnedLocale`), else it is left out.
- The request carries no token: the snapshot is the same for every visitor.

```graphql
query ThemeSnapshot($domain: String, $storeId: String, $cultureName: String) {
  themeSnapshot(domain: $domain, storeId: $storeId, cultureName: $cultureName) {
    storeId
    cultureName
    publication
    content
    menus {
      name
      items {
        title
        url
        priority
        childItems {
          title
          url
          priority
        }
      }
    }
  }
}
```

The field is part of the x-cms schema: `/graphql` reaches the platform on every storefront host.

| Argument | Meaning |
|---|---|
| `domain` | the store is found by domain, as for `pageContext` |
| `storeId` | the store by id; wins over `domain` |
| `cultureName` | the language asked for; optional |

| Field | Meaning |
|---|---|
| `storeId` | the store the snapshot belongs to |
| `cultureName` | the language the texts are in |
| `publication` | the number of the publication |
| `content` | the snapshot JSON, [described below](#snapshot), as a string |
| `menus` | the link lists the header and footer name, with their links |

The field is `null` when the store has the New Page Builder setting off. A storage failure is a GraphQL error; the
storefront then renders its built-in templates and asks again on the next navigation inside the site.

The application uses the response only when `storeId` is the store `pageContext` resolved; otherwise it requests the
snapshot again with that `storeId`. When `pageContext` gives another language (a signed-in contact with their own
language), it requests the snapshot once more with that language.

### Language

The server checks `cultureName` against the store's languages before it touches the cache:

1. a store language equal to it, ignoring case;
2. else a store language with the same language part (`de` and `de-de` give `de-DE`), the store default when it is
   one of them, else the first of them in the store's order;
3. else, and when `cultureName` is left out, the store default language.

A language the publication has no file for (added to the store after it) gets the file of the store default language;
a store default it has no file for (chosen after it), the file of the default language its record names. The response
names the language it served.

### Menus

Section settings of the type `menu` hold the name of a store link list. The snapshot lists those names in `menus`; for
each of them the response carries the list as the x-cms `menu` query returns it for the served language, under the
name the setting holds. The storefront matches a setting to `menus[].name` ignoring case. Menus are read on every
request and are not part of the cached snapshot, so a menu edited in Content shows without a new publication.

### Cache

The server caches the snapshot of each store and language, with its publication number, for an absolute time, the
setting `PageBuilder:ThemeSnapshot:CacheAbsoluteExpiration`, instead of the platform's sliding expiration, under which
an entry that is read often never expires. It reads the pointer only when the entry is missing. Publishing resets the store's
entries on its own node at once and on the other nodes through Redis when it is configured; without Redis the other
nodes serve the previous theme until their entries expire.

A snapshot larger than the setting `PageBuilder:ThemeSnapshot:SizeWarningThreshold` (gzip bytes) is published and
logged as a warning.

## Snapshot

```ts
interface ThemeSnapshot {
  /** the snapshot format; a storefront ignores a snapshot whose format it does not know */
  format: 1;
  publication: number;
  /** ISO 8601 */
  publishedDate: string;
  /** the theme folder it was built from */
  theme: string;
  /** the language of the texts */
  cultureName: string;
  /** the hash of the section schema catalog the publication was validated against */
  catalogHash: string;
  rules: TemplateRules;
  /** every template of the theme, by key */
  templates: Record<string, PageDocument>;
  header?: SectionGroup;
  footer?: SectionGroup;
  brand: Brand;
  palette?: Palette;
  /** the names held by `menu` settings of the header and footer */
  menus: string[];
}
```

### Documents and sections

```ts
interface PageDocument {
  settings: Item;
  content: Item[];
}

/** a section, or a block of a section */
interface Item {
  id: string;
  /** the schema type; never renamed once it is stored */
  type: string;
  hidden?: boolean;
  blocks?: Item[];
  /** the values of the settings the type's schema declares */
  [setting: string]: unknown;
}

interface SectionGroup {
  content: Item[];
}
```

The storefront renders only section types it registered and passes a section only the settings its schema declares.
A snapshot never holds a shared component (`componentRef`): shared components are page content. A publication refuses a
theme file that places one; the first publication, from the live files, leaves it out. The header group holds
`site-header` and the announcement bar, the footer group holds `site-footer`; neither has a logo setting of its own:
they show the brand's logos.

### Templates

A template key is the page type, optionally followed by a dot and a name: `product`, `product.b2c`,
`category.sale`, `page.landing`. Page types are `product`, `category` and `page`.

- `settings.layout` of a `page` template is `full` (the site header and footer), `minimal` (the logo only) or `blank`
  (nothing). A page may set its own `settings.layout`, which wins. Product, category, cart, checkout and account
  pages keep the layout of their route.
- A product template states how variations load: `variationsLayout` is `b2b` or `b2c` on its `product-info` section.
- Every template holds its main section: `product-info`, `category-listing` or `page-content`. The designer does not
  let it be removed or hidden; a storefront that gets a template without it renders the main section after the other
  sections itself.

### Template rules

```ts
interface TemplateRules {
  /** the rules format */
  version: 2;
  /** the condition names the rules use: the designer and the agents warn when a storefront does not know one */
  conditions: string[];
  product?: TemplateRule[];
  category?: TemplateRule[];
  page?: TemplateRule[];
}

interface TemplateRule {
  /** stable: kept when the rule changes or moves */
  id: string;
  /** a template key of the same page type */
  template: string;
  /** condition name → value; every condition must hold; none means always */
  when?: Record<string, unknown>;
  disabled?: boolean;
}
```

The storefront picks a page's template synchronously, from the snapshot in memory:

1. a content page's own `settings.template`, when it names a template of its page type;
2. else the first rule of the page type that is not disabled and whose every condition holds;
3. else the template keyed by the page type (`product`);
4. else its built-in template.

A rule naming a template that neither the snapshot nor the storefront's built-in templates (`product`, `product.b2c`,
`category`, `page`) have is skipped, so the default B2C rule keeps the built-in B2C template while the theme has none of
its own. A condition the storefront does not know never holds, so a rules file from a newer designer is safe for an
older storefront.

When the rules have no list for a page type, the default rules of that type apply. An empty list means no rules. The
default rules are:

```json
{
  "product": [
    {
      "id": "default-b2c",
      "template": "product.b2c",
      "when": { "property": { "name": "VirtoFrontend_UI_Layout", "equals": "B2C" } }
    }
  ]
}
```

A rules file of `version` 1 has no ids and no `conditions`; the storefront reads it with `"{pageType}-{index}"` as the
ids, and the designer writes it as `version` 2 on its next save.

### Conditions

The storefront keeps a registry of conditions, `{ name, pageTypes, value, evaluate(subject, value) }`, and announces
it in the preview handshake ([preview protocol](preview-protocol.md#handshake)). Built-in conditions:

| Name | Page types | Value | Holds when |
|---|---|---|---|
| `inCategory` | product, category | category ids, `string[]` | one of the ids is the category itself or a segment of one of the subject's outlines |
| `productType` | product | `string` | the product type equals it |
| `property` | product | `{ name, equals }` | a property with that name has the value `equals`; for a multi-value property, one of its values |
| `path` | page | `string`, `*` matches any characters | the page's permalink matches; case and the leading and trailing `/` are ignored |
| `language` | page | `string` | the page language equals it, or its language part does |
| `source` | page | `string` | the page's slug `objectType` equals it (`Pages`, `ContentFile`) |
| `setting` | page | `{ name, equals }` | the page's setting with that name has the value `equals` |

Values are compared exactly, case and spaces included, except for `path`: a product whose `VirtoFrontend_UI_Layout`
is `b2c` or `B2C ` does not match the default rule, as in production today.

The subject of a product is the product with all its outlines, not only the outline of the path it was reached by,
so the template does not depend on where the visitor came from. The subject of a category is the category and its
outline from the slug. The subject of a content page is its permalink, language, settings and slug `objectType`.

Plugins add conditions under their own prefix (`loyalty.tier`) through the same registry.

### Translatable fields

A setting whose schema field has `"translatable": true` holds, in the theme files and drafts, either a string, the
same in every language, or a dictionary from language to text:

```json
{ "text": { "en-US": "Free shipping", "de-DE": "Kostenloser Versand" } }
```

A dictionary is an object whose keys are all language names with a subtag (`en-US`) and whose values are all texts.
The publication resolves every dictionary: in the snapshot of a language it is a string, the text of that language,
else the text of the store default language, else the first text of the dictionary. A snapshot never holds one.

### Brand

```ts
interface Brand {
  /** the logo on light backgrounds: the header in the light theme */
  logoUrl?: string;
  /** the logo on dark backgrounds: the footer, and the header in the dark theme */
  secondaryLogoUrl?: string;
  faviconUrl?: string;
}
```

White-labeling wins over the theme, field by field: the organization's white-labeling, then the store's, then the
theme, then the storefront's `settings_data.json`. The field names are the white-labeling ones, so the storefront
takes a theme field only when white-labeling left it empty.

### Palette

```ts
interface Palette {
  light: Record<string, string>;
  /** without it, the storefront has no dark theme */
  dark?: Record<string, string>;
}
```

The keys are the tokens of the storefront's built-in presets (`client-app/assets/presets/{name}.json` and
`{name}.dark.json`); the storefront lists them in the handshake (`paletteTokens`). A token a palette leaves out keeps
the color of the preset; a publication keeps a palette only when its light part has a color. The storefront applies it in place of the preset, as it applies a preset, in
`<style id="vc-theme-variables">`: the light part under `:root:not(.dark)`, the dark part under `html.dark`.

A white-labeling preset (`themePresetName`) replaces the whole palette; the two are never mixed. Without a palette
the storefront's preset works as now.

Section settings of the type `color-token` hold a palette token name, not a color, so that a section such as the
announcement bar follows the light and dark theme.

## Section schema catalog

The catalog is the storefront's section, block, template and palette schemas, the `loaded` data of the
[preview protocol](preview-protocol.md#handshake), named by its `hash`.

- The designer uploads the catalog of the storefront it previews when the server does not have that hash. Uploading
  takes `builder:theme`. The server keeps every catalog by its hash and never changes one; the latest upload is the
  store's current catalog, which agents read.
- A save and a publication name the hash of the catalog the designer previewed. The server validates the theme files
  against that catalog with the same rules (every section and block type is one the storefront has, and a theme file
  places no shared component) and answers 400 `{ error, errors }` when one does not fit or the hash is unknown.
  A publication records the hash in the snapshot; one that names none is refused. A save that names none is not
  checked (agents, the Angular designer). The first publication, from the live files, names none and checks the shape
  only; it finds the menus through the store's current catalog, when there is one.
- A schema is data: see [Schemas](preview-protocol.md#schemas).

## Endpoints

All take `storeId` and check the store as the other Page Builder endpoints do.

| Method and route | Permission | Returns |
|---|---|---|
| `POST api/pagebuilder/theme/publish?storeId&catalogHash` | `builder:publish` | `{ publication }`; 400 `{ error, errors }` when it names no catalog or the theme does not fit it |
| `GET api/pagebuilder/theme/status?storeId` | `builder:read` | `{ publication, drafts }`: the current publication, left out before the first, and the theme files with a draft by their path in the theme (`templates/product.json`, `config/template_rules.json`) |
| `POST api/pagebuilder/save?storeId&theme&draft&catalogHash`, the files as the body | `builder:update`; `builder:templates` or `builder:theme` by theme file | 200; 400 `{ error, errors }` when a theme file does not fit the catalog it names; 400 `{ error }` for a live theme write on a New Page Builder store |
| `POST api/pagebuilder/designer/save?storeId&catalogHash` | see [Save](history-api.md#save) | the designer's Save: the page and the theme files as drafts, all or nothing, one version |
| `POST api/pagebuilder/designer/discard?storeId` | see [Discard](history-api.md#discard) | the designer's Discard: the drafts of the page and the theme files go, all or nothing, one version |
| `PUT api/pagebuilder/catalog?storeId`, the catalog as the body | `builder:theme` | `{ hash }`; 400 when it has no valid `hash` |
| `GET api/pagebuilder/catalog?storeId&hash` | `builder:read` | the catalog with that hash, the current one without it; 404 when there is none |
