# files control descriptor

[file-upload](https://pivan.github.io/file-upload/) component is used for this control.
The [npm-package](https://www.npmjs.com/package/@iplab/ngx-file-upload) is available too.

| Property | Type | Description |
| - | - | - |
| `accept` | `string` | Acceptable files extensions or mime-types, used directly in attribute [accept](https://developer.mozilla.org/en-US/docs/Web/HTML/Element/input/file#accept). |
| `multiple` | `boolean` | Allow multiple files uploading. Default `true`. |
| `sortable` | `boolean` | Allow sort files. Default `true`. |
| `maxFileSize` | `number` | Maximum file size. |
| `collapseThreshold` | `number` | Count of files after that panel with preview will be collapsed. Default is 6. |
| `collapseCount` | `number` | Number of files that will be shown in collapsed state. Default is 4. |
| `skipRemoveConfirmation` | `boolean` | Ask user to confirm file removing. |
| `removeMessage` | `string` | Message for file removing confirmation. |
| `urlField` | `string` | Name of field with `url` if item is object. |
| `filenameField` | `string` | Name of field with `filename` if item is object. |
| `element` | `ControlDescriptor[]` | Descriptors for other field of object. |
| `UploadAcceptRequest` | `AssetsRequest` | Custom request for upload asset. |

Value of this control is array of urls or objects if `element` property is defined.

## Example

```json
...
    "settings": [
        {
            "id": "attachment",
            "label": "Attach a file",
            "type": "files",
            "multiple": false,
            "acceptTypes": ".pdf,application/pdf"
        },
        ...
    ]
...
```
<!--
Result (todo: renew images)

![File control example](images/file-control.png "File control example")
-->