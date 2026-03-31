# String Control Descriptor

This control is used to input short or long text values. You can configure it to allow single-line or multi-line input.

| Property  | Type    | Default | Description                             |
| --------- | ------- | ------- | --------------------------------------- |
| `multiline` | boolean | `false` | Allows entering text in multiple lines. |
| `minRowsCount` | number | `1` | Minimum number of visible rows for the multiline textarea. Defines the initial height. Only applies when `multiline` is `true`. |
| `maxRowsCount` | number | `4` | Maximum number of rows the textarea will expand to before showing a scrollbar. Only applies when `multiline` is `true`. |

## Example


<div class="grid" markdown>

```json
...
    "settings": [
        {
            "id": "title",
            "type": "string",
            "label": "Title",
            "placeholder": "Please enter title"
        },
        {
            "id": "message",
            "type": "string",
            "label": "Message",
            "placeholder": "Please enter message",
            "multiline": true
        },
        {
            "id": "description",
            "type": "string",
            "label": "Description",
            "multiline": true,
            "minRowsCount": 2,
            "maxRowsCount": 8
        },
        ...
    ]
...
```


![String control](media/string-control.png){: style="display: block; margin: 0 auto;" }


</div>

<br>
<br>
********

<div style="display: flex; justify-content: space-between;">
    <a href="../select">← Select </a>
    <a href="../text">Text →</a>
</div>