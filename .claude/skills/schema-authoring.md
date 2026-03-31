# Schema Authoring

## Description
JSON schema conventions for creating and editing sections, blocks, templates, shared settings, and objects in the Page Builder. Use this skill when adding or modifying any file under `config/schemas/` in the theme repository.

## Triggers
- Creating a new section or block descriptor
- Adding settings to an existing section or block
- Defining a new template in `schemas/templates/`
- Working with shared settings (`_sections.json`, `_blocks.json`)
- Defining reusable object types in `schemas/objects/`
- Writing visibility expressions or ServerRequestDescriptor fields

---

## Folder Structure

```
config/schemas/
  templates/         # Template descriptors (one file = one template/page type)
  sections/          # Section descriptors (one file = one section type)
  blocks/            # Block descriptors (one file = one block type, same format as sections)
  shared/
    _sections.json   # Global settings injected into every section
    _blocks.json     # Global settings injected into every block
    *.json           # Named shared groups, included via includeShared
  objects/           # Reusable object type definitions
```

---

## SectionPropertyDescriptor — Field Reference

Every entry in `settings[]` is a `SectionPropertyDescriptor`:

| Property        | Type                   | Description |
|-----------------|------------------------|-------------|
| `id`            | string                 | Property key in the data model. Required. |
| `type`          | string                 | Control type (see below). Required. |
| `label`         | string                 | Label shown in the editor sidebar. |
| `default`       | any                    | Default value when a new section is created. |
| `visibility`    | string (JS expression) | Show/hide this field. Evaluated in component context (`this.item`, `this.model`, etc.). |
| `tab`           | string                 | Groups fields under a named tab in the sidebar. |
| `group`         | string                 | Groups fields under a collapsible group within a tab. |
| `required`      | boolean                | Marks field as required in the form. |
| `multiple`      | boolean                | For `images`/`files` — allow multiple uploads. |
| `displayField`  | string                 | For `list` and `select` — which property to show as label. |
| `equalKey`      | string                 | For `select` — property used for value equality comparison. |
| `options`       | `{ label, value }[]`   | Static options for `select`. |
| `request`       | ServerRequestDescriptor | Dynamic data source for `select`, `list`, `search`. |
| `resultType`    | string                 | Output format for `markdown` (e.g. `"markdown"` or `"html"`). |
| `elementDescriptor` | string            | For `object` — name of the file in `objects/` folder. |
| `element`       | SectionPropertyDescriptor[] | For `list` — descriptors for list item fields. |

---

## Control Types

| Type       | Description |
|------------|-------------|
| `string`   | Single-line text input |
| `text`     | Multi-line textarea |
| `richtext` | CKEditor rich text editor |
| `markdown` | Markdown editor (EasyMDE) |
| `number`   | Numeric input |
| `checkbox` | Boolean toggle |
| `select`   | Dropdown — static `options` or dynamic `request` |
| `color`    | Color picker |
| `calendar` | Date/time picker |
| `images`   | Image upload (single or multiple) |
| `files`    | File upload (single or multiple) |
| `object`   | Nested object — requires `elementDescriptor` |
| `list`     | Repeatable list of items — requires `element` descriptors |
| `header`   | Non-editable section header (label only, no data) |
| `paragraph`| Non-editable text block |

---

## Shared Settings

### Global shared (`_sections.json` / `_blocks.json`)
Settings defined here are merged into every section or block automatically.

### Named shared groups
Create a file like `shared/typography.json` with a `settings` array. Include it in a section with:
```json
"includeShared": ["typography"]
```

### Excluding shared settings
```json
"excludeShared": true               // exclude all shared settings
"excludeShared": ["fontColor"]      // exclude specific ids from shared
```

---

## Visibility Expressions

`visibility` is a JavaScript expression evaluated in component context. Returns `true` to show the field.

Available context variables:
- `this.item` — the current item's data (in a list element)
- `this.model` — the current section's data model
- `this.block` — current block model (if inside a block)

```json
{ "visibility": "!!this.item && this.item.action === 'url'" }
{ "visibility": "this.model.showAdvanced === true" }
```

---

## ServerRequestDescriptor

Used in `request` fields of `select`, `list`, `search` controls to load data dynamically.

```json
{
  "url": "/graphql",
  "method": "post",
  "body": {
    "query": "{products(storeId:\"{{location.params.storeId}}\"){items{id,name}}}"
  },
  "cacheable": true,
  "response": {
    "result": "data.products.items"
  },
  "label": "name"
}
```

Template tokens available in `url` and `body`:
- `{{location.params.storeId}}` — current store ID from URL
- `{{location.params.theme}}` — current theme
- `{{settings.*}}` — resolved app settings
- `{{this.model.*}}` — current section data

---

## Section Descriptor — Top-Level Properties

| Property        | Type                          | Description |
|-----------------|-------------------------------|-------------|
| `name`          | string                        | Display name in the add-section panel |
| `icon`          | string                        | Material icon name |
| `displayField`  | string                        | Property used as the section label in the list |
| `static`        | boolean                       | If true, section cannot be added/removed |
| `sort`          | number                        | Order in the add-section panel |
| `group`         | string                        | Group name in the add-section panel |
| `groupIcon`     | string                        | Group icon |
| `groupSort`     | number                        | Group sort order |
| `settings`      | SectionPropertyDescriptor[]   | List of editable fields |
| `default`       | object                        | Default data model for new instances |
| `includeShared` | string[]                      | Named shared groups to include |
| `excludeShared` | string[] \| true              | Shared settings to exclude |

---

## Template Descriptor — Top-Level Properties

| Property         | Type                                        | Description |
|------------------|---------------------------------------------|-------------|
| `name`           | string                                      | Display name in template selector |
| `alias`          | string                                      | Unique route key |
| `previewUrl`     | string                                      | URL loaded in the preview iframe |
| `path`           | string                                      | Path to content file in storage |
| `type`           | string                                      | Content type (`pages`, `theme`, `blogs`, etc.) |
| `sections`       | string[]                                    | Allowed section types. Empty = all allowed. |
| `settings`       | SectionPropertyDescriptor[]                 | Template-level settings (not per-section) |
| `request`        | ServerRequestDescriptor \| ServerRequestDescriptor[] | Load child entries dynamically |
| `children`       | TemplateEntryList                           | Static child entries |
| `sort`           | number                                      | Order in template selector |
| `isDefault`      | boolean                                     | Selected by default |
| `previewMessage` | any                                         | Extra data sent to the preview iframe |

---

## Examples

=== "Minimal section"
```json
{
  "name": "Text",
  "icon": "text_snippet",
  "displayField": "title",
  "settings": [
    { "id": "title", "label": "Title", "type": "string" },
    { "id": "body",  "label": "Content", "type": "markdown", "resultType": "markdown" }
  ]
}
```

=== "Section with tabs and visibility"
```json
{
  "name": "Hero",
  "icon": "image",
  "settings": [
    { "id": "title",      "label": "Title",       "type": "string",   "tab": "Content" },
    { "id": "image",      "label": "Image",        "type": "images",   "tab": "Media",  "multiple": false },
    { "id": "showButton", "label": "Show button",  "type": "checkbox", "tab": "Content", "default": false },
    {
      "id": "buttonLabel", "label": "Button label", "type": "string", "tab": "Content",
      "visibility": "this.model.showButton === true"
    }
  ]
}
```

=== "Section with dynamic select"
```json
{
  "name": "Products",
  "icon": "inventory_2",
  "settings": [
    {
      "id": "category",
      "label": "Category",
      "type": "select",
      "equalKey": "id",
      "displayField": "name",
      "request": {
        "url": "/api/catalog/categories?storeId={{location.params.storeId}}",
        "method": "get",
        "cacheable": true,
        "response": { "result": "$.items" }
      }
    }
  ]
}
```

=== "Reusable object"
```json
// objects/button.json
{
  "settings": [
    { "id": "label",  "type": "string", "label": "Label" },
    { "id": "url",    "type": "string", "label": "URL" },
    { "id": "style",  "type": "select", "label": "Style",
      "options": [{ "label": "Primary", "value": "primary" }, { "label": "Outline", "value": "outline" }] }
  ]
}

// In a section:
{ "id": "button", "type": "object", "label": "Button", "elementDescriptor": "button" }
```
