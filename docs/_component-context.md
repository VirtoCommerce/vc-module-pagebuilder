# Component Context

The **Component context** provides all the necessary data and tools that a Page Builder component might need while rendering or interacting with the UI. It allows you to access the current section, block, schema, and helper functions for advanced use cases.

Use this context in custom controls, dynamic sections, or when building interactive components inside the Page Builder.


## Available properties

| Property        | Type                                    | Description                                                             |
| --------------- | --------------------------------------- | ----------------------------------------------------------------------- |
| `model`         | any                                     | The item currently being edited — can be a block, section, or settings. |
| `block`         | SectionModel <br> null                | The current block (if available).                                       |
| `section`       | SectionModel <br> null                | The current section. Useful when working with child blocks.             |
| `template`      | TemplateModel <br> null               | The template currently in use.                                          |
| `page`          | SectionModel[]                        | The full page structure — an array of sections.                         |
| `settings`      | SectionModel                          | Global settings for the current template.                               |
| `schema`        | SectionPropertyDescriptor[]           | Schema of the currently selected block or section.                      |
| `blockSchema`   | SectionPropertyDescriptor[] <br> null | Schema of the current block.                                            |
| `sectionSchema` | SectionPropertyDescriptor[]           | Schema of the current section (or the parent section if in a block).    |
| `objects`       | ObjectsSchemasList                    | A list of object schemas available for the component.                   |
| `index`         | number                                  | Index of the current item (for list-like sections).                     |
| `item`          | any                                     | Current item’s value.                                                   |
| `parent`        | ControlContext                        | The parent context (used in nested lists or structures).                |
| `file`          | AssetFile                             | The file or image associated with the current control.                  |
| `element`       | any                                     | The current item in a list-like control.                                |
| `utils`         | object (helper functions)               | A set of utility functions (see below).                                 |
| `__searchQuery` | string                                  | The current search query (used in [search](search.md) and [select](select.md) controls).      |


## Utility functions (`utils`)

The `utils` object provides a collection of helper methods for working with data, text, paths, and more. It is for custom controls written in code: an [expression](./template-syntax.md#expression-language) in a schema calls the helpers it supports by name, as `combine(…)`, never through `this.utils`.

| Function                                                       | Description                                                        |
| -------------------------------------------------------------- | ------------------------------------------------------------------ |
| `spreadPropertyByOther(obj, keyProperty, ...spreadProperties)` | Spreads properties from one object into another based on a key.    |
| `generateAnchor(value)`                                        | Generates a URL-safe anchor from a string.                         |
| `generateUniqueString(length)`                                 | Generates a random unique string of specified length.              |
| `onlyLettersAndDigits(value)`                                  | Strips all characters except letters and digits.                   |
| `template(value, ...args)`                                     | Replaces placeholders in a string with provided arguments.         |
| `evalInContext(expr, context)`                                 | Evaluates an expression within a given context.                    |
| `getValueOrDefault(value, defaultValue)`                       | Returns a value or a default if undefined.                         |
| `getValueByPath(model, path)`                                  | Retrieves a nested value from an object using a string path.       |
| `stripHtmlTags(str)`                                           | Removes HTML tags from a string.                                   |
| `combine(...parts)`                                            | Combines string parts into a single string.                        |
| `toList(obj, keyPropertyName)`                                 | Converts an object to a list based on a property key.              |
| `tryParseJson(value)`                                          | Tries to parse a string as JSON.                                   |
| `getItemValue(item, descriptor)`                               | Extracts a value from an item using a descriptor or path.          |
| `arrayCastByConfig(item, isArray)`                             | Ensures a value is treated as an array if configured.              |
| `cutString(value, length)`                                     | Truncates a string to a specified length (default: 50 characters). |
| `filterBy(list, field, value)`                                 | The items of a list whose own `field` equals `value`.              |
| `toOptions(list, labelFields, valueField)`                     | Select options `{ label, value }` built from a list.               |

<br>
<br>
********

<div style="display: flex; justify-content: space-between;">
    <a href="../schemas">← Schemas </a>
    <a href="../calendar">Controls →</a>
</div>