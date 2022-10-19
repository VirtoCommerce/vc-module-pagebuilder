# header control descriptor

| Property  | Description                                                                |
| --------- | -------------------------------------------------------------------------- |
| `content` | строка статического текста, используется для вывода информации в редакторе |

Этот контрол нужен для информации, он не добавляет свойств конечному блоку

## Пример:

```json
...
    "settings": [
        {
            "type": "header",
            "content": "Please upload image of reasonable size"
        },
        ...
    ]
...
```

Эта настройка отобразит текст в редакторе

![Header control example](images/header-control.png "Header control example")
