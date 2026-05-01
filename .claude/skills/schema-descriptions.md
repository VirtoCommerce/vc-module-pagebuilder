# Schema Descriptions

## Description
Author the LLM-facing `description` field on PageBuilder schemas (sections, blocks, templates) so the AI agent (Virto OZ) can pick them by intent. Convention is established by the module's API contract — this skill applies it consistently.

## Triggers
- Adding or editing the `description` field in a schema JSON file under `config/schemas/{sections,blocks,templates}/`
- User asks to "write a description for [section/block/template] X"
- Auditing a theme for schemas missing descriptions
- Updating descriptions to match the current convention

For the mechanical structure of schemas (control types, property descriptors, visibility expressions, ServerRequestDescriptor), see `schema-authoring.md`. This skill covers ONLY the `description` field.

---

## Why this exists

The AI agent calls `pagebuilder_list_section_schemas` to discover what's in the theme, then picks entries by `description` (primary signal) and `name` (fallback). Without a good description the agent has only the UI label — "Title", "Image", "Text" — without context. With a description it knows what role each schema plays and which sibling to pick instead when this one doesn't fit.

The canonical format is documented in the module's tool YAMLs:
- `src/VirtoCommerce.PageBuilderModule.Web/ai/tools/list-section-schemas.yaml`
- `src/VirtoCommerce.PageBuilderModule.Web/ai/tools/get-section-schema.yaml`

If this skill drifts from those YAMLs, the YAMLs win — they describe the live API contract.

---

## Where `description` goes

**Yes — schema root:**
- `sections/*.json` (both regular content sections and `static` settings panels)
- `blocks/*.json`
- `templates/*.json`

**No — never:**
- Inside `settings[]` per-field. Shared field schemas (`shared/title.json`, `shared/_sections.json`) are reused across many sections with different contextual meaning; one field-level description can't fit them all. Per-field guidance goes inline in the **schema-root** description of the section that uses the field.
- On `objects/` or `shared/` schemas. The agent doesn't pick these by intent — it follows schema references. The backend strips descriptions from these in the catalog response anyway.

---

## Format

One `description` string. English, sentence case, no localization. Three parts in this order:

1. **General purpose** — one phrase saying what this schema is for, including its visual / structural shape ("Hero-style heading on a tinted background", "Default content page", etc.).
2. **Use when: ... / Skip when: ...** — concrete cues to disambiguate this entry from sibling entries. In `Skip when` always name the alternative the agent should pick instead.
3. **Inline field guidance** — for each meaningful field, what to fill or which default to keep. Skip trivial fields whose meaning is obvious from `id` plus `default`.

Length: 80–250 words is typical. Longer is fine when the schema has many meaningful fields. Shorter is fine for narrow-scope schemas with no ambiguity.

---

## Procedure

When asked to write a description for `<kind>/<key>`:

1. **Read the schema file.** Note `static`, `includeShared`, `settings[]` (each field's `id`, `type`, `default`, `options`).
2. **Read referenced shared schemas.** For each name in `includeShared`, read `shared/<name>.json`. The merged field list is what the section actually exposes.
3. **For sections — read the linked Vue component.** Look in `<theme>/client-app/shared/static-content/components/` for `<key>-block.vue` or `<key>.vue`. The component template shows the visual shape and how each setting renders; props clarify ambiguous field semantics.
4. **For templates — note the merge.** The agent sees `settings[]` after the backend merges applicable static sections (filtered by `template.sections` if present). Cover the merged fields in inline guidance — to the agent they appear as native template fields.
5. **Identify siblings.** List the 1–2 most-likely-confusable entries in the same kind folder. Prepare `Use when` / `Skip when` cues that name them by key.
6. **Draft the description** in the three-part format. Use file `id`s and field names verbatim — they're what the agent will see and match against.
7. **Apply the edit** — insert `"description": "..."` immediately after `"name": "..."` in the JSON. Don't reformat the rest of the file.

---

## Examples

### Section — `sections/title.json`

> Hero-style heading on a tinted background: a heading element (h2–h6, default h2) with an optional short subtitle line under it. Use when: opening a content area that needs a clear topic announcement with a brief lead-in — blog post intros, mid-page section breaks. Skip when: you need body copy (use a text section), a heading paired with an action button (use call-to-action), or the page-level H1/SEO header (use the pinned page-header section). Subtitle is a 1–2 sentence lead-in; for longer body copy use a text section.

What it does well: phrase-1 names the visual shape; Use/Skip cues name three sibling sections to disambiguate; subtitle gets one line of guidance because it's the only ambiguous field (title and heading are obvious; background is a universal field with `default`s).

### Template — `templates/page.json`

> Default content page — flexible composition for marketing, landing, FAQ, About-us, and other static informational pages. This is the default template when the user does not explicitly indicate a more specialized page type. Use when: the user asks for a generic page, landing page, info page, or anything that isn't clearly a blog post or product page. Skip when: the user names a specialized type — use `blogs` for blog posts / articles / news, `product` for product detail pages. Page settings: `header` is the visible H1 (typically same or close to the page name); `hideBreadcrumbs` defaults to false, set true only for top-level standalones like a homepage; `seoInfo` carries SEO meta (`pageTitle`, `metaKeywords`, `metaDescription`) — leave it empty unless the user supplied SEO copy in the prompt.

What it does well: names this template as the default explicitly (the agent's fallback rule); Use/Skip name the two siblings; "Page settings:" prefix introduces inline field guidance for all three merged fields, including the `seoInfo` object that comes from the static `page-header` section after merge.

---

## Anti-patterns

- **Field-level `description`** — don't add. Put per-field hints inside the schema-root description.
- **Localization** — don't translate. One language per theme.
- **Designer-speak** — don't reference editor UI ("appears in the sidebar", "open the panel"). Humans don't read this. Speak directly to the agent about generation choices.
- **Vague Use cues** — "Use for important things" is useless. Be concrete: "Use when: blog post intros, mid-page section breaks".
- **Marketing padding** — don't write "a beautiful, flexible heading". Cut every word that doesn't help the agent decide.
- **Verbosity for trivial fields** — don't write "title is the title". Describe only fields where the agent might be unsure: which `select.value` to pick, what default to keep, when to leave empty.
- **Circular reference** — don't say "use this template when the user wants a page". Say what kind of content goes here.
- **Don't break the file** — `description` is a single string. No nested arrays, no rich-text formatting, no markdown lists. Inline field guidance is prose.

---

## Cross-references

- Mechanical schema authoring (descriptors, control types, ServerRequestDescriptor): `schema-authoring.md`.
- Canonical convention (live API contract):
  - `src/VirtoCommerce.PageBuilderModule.Web/ai/tools/list-section-schemas.yaml`
  - `src/VirtoCommerce.PageBuilderModule.Web/ai/tools/get-section-schema.yaml`
- Reference theme with descriptions filled in:
  - `vc-frontend/client-app/plugins/builder-preview/schemas/sections/title.json`
  - `vc-frontend/client-app/plugins/builder-preview/schemas/templates/page.json`
