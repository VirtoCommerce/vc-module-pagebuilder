# collection control descriptor

|Property|Type|Description|
|-|-|-|
| `addText` | `string` | текст на кнопке добавления элемента в список, по умолчанию `Add item` |
| `displayField` | `string` | Свойство, которое будет отображаться как название элемента |
| `element` | `ControlDescriptor[]` | описание контролов для элемента списка |


Название элемента усекается до 14ти символов и добавляется многоточие. Если же название оказалось пустым, то вместо него пишется порядковый номер элемента в массиве и добавляется `<no title>`.

При добавлении элемента в список, его свойства заполняются из свойства `default` дескрипторов `element`.

## Пример

```json
...
    "settings": [
        {
            "id": "formFields",
            "label": "Fields list",
            "type": "list",
            "addText": "Add a field",
            "displayField": "labelText",
            "element": [
                {
                    "id": "fieldType",
                    "label": "Type",
                    "type": "select",
                    "default": "text",
                    "options": [
                        { "value": "checkbox", "label": "Checkbox" },
                        { "value": "text", "label": "Text" }
                    ]
                },
                {
                    "id": "fieldName",
                    "label": "Name",
                    "type": "string"
                },
                {
                    "id": "labelText",
                    "label": "Label",
                    "type": "string"
                }
            ],
            "default": [
                { "fieldType": "text", "fieldName": "fullname", "labelText": "Full name" },
                { "fieldType": "text", "fieldName": "email", "labelText": "Email" },
                { "fieldType": "checkbox", "fieldName": "accept", "labelText": "By clicking \"Submit\" I understand that I consent to opt-in to the Terms and Policy" }
            ]
        },
        ...
    ]
...
```

Результат

![List control example](images/list-control.png "List control example")

В режиме редактирования элемента

![List control edit mode example](images/list-control-edit.png "List control edit mode example")

