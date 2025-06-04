# Color Control Descriptor

The [color picker](https://ngx-color.vercel.app/) is used for this editor. The [npm-package](https://www.npmjs.com/package/ngx-color) is available too.

| Property | Type | Description |
| - | - | - |
| `colorMode` | `color` \| `presets` | Sketch picker is used for `color` mode. Twitter picker is used for `presets`. |
| `disableAlpha` | `boolean` | Remove alpha slider and options from picker. |
| `clearValue` | `string` | Value for clear button. |
| `inline` | `boolean` | Display picker inline. |
| `presets` | `string[]` | List of values for presets mode. |

## Example:

```json
...
    "settings": [
        {
            "id": "headerColor",
            "label": "Header color",
            "type": "color",
            "outputFormat": "hsla"
        },
        ...
    ]
...
```

![Color control example](images/color-picker.png "Color control example")

![Color control example in open state](images/color-picker-popup.png "Color control example in open state")

```json
...
    "settings": [
        {
            "id": "headerColor",
            "label": "Header color",
            "type": "color",
            "inline": "true"
        },
        ...
    ]
...
```

![Inline color control example](images/color-picker-inline.png "Inline color control example")
