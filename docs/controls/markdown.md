# Markdown Control Descriptor

Markdown editor. The value can be saved either as Markdown or HTML or both.

| Property | Type | Description |
| --- | --- | --- |
| `resultType` | `'markdown' \| 'html' \| 'mixed'` | The result type |
| `styles` | `string[] \| string` | Links to css-files that can be used for preview pane. Be carefull with `CORS`. |
| `uploadAssetsRequest` | `AssetsRequest` | Descriptor that used for upload images when paste them from the clipboard. |
| `urlField` | `string` | Used for link to image. |
| `filenameField` | `string` | Used for alt for pasted image. |

Result for `markdown` and `html` is a string. For `mixed` it is an object with `markdown` and `html` properties.

When `resultType` is `html` the value transforms to `md` using [turndown](https://github.com/mixmark-io/turndown) library.

## Example
Schema
```json
...
    "settings": [
        {
            "id": "content",
            "label": "Content",
            "type": "markdown",
            "resultType": "mixed"
        },
        ...
    ]
```

Results
```json
{
    "content": [
        {
            "type": "markdown-example",
            "content": {
                "markdown": "## Markdown example",
                "html": "<h2>Markdown example</h2>"
            }
        }
    ] 
}
```