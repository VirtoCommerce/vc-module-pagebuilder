# Images Control Descriptor

This descriptor contains the same options as [files control](files.md). The one exception is that default value for `accept` property is `image/*`.

## Example

```json
...
    "settings": [
        {
            "id": "image",
            "label": "Image",
            "type": "images",
            "default": {
                "url": "/themes/assets/blocks/portfolio.png"
            }
        },
        ...
    ]
...
```
Result

![Image control example](images/image-control.png "Image control example")
