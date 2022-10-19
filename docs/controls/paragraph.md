# paragraph control descriptor

| Property  | Description                                                                |
| --------- | -------------------------------------------------------------------------- |
| `content` | строка статического текста, используется для вывода информации в редакторе |

Этот контрол нужен для информации, он не добавляет свойств конечному блоку

## Пример:

```json
...
    "settings": [
        {
            "type": "paragraph",
            "content": "Please upload image of reasonable size"
        },
        ...
    ]
...
```

Эта настройка отобразит текст в редакторе

![Paragraph control example](images/paragraph-control.png "Paragraph control example")
