# Template Syntax

The Page Builder designer uses a template engine (`utils.template()`) to evaluate dynamic values in settings, URLs, request bodies, and response selectors. Templates are strings containing tokens that are resolved against an execution context at runtime.

Source: `src/app/modules/integration/helpers/utils.ts`

## Syntax forms

### 1. Path substitution: `{{path}}`

Resolves a dot-separated path from the context object. If the value is `null` or `undefined`, it is replaced with an empty string.

```
"url": "/api/stores/{{location.params.storeId}}"
```

Context paths available:

| Path | Description |
|------|-------------|
| `location.params.*` | URL query and hash parameters |
| `location.origin` | Current origin (`https://host:port`) |
| `location.path` | Current pathname |
| `location.host` | Hostname with port |
| `location.protocol` | Protocol (`https:`) |
| `location.hash` | Hash fragment |
| `location.url` | Full URL |
| `config.*` | Raw (unevaluated) config values from `settings.json` |
| `settings.*` | Evaluated settings values (resolved from requests, selectors, etc.) |

### 2. Inline expression: `{{=expr}}`

Evaluates a JavaScript expression. The result is **coerced to a string** via `String.prototype.replace()`. Use this when the expression result is embedded inside a larger string.

```
"fullPreviewUrl": "{{=combine(this.settings.storefrontUrl, this.settings.previewPath)}}?ep={{location.origin}}"
```

Inside expressions:

- `this` refers to the execution context (contains `location`, `config`, `settings`, and any additional context properties)
- Utility functions are available (see below)

### 3. Raw expression: `@{{expr}}`

Evaluates a JavaScript expression and returns the result **as-is**, preserving its original type (array, object, number, boolean). The `@{{...}}` pattern must be the **entire string value** — it cannot be mixed with other text.

Returns `null` if the expression evaluates to `null` or `undefined`.

```json
{
    "body": {
        "objectIds": "@{{this.settings.previewImpersonation || []}}",
        "take": 100
    }
}
```

This sends `{"objectIds": ["id1", "id2"], "take": 100}` — the array is preserved, not stringified.

**When to use `@{{}}` vs `{{=}}`:**

| Scenario | Syntax | Result type |
|----------|--------|-------------|
| Value inside a URL or text | `{{=expr}}` | Always string |
| POST body field that must be an array | `@{{expr}}` | Array |
| POST body field that must be an object | `@{{expr}}` | Object |
| POST body field that must be a number | `@{{expr}}` | Number |

## Utility functions

The following functions are available inside `{{=expr}}` and `@{{expr}}` expressions:

| Function | Description |
|----------|-------------|
| `combine(a, b, ...)` | Joins path parts with `/`, avoids double slashes |
| `getValueOrDefault(val, def)` | Returns `val` unless `undefined`, then returns `def` |
| `getValueByPath(obj, path)` | Resolves a dot-path on an object |
| `stripHtmlTags(str)` | Removes HTML tags from a string |
| `toList(obj, keyProp)` | Converts object to array, adding each key as `keyProp` |
| `cutString(str, limit?)` | Truncates string at `limit` (default 50) with `...` |
| `tryParseJson(str)` | Parses JSON string, returns `null` on failure |
| `getItemValue(item, descriptors)` | Extracts values from item by descriptor array |

## Response processing

When a request includes a `response` descriptor, the response is processed in order:

1. **`selector`** (optional) — evaluates a JS expression with `this.response` set to the raw HTTP response. Returns the computed value.
2. **`result`** (optional) — applies a JSONPath expression (e.g., `$.results`, `$.settings[?(@.name=='X')].value`) to extract data.
3. **`isArray`** (optional) — if `true`, wraps a non-array result in an array; if `false`, unwraps an array to its first element.

```json
{
    "response": {
        "selector": "this.response.permissions.indexOf('builder:theme') === -1",
        "isArray": false
    }
}
```

## Triple braces escape

`{{{name}}}` is treated as a literal `{` + `{{name}}` + `}`, effectively escaping the inner braces. This is rarely needed.

## Examples

**Static URL with path substitution:**
```
"/api/pagebuilder/templates?storeId={{location.params.storeId}}&theme={{config.themeName}}"
```

**Expression building a URL:**
```
"{{=combine(this.settings.storefrontUrl, '/designer-preview?pageId=' + this.groupId)}}"
```

**Selector extracting a setting from store API response:**
```json
{
    "selector": "(function() { var s = (this.response.settings || []).find(function(x) { return x.name === 'MySettingName' }); return s && s.value ? s.value.split('\\n').filter(function(x) { return x.trim() }) : [] }).call(this)"
}
```

**Raw expression passing an array in POST body:**
```json
{
    "objectIds": "@{{this.settings.previewImpersonation || []}}"
}
```
