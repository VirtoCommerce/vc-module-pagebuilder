# color control descriptor

Для редактора используется библиотека [ngx-color-picker](https://www.npmjs.com/package/ngx-color-picker).

| Property        | Type      | Description |
| --------------- | --------- | ----------- |
| `outputFormat`  | `string`  | формат результата. `auto`, `hex`, `rgba`, `hsla` |
| `emptyValue` | `string` | значение при нажатии на кнопку `Clear` |

## Пример:

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

Результат

![Color control example](images/color-control.png "Color control example")

![Color control example in open state](images/color-control-opened.png "Color control example in open state")
