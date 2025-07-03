# Asset File

описывает загруженный или прикрепленный в редакторе файл. фактически наследуется (inherits) от стандартного класса [File](https://developer.mozilla.org/en-US/docs/Web/API/File)

используется в контролах `ImagesComponent`, `FilesComponent` и `MarkdownComponent`.

функция этого класса - быть моделью в запросах и ответах при сохранении файла.

выбранные для загрузки файлы преобразуются к этой модели, а затем по очереди загружаются.

export interface AssetFile extends File {
    data?: any; // объект, описанные дополнительными свойства ми(из `descriptor.element`)
    url?: string; // полный урл где файл хранится (todo: надо описать как на него можно повлиять)
    previewUrl: string | null; // урл для превью (todo: на него можно повлиять)
    assetName: string; // имя файла
}

этот объект добавляется в контекст генерации запроса для сохранения файла на сервере

примеры

### default config for upload file (in settings.json)

```json
	...
    "uploadAssetsRequest": {
        "url": "/api/content/pages/{{location.params.storeId}}?folderUrl=/assets/pages&name={{**file.assetName**}}",
        "method": "POST",
        "form": {
            "name": "uploadedFile",
            "fileName": "{{**file.assetName**}}"
        },
        "response": {
            "result": "$[0].url",
            "isArray": false
        }
    },
	...
```

ожидается что ответ от такой загрузки будет содержать что-то такое

```json
[
	{
		"url": "https://url.to.image",
		...
	}
]
```

и этот ответ будет преобразован в 

`"https://url.to.image"`

