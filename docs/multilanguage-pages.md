# Multilanguage Pages

A **Multilanguage Page** is one page with a version for every store language. Each language version is a regular Page Builder page with its own content, permalink, status, and design, while the Multilanguage Page keeps what they share: the name and the default settings.

When a visitor switches the storefront language, they land on the same page in the new language, at that language's own permalink. For example, `/veelgestelde-vragen` in Dutch and `/faq` in English are the same page, and switching from Dutch to English opens `/faq`.

A **Landing Page** is a page in a single language, as before.

## At a glance

| | |
|---|---|
| **Available when** | The store has more than one language. |
| **Shared by all languages** | Name. |
| **Inherited by default** | Permalink, Visibility, User groups, Organization, Start date, End date. A language can set its own value. |
| **Per language** | Content, status (Draft, Published, Archived), design. |
| **Source language** | The default language of the Multilanguage Page (the store default language unless changed). New versions copy its content; translations are flagged as outdated when it is republished. |
| **Storefront** | Language switch moves to the translation's permalink; `<link rel="alternate" hreflang>` for every published translation plus `x-default`. |

## Create a Multilanguage Page

1. Open the Page Builder for the store.
1. Click **Add** in the toolbar.
1. Select **Multilanguage Page**. If the store has a single language, a Landing Page opens directly.
1. Fill in **Name** and **Permalink**. **Default language** is the store default language unless you choose another one.
1. Optionally set **Advanced options** (Personalization & Access control, Scheduling). They are the defaults for every language.
1. Click **Save**. The **Languages** table appears.

## Languages table

The table lists every store language, the default language first:

| Column | Meaning |
|---|---|
| **Language** | Culture name; *default* marks the source language, *not a store language* marks a version whose language was removed from the store. |
| **Permalink** | The version's permalink with an *inherited* or *custom* badge. |
| **Status** | Published, Draft, Archived, Missing (no version yet), with *Outdated* when the translation is behind the source language. |
| **Actions** | See below. |

Actions for a language that has a version:

* **Open** – opens the version's page blade, where you edit its permalink and advanced options.
* **Open designer** – edits the version's content.
* **Mark up to date** – shown for outdated translations whose source change needs no translation.
* **Unlink** – turns the version into a separate Landing Page with its current values.

Actions for a missing language:

* **Create** – creates a draft version that starts from the default language content.
* **Link existing** – makes an existing Landing Page in that language the version. The page keeps its permalink and settings and takes the shared name, so the storefront keeps serving it at the same address.

## Inherited and custom values

A language version inherits the Multilanguage Page values until you change them for that language. In the version's page blade every inheritable field shows whether it is *inherited from the multilanguage page* or *set for this language*. Setting a field back to the Multilanguage Page value makes it inherited again.

When you change a setting of the Multilanguage Page that some languages set themselves, saving asks how to apply it:

* **Apply to all languages** – the languages lose their own value of the changed settings and inherit the new one.
* **Apply to inheriting languages only** – languages with their own value keep it.
* **Cancel**.

## Publish and archive

* **Publish all** publishes the draft of every language version that has one; the others are reported as skipped. The source language is published first.
* **Archive all** archives every language version.
* **Delete** deletes the Multilanguage Page and keeps its language versions as separate Landing Pages.
* Each version can still be published, unpublished, or archived on its own from its page blade.

## Outdated translations

Publishing the source language version starts a new revision. A translation published (or created as a copy, or marked up to date) before that is shown as **Outdated** in the Languages table and in the pages list, until it is republished or marked up to date.

Pages published before this feature have no revision yet, so their translations become outdated only after the source language is republished.

## Convert, clone, and permalink check

* **Convert to multilanguage** on a Landing Page makes it the default language version of a new Multilanguage Page. Nothing changes on the storefront.
* **Clone** on a Multilanguage Page copies it with every language version as drafts; the name gets *(copy)* and permalinks a *-copy* suffix.
* Saving a page whose permalink is already used by another active page in the same language asks for confirmation: the storefront serves only one page per permalink and language.

## Pages list

A Multilanguage Page is a single row of type *Multilanguage* with a status pill per language: published, published with unpublished changes, draft, outdated (dashed yellow), archived (red), missing (dashed gray).

The **Filters** panel adds:

* **Page type** – Landing Page or Multilanguage.
* **Language** – pages with a version in the language.
* **Translation status** – *Missing* (no version in the selected language, or in some store language) or *Outdated*.

## Storefront

* Switching the language on a Multilanguage Page opens the translation's permalink, e.g. `/fr/questions` → `/faq`.
* Every translated page outputs `<link rel="alternate" hreflang="…">` for each published translation and `x-default` for the default language version.
* When a page has translations but none in the visitor's language, the **VirtoPages.MissingTranslationBehavior** store setting decides:
    * `DefaultLanguage` (default) – the default language version is shown.
    * `NotFound` – the page is not found.

Pages must be enabled for the store (**VirtoPages.Enable** store setting) for the storefront to serve Page Builder pages.

## Export and import

Platform export writes Multilanguage Pages before the pages, and every page keeps its link to its Multilanguage Page. Import restores the Multilanguage Pages first, so the order in the file does not matter. A language version whose Multilanguage Page is not in the target is imported as a Landing Page.

## API reference

All endpoints require the Page Builder permissions noted and are scoped to the user's store.

| Endpoint | Permission | Purpose |
|---|---|---|
| `POST api/page-builder-multilanguage-pages/search` | `builder:read` | Search Multilanguage Pages. |
| `GET api/page-builder-multilanguage-pages/{id}` | `builder:read` | Get a Multilanguage Page. |
| `POST api/page-builder-multilanguage-pages` | `builder:create` | Create. |
| `PUT api/page-builder-multilanguage-pages` | `builder:update` | Update; inherited settings are applied to the language versions. |
| `DELETE api/page-builder-multilanguage-pages/{id}` | `builder:delete` | Delete; versions become Landing Pages. |
| `GET api/page-builder-multilanguage-pages/{id}/languages` | `builder:read` | Languages table. |
| `POST api/page-builder-multilanguage-pages/{id}/languages/{cultureName}?copyContent=true` | `builder:create` | Create a missing language version. |
| `POST api/page-builder-multilanguage-pages/{id}/link/{groupId}` | `builder:update` | Link an existing page. |
| `POST api/page-builder-multilanguage-pages/unlink/{groupId}` | `builder:update` | Unlink a version. |
| `POST api/page-builder-multilanguage-pages/convert/{groupId}` | `builder:create` | Convert a Landing Page. |
| `POST api/page-builder-multilanguage-pages/{id}/clone` | `builder:create` | Clone with every language. |
| `POST api/page-builder-multilanguage-pages/{id}/reset-overrides` | `builder:update` | Apply the given settings to every language (body: field names). |
| `POST api/page-builder-multilanguage-pages/mark-up-to-date/{groupId}` | `builder:update` | Mark a translation up to date. |
| `POST api/page-builder-multilanguage-pages/{id}/publish-all` | `builder:publish` | Publish every draft. |
| `POST api/page-builder-multilanguage-pages/{id}/archive-all` | `builder:delete` | Archive every version. |
| `POST api/page-builder-pages/list` | `builder:read` | Pages list with Multilanguage Pages as single rows; `pageType`, `languageCode`, `translationStatus` filters. |
| `POST api/page-builder-pages/permalink-usage` | `builder:read` | Active pages already serving a permalink in given languages. |

Storefront API (xAPI): `pageDocument` exposes `cultureName` and `alternates { id cultureName permalink }`; `slugInfo` returns the translation in the requested language for a permalink of another language.

## Requirements

Multilanguage Pages need the versions of these components that include it, deployed together:

* Page Builder module
* Pages module (`VirtoCommerce.Pages`) – translation group in the page index and the SEO resolver
* XCMS module (`VirtoCommerce.XCMS`) – `alternates` in xAPI
* Virto Frontend – language switch and `hreflang`

<br>
<br>
********

<div style="display: flex; justify-content: space-between;">
    <a href="../create-landing-page">← Getting started </a>
    <a href="../ai-assistant">AI Assistant →</a>
</div>
