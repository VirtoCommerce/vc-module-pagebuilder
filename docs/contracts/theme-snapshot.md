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
| `config/settings_data.json` | the theme settings of the Angular designer; see [Theme settings](#theme-settings) |
| `snapshots/{publication}/{cultureName}.json` | a published snapshot in one language, never changed after it is written |
| `snapshots/{publication}/publication.json` | the publication's record, `{ "defaultLanguage": "en-US" }`: the store default language it was written with |
| `snapshots/current.json` | the pointer to the current publication: `{ "publication": 12 }` |
| `catalogs/{hash}.json`, `catalogs/current.json` | the storefront schema catalogs, by hash, and the latest one's hash |

Every file except the snapshots and the catalogs has a draft next to it, `{file}-draft`. Only a publication writes
`snapshots/` and only a catalog upload writes `catalogs/`: a save or a restore naming either is refused with 400.
The designer and the agents write drafts only:
on a store with New Page Builder on, the builder's save refuses a live write of a theme file (`draft=false`) with 400.
The designer saves the page and the theme files it changed in one request, all or nothing
([Save](history-api.md#save)).
When `config/brand.json` does not exist yet, the designer and a publication take the brand from the current settings
of `config/settings_data.json`: `logo_image`, `logo_inverted_image` and `favicon_image`.

### Theme settings

The storefront built for the New Page Builder never reads the store theme's `config/settings_data.json`: it reads its
bundled `config/settings_data.json` only, and takes the brand and the palette from `config/brand.json` and
`config/palette*.json` through the snapshot. The Vue designer therefore does not edit `config/settings_data.json`: it
neither shows, saves, discards nor restores it. The Angular designer and `api/pagebuilder/save` keep editing it for
stores on the old designer, and a publication reads it only for the brand of a theme without `config/brand.json`.

## Publishing

Saves and publications of a store's theme run one at a time, under a distributed lock per store, so a save never lands
inside a publication and two publications never take the same number. A publication puts all the theme's drafts live
at once:

1. The server reads every theme file, the draft when there is one, else the live file.
2. It validates them against the section schema catalog the publication names (see
   [Section schema catalog](#section-schema-catalog)). A file that fails stops the publication; nothing is written.
3. For each store language it writes `snapshots/{n}/{cultureName}.json`, where `n` is one more than the highest
   publication of the theme; the translatable texts are resolved there
   ([Translatable fields](#translatable-fields)).
4. It writes the drafts over the live files.
5. It writes the record, `snapshots/{n}/publication.json`.
6. It rewrites `snapshots/current.json` to `n`. This is the moment the storefront sees the new theme.
7. It deletes the drafts.

Steps 1–7 hold the lock. The publication is then recorded in the history, after the lock, as one version holding every
file it put live: the history lives on GitHub, and a slow answer from it must not hold up the store's theme saves.

The pointer is written after the snapshots, the live files and the record, so it never names a publication that is not
complete. A failure in steps 3–6 puts the live files written so far back as they were and fails the publication: the
previous publication stays current, every draft stays, and nothing is recorded in the history. Publishing again does
the whole publication, under the next number. A failure in step 7 does not fail the publication: it is recorded in the
history, and each draft left holds what its live file holds. `theme/status` lists those files as drafts until the next
publication, which puts the same content live again.

A draft and its live file hold the same content from step 4 on, so the version the designer reads for a theme file (the
draft, else the live file) does not change across a publication.

A pointer that exists but is not valid JSON (a write cut off on a file system) is read as the highest complete
publication: the highest one whose record is whole. When none is, the server publishes the live files again, as on
the first request.

To go back to an earlier theme, the editor restores its version from the history and publishes it. The header, the
footer and the rules are published this way only, not through the Content module's publishing endpoint. Publishing a
page does not publish the theme.

## Request

`index.html` sends the request with an inline script before the application JavaScript loads, so the snapshot is
usually there before the first render. The script is in `index.html` only when the storefront is built with the Page
Builder flag of its bundled `config/settings_data.json`; it is marked `data-cfasync="false"` so that Cloudflare Rocket
Loader does not defer it.

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
  The storefront's `product-info` schema declares its default, the value of the storefront's built-in product
  template, so a product template the designer starts states it.
- Every template holds its main section: `product-info`, `category-listing` or `page-content`. The designer does not
  let it be removed or hidden; a storefront that gets a template without it renders the main section after the other
  sections itself. The designer does not add a missing one on its own: it says so and offers to add it (or to show a
  hidden one).
- A main section the designer adds, to a template it starts or one that misses it, gets the defaults its schema
  declares, as any section added.

### Template rules

```ts
interface TemplateRules {
  /** the rules format */
  version: 2;
  /** the condition names the rules use, sorted; see below */
  conditions?: string[];
  product?: TemplateRule[];
  category?: TemplateRule[];
  page?: TemplateRule[];
}

interface TemplateRule {
  /** unique within the page type's list; stable: kept when the rule changes or moves */
  id: string;
  /** a template key of the same page type */
  template: string;
  /** condition name → value; every condition must hold; none means always */
  when?: Record<string, unknown>;
  disabled?: boolean;
}
```

Writers (the designer, the agents, a publication) always write `conditions`. Readers never depend on it: the
designer and the agents warn when a storefront does not know a condition the rules use, which they read from the rules
themselves, so a file without `conditions` is valid.

Rule ids are unique within a page type's list; writers never write two alike. A reader gives each rule it reads (one
whose `template` is a string and whose `when` is an object or absent) its id this way: a rule whose `id` is not a
string, is empty or is the id of an earlier rule of the list gets `{pageType}-{index}`, `index` being its position
among the rules read, from 0; when that id is taken too, `{pageType}-{index}-{n}` with the first `n` from 2 that is
free. The server's snapshot builder and the storefront read a list the same way, so `resolved.ruleId`
([preview protocol](preview-protocol.md#preview-to-designer)) names the same rule on both sides.

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

A rules file of `version` 1 has no ids and no `conditions`; it is read the same way, so its rules get
`"{pageType}-{index}"` as their ids, and the designer writes it as `version` 2 on its next save.

### Conditions

The storefront keeps a registry of conditions, `{ name, pageTypes, value, evaluate(subject, value) }`, and announces
it in the preview handshake ([preview protocol](preview-protocol.md#handshake)). Built-in conditions:

| Name | Page types | Value | Holds when |
|---|---|---|---|
| `inCategory` | product, category | category ids, `string[]`; a string of ids separated by commas is read as that list | one of the ids is the category itself or a segment of one of the subject's outlines |
| `productType` | product | `string` | the product type equals it |
| `property` | product | `{ name, equals }` | a property with that name has the value `equals`; for a multi-value property, one of its values |
| `path` | page | `string`, `*` matches any characters | the page's permalink matches; case and the leading and trailing `/` are ignored |
| `language` | page | `string` | the page language equals it, or its language part does |
| `source` | page | `string` | the page's slug `objectType` equals it (`Pages`, `ContentFile`) |
| `setting` | page | `{ name, equals }` | the page's setting with that name has the value `equals` |

The descriptor of `inCategory` declares its value as a list of strings: a `list` field without `element`
([Schemas](preview-protocol.md#schemas)).

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

The keys of a dictionary are culture names as the store's languages list them (`en-US`, `sr-Latn-RS`). A reader takes
an object for a dictionary when its keys are all culture names of BCP-47 shape, `^[a-z]{2,3}(-[a-z0-9]{2,8})*$`
ignoring case, one of them with a subtag, and its values are all texts: a row such as `{ "sku": "X1" }` stays a row.
The designer keeps a plain string until a language other than the store default gets a text of its own; an emptied
text removes that language's entry, and a dictionary left with the default language's text alone is a string again.
The publication resolves every dictionary: in the snapshot of a language it is a string, the text of that language,
else of its language part (`de` for `de-AT`), else of a culture of the same language (`de-DE`), else of the store
default language, else the first text of the dictionary; culture names are compared ignoring case. A snapshot never
holds one.

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

White-labeling wins over the theme: the organization's white-labeling, then the store's (merged in `pageContext`), then
the theme, then the storefront's bundled `config/settings_data.json` (`logo_image`, `logo_inverted_image`). The field
names are the white-labeling ones.

- `logoUrl`, the header's logo, goes field by field: the storefront takes the theme's only when white-labeling left it
  empty. The theme's `faviconUrl` shows when white-labeling has no favicons.
- The header's logo for dark backgrounds (the header in the dark theme) comes from the source of the light logo:
  white-labeling's `secondaryLogoUrl` when white-labeling sets `logoUrl`, else the theme's when the theme sets
  `logoUrl`, else the bundled `logo_inverted_image`. A light logo of one source is never paired with a dark logo of
  another: when white-labeling sets `logoUrl`, the header never shows the theme's `secondaryLogoUrl`. When that source
  has no dark logo, or white-labeling's `logoUrl` is the organization's (its `secondaryLogoUrl` may be the store's),
  the header shows the light logo in the dark theme too.
- The footer's logo goes field by field: white-labeling's `secondaryLogoUrl`, else the theme's, else the bundled
  `logo_inverted_image`.

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

### Contrast pairs

The server checks a palette against the pairs of colors the storefront puts on one another, by WCAG 2.2 relative
luminance: text needs 4.5:1, icons and controls 3:1 (`ModuleConstants.Palette`). The list is
`ThemePaletteService.GetContrastPairs` (Data), which a module can override with the token rules. Each side of a pair is a token and the token the storefront's stylesheets
(`_colors.scss`, `_ui-kit-tokens.scss`) fall back to when the preset does not define it.

| Pair | Text (fallback) | Background (fallback) | Ratio |
|---|---|---|---|
| `body-text` | `color_body_text` (`color_neutral_950`) | `color_body_bg` (`color_neutral_50`) | 4.5 |
| `link`, `link-hover` | `color_link` (`color_accent_600`), `color_link_hover` (`color_accent_700`) | `color_body_bg` (`color_neutral_50`) | 4.5 |
| `price` | `color_price` (`color_neutral_900`) | `color_additional_50` | 4.5 |
| `shape-icon` | `color_shape_icon` (`color_additional_50`) | `color_shape_icon_bg` (`color_secondary_500`) | 3 |
| `empty-list-icon` | `color_empty_list_icon` (`color_primary_500`) | `color_body_bg` (`color_neutral_50`) | 3 |
| `header-top-{text,link,link-hover,link-active}` | `color_header_top_*` (`additional_50`, `accent_300`, `additional_50`, `accent_300`) | `color_header_top_bg` (`color_secondary_900`) | 4.5 |
| `header-bottom-…` | `color_header_bottom_*` (`neutral_900`, `secondary_700`, `neutral_900`, `neutral_900`) | `color_header_bottom_bg` (`color_additional_50`) | 4.5 |
| `footer-top-…` | `color_footer_top_*` (`additional_50`, `neutral_400`, `additional_50`, `additional_50`) | `color_footer_top_bg` (`color_secondary_900`) | 4.5 |
| `footer-bottom-…` | `color_footer_bottom_*` (`additional_50`, `accent_300`, `additional_50`, `additional_50`) | `color_footer_bottom_bg` (`color_secondary_950`) | 4.5 |
| `mobile-menu-{text,link,link-active,navigation}` | `color_mobile_menu_*` (`additional_50`, `secondary_200`, `additional_50`, `primary_400`) | `color_mobile_menu_bg` (`color_secondary_900`) | 4.5 |
| `mobile-menu-{icon,icon-active,control}` | `color_mobile_menu_*` (`secondary_600`, `additional_50`, `primary_500`) | `color_mobile_menu_bg` (`color_secondary_900`) | 3 |
| `button-solid-{c}` | `color_vc_text_solid_{c}` (`additional_50`; warning: `warning_900`) | `color_vc_background_solid_{c}` (`{c}_500`) | 4.5 |
| `button-soft-{c}`, `button-tonal-{c}` | `color_vc_text_{soft,tonal}_{c}` (`{c}_800`) | `color_vc_background_{soft,tonal}_{c}` (`{c}_100`) | 4.5 |
| `button-outline-{c}`, `button-surface-{c}` | `color_vc_text_{outline,surface}_{c}` (`{c}_500`; secondary: `secondary_600`; warning: `warning_700`) | `color_vc_background_{outline,surface}_{c}` (`additional_50`) | 4.5 |
| `button-ghost-{c}` | `color_vc_text_ghost_{c}` (as outline) | `color_body_bg` (`color_neutral_50`) | 4.5 |

`{c}` is each of primary, secondary, accent, neutral, success, warning, danger and info; fallbacks are token names
without the `color_` prefix. Badges and chips use the button tokens.

A side takes the palette's color for its token. When the palette leaves the token out, it takes the fallback's color
only if the storefront did not announce the token (its preset does not define it, so the stylesheet falls back); a
token the storefront announced and the palette leaves out keeps the preset's color, which the server does not know,
and the pair is not checked. A value that is not `#rrggbb` or `#rgb` is not checked either. The light and the dark
part are checked each with its own tokens; a part without colors is not checked.

### Palette generation

The generator builds both parts over every announced token from one or two colors: the primary, and an optional
second color for the secondary and accent families. With one color the secondary is the primary's hue, muted, and the
accent is the primary. Neutral, success, warning, danger and info start from the default preset's colors. Each family
gets shades 50–950 around its color as 500; the dark shades invert unevenly, as the storefront's dark presets do.
The other tokens take the shade the storefront's fallbacks name (light) or its default dark preset uses (dark); the text
on a solid fill is white or near black, whichever contrasts more. Then shades move in lightness, towards black or white,
until every pair passes; a move is kept only when the pairs fall short by less in total, and a color's own shade 500
moves only when no other shade can. The white and black (`color_additional_50`, `color_additional_950`) never move.
A token the generator has no rule for is left out and keeps the preset's color.

## Section schema catalog

The catalog is the storefront's section, block, template and palette schemas, the `loaded` data of the
[preview protocol](preview-protocol.md#handshake), named by its `hash`.

- The designer uploads the catalog of the storefront it previews when the server does not have that hash. Uploading
  takes `builder:update`, the designer's own permission, so an editor of templates or a publisher is not blocked by a
  new storefront. The server keeps every catalog by its hash and never changes one; the latest upload is the
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
| `POST api/pagebuilder/save?storeId&theme&draft&catalogHash`, the files as the body | `builder:update`; `builder:templates` or `builder:theme` by theme file | 200; 400 `{ error, errors }` when a theme file does not fit the catalog it names; 400 `{ error }` for a live theme write on a New Page Builder store, or for a file under `snapshots/` or `catalogs/` |
| `GET api/pagebuilder/designer/theme-file?storeId&path` | `builder:read` | `{ path, content?, eTag }`: a theme file as the designer edits it and its version, see [Save](history-api.md#save) |
| `POST api/pagebuilder/designer/save?storeId&catalogHash` | see [Save](history-api.md#save) | the designer's Save: the page and the theme files as drafts, all or nothing, one version; 412 when one was saved by someone else since it was read |
| `POST api/pagebuilder/designer/discard?storeId` | see [Discard](history-api.md#discard) | the designer's Discard: the drafts of the page and the theme files go, all or nothing, one version |
| `PUT api/pagebuilder/catalog?storeId`, the catalog as the body | `builder:update` | `{ hash }`; 400 when it has no valid `hash` |
| `GET api/pagebuilder/catalog?storeId&hash` | `builder:read` | the catalog with that hash, the current one without it; 404 when there is none |
| `POST api/pagebuilder/theme/palette?storeId`, `{ primary, secondary?, tokens: { light, dark } }` as the body | `builder:theme` | `{ light, dark, failures }`: the generated palettes and the pairs they still fail (none so far); 400 when a color is not `#rrggbb`/`#rgb` or no light tokens are named |
| `POST api/pagebuilder/theme/palette/check?storeId`, `{ palette: { light, dark? }, tokens: { light, dark } }` as the body | `builder:theme` | `{ failures, unchecked }`: the pairs the palette fails and how many pairs could not be checked; 400 without a palette or tokens |

A failure is `{ scheme, pair, text, background, textColor, backgroundColor, ratio, minimumRatio }`: `text` and
`background` are the tokens whose colors were compared (the pair's own or its fallback), `ratio` is rounded down to two
decimals. `tokens` are the storefront's `paletteTokens` ([handshake](preview-protocol.md#handshake)).
