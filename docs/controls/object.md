# object control descriptor

| Property | Type | Description |
| --- | --- | --- |
| `title` | `string` | Название объекта |
| `displayField` | `string` | Имя свойства, которое показывается как название объекта |
| `element` | `ControlDescriptor[]` | Описание свойств объекта |
| `elementDescriptor` | `string` | Название свойства для описания редактора из секции `shared` |

Несколько свойств можно объединить в один объект, который обрабатывается как единое целое.

Если такой объект нужен в нескольких блоках, то описание этого объекта можно вынести в раздел `shared.objects`.

## Примеры

### Inline settings

```json
...
    "settings": [
        {
            "id": "button",
            "label": "Describe a button",
            "type": "object",
            "title": "The button",
            "displayField": "caption",
            "element": [
                {
                    "id": "caption",
                    "type": "string",
                    "label": "Caption"
                },
                {
                    "id": "action",
                    "type": "select",
                    "label": "onClick action",
                    "default": "popup",
                    "options": [
                        { "label": "Show popup", "value": "popup" },
                        { "label": "Go to link", "value": "url" }
                    ]
                },
                {
                    "id": "url",
                    "type": "string",
                    "label": "Enter link",
                    "visibility": "!!this.item && this.item.action === 'url'"
                }
            ]
        },
        ...
    ]
...
```

Результат в свёрнутом состоянии

![Collapsed object control example](images/object-control-closed.png "Collapsed object control example")

Результат в развёрнутом состоянии

![Expanded object control example](images/object-control-opened.png "Expanded object control example")

Результат с заполненными данными

![Filled object control example](images/object-control-filled.png "Filled object control example")

Свойство блока будет выглядеть следующим образом

```json
{
    "button": {
        "caption": "Buy now!",
        "action": "url",
        "url": "https://virtocommerce.com"
    }
}
```

### Shared settings

```json
{
    "shared": {
        "objects": {
            "buttonEditor": [
                {
                    "id": "caption",
                    "type": "string",
                    "label": "Caption"
                },
                {
                    "id": "action",
                    "type": "select",
                    "label": "onClick action",
                    "default": "popup",
                    "options": [
                        { "label": "Show popup", "value": "popup" },
                        { "label": "Go to link", "value": "url" }
                    ]
                },
                {
                    "id": "url",
                    "type": "string",
                    "label": "Enter link",
                    "visibility": "!!this.item && this.item.action === 'url'"
                }
            ]
        }
    },
    "promo": {
        ...
        "settings": [
            {
                "id": "button",
                "label": "Describe a button",
                "type": "object",
                "title": "The button",
                "displayField": "caption",
                "elementDescriptor": "buttonEditor"
            },
            ...
        ]
    },
    "hero": {
        ...
        "settings": [
            {
                "id": "button",
                "label": "Describe hero button",
                "type": "object",
                "elementDescriptor": "buttonEditor"
            },
            ...
        ]
    }
}
```

В результате для разных блоков у нас используется один и тот же редактор, описанный в секции `shared`.
