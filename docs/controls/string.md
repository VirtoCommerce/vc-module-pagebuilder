# string control descriptor

| Property      | Type      | Description                                                                |
| ------------- | --------- | -------------------------------------------------------------------------- |
| `placeholder` | `string`  | placeholder для контрола |
| `multiline`   | `boolean` | позволяет ввод текста в несколько строк |

Для однострочной строки используется `<input type="text" />`, для многострочной &ndash; `textarea`.

## Пример:

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
        ...
    ]
...
```

Результат

![String control example](images/string-control.png "String control example")
