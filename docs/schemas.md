# Schemas

## Overview
TODO:

## Theme Structure
Page Builder reads meta-data from theme. Developers can extend or customize page builder behaviour by theme repository.

```text
├── client-app                    // The main folder for the application.
|   ├── shared                    // Assets needed to be precompiled during building.
|   |   └── static-content
|   |      └── components         // Vue Components for rendering Page Builder elements. 
├── config                        
|   |   └── schemas               // All Page Builder meta information are stored here.
|   |      ├── blocks             // Block definitions.
|   |      |   └──...
|   |      ├── objects            // Object definitions.
|   |      |   └──...
|   |      ├── sections           // Sections definitions.
|   |      |   └──...
|   |      ├── shared             // Global settings folder.
|   |      |   └── _blocks.json   // Global settings for blocks.
|   |      |   └── _sections.json // Global settings for sections.
|   |      └── templates          
|   |          └── page.json      // Page Builder configuration for static pages.
|   └── settings_schema.json      // Theme settings schema file. Page Builder uses it for Theme and Preset editor.
|   └── settings_data.json        // Theme config file.
```

## Type of Json Files


### Templates

Каждый файл &ndash; это дескриптор страницы, либо группы страниц.
По содержимому этих файлов формируется выпадающее меню в билдере.
формат файла

| property | type | required | description |
| - | - | - | - |
| `name` | `string` | `true` | Название страницы |
| `alias` | `string` | `true` | используется в роутинге, своего рода уникальный идентификатор |
| `previewUrl` | `string` | `true` | адрес, на который переходит превью при выборе шаблона |
| `path` | `string` | `false` | Путь к странице относительно корня хранилища |
| `type` | `string` | `false` | ContentType для шаблона (`pages`, `theme`, etc.) |
| `request` | `ServerRequestDescriptor` \| `ServerRequestDescriptor[]` \| `string` \| `string[]` | `false` | Описание запроса для загрузки дочерних элементов |
| `sort` | `number` | `false` | Возможность сортировать шаблоны в выпадающем списке |
| `isDefault` | `boolean` | `false` | Шаблон по умолчанию, в случае когда шаблона в урле либо не существует, либо он не указан |
| `sections` | `string[]` | `false` | Перечень допустимых секций для данного шаблона. Если массив пустой, то допустимы все. |
| `settings` | `SectionPropertyDescriptor[]` | `false` | Настройки шаблона. Отображаются как отдельный блок.  |
| `children` | `TemplateEntryList` | `false` | Дочерние шаблоны. |
| `previewMessage` | `any` | `false` | данные, отправляемые в превью при переходе на шаблон |

Простейший пример
```json
{
  "name": "Homepage",
  "alias": "homepage",
  "previewUrl": "/",
  "type": "pages",
  "sections": [ ],
}
```
Пример с ограничением по секциям и дополнительными настройками
```json
{
  "name": "Зкщвгсеы",
  "previewUrl": "/products/hp-laserjet-pro-mfp-m130fw",
  "type": "theme",
  "sections": [
    "call-to-action",
    "call-to-action-with-image",
    "features",
    "image",
    "products",
    "text",
    "title"
  ],
  "settings": [
    {
      "id": "header",
      "label": "Page Header / H1",
      "type": "string"
    }
  ]
}
```
Пример с запросом дочерних шаблонов
```json
{
  "name": "Pages",
  "previewUrl": "",
  "request": [
    {
      "url": "/api/content/pages",
      "method": "get",
      "cacheable": true
    }
  ],
  "type": "pages"
}
```

### Sections
Каждый файл описывает блок, который может быть добавлен на страницу.
По содержимому файла формируется редактор, который позволяет редактировать блок.

| property | type | description |
| - | - | - |
| `icon` | `string` | Иконка блока |
| `name` | `string` | Имя блока |
| `static` | `boolean` \| `string` | Статические блоки нельзя удалять, являются частью настройки шаблона |
| `displayField` | `string` | Название своства, отображаемое в списке блоков |
| `sort` | `number` | Возможность сортировать блоки придобавлении |
| `settings` | `SectionPropertyDescriptor[]` | Описание свойств блока |
| `default` | `SectionModel` | Дефолтные значения для блока при добавлении |
| `group` | `string` | Название группы блоков на панели добавления блока |
| `groupIcon` | `string` | Иконка группы |
| `groupSort` | `number` | Сортировка |
| `includeShared` | `string[]` | list of names to add settings from Shared |
| `excludeShared` | `string[]` \| `true` | true - not use shared settings, string[] - list of settings id to exclude from result shared list |

<!-- blocks?: string[]; -->
<!-- inline?: boolean; // used for settings groups, when false, group displayed as a overlap panel -->

Примеры

The simplest example
```json
{
  "name": "Text",
  "icon": "text_snippet",
  "displayField": "title",
  "settings": [
    {
      "id": "title",
      "label": "Title",
      "type": "string"
    },
    {
      "id": "text",
      "label": "Content",
      "type": "text"
    }
  ]
}
```

Пример с дополнительными настройками
```json
{
  "name": "Image",  
  "icon": "image",
  "displayField": "name",
  "group": "Media",
  "groupIcon": "media",
  "settings": [
    {
      "id": "image",
      "type": "images",
      "multiple": false
    },
    {
      "id": "alttext",
      "label": "Alternative Text",
      "type": "string"
    }
  ]
}

```


### Blocks
Секции могут содержать в себе дочерние блоки. Формат файла точно такой же как и у секций.

### Shared
В папке Shared находятся общие настройки, которые могут быть использованы в секциях и блоках.

Общие настройки для всех секций описаны в файле `_sections.json`, а для блоков в файле `_blocks.json`.

Можно сгруппировать настройки в отдельный файл, на который потом ссылаться в свойстве `includeShared` секции или блока.

Формат файла

| property | type | description |
| - | - | - |
| `settings` | `SectionPropertyDescriptor[]` | Описание свойств блока |

### Objects
В папке Objects находятся настройки для объектов, которые могут быть добавлены в редактор секции или блока.

Формат файла

| property | type | description |
| - | - | - |
| `settings` | `SectionPropertyDescriptor[]` | Описание свойств блока |

Пример

Редактор кнопки, файл `button.json`
```json
{
  "settings": [
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
```
Теперь в секции можно использовать его
```json
{
  "settings": [
    {
      "id": "button",
      "type": "object",
      "label": "Button",
      "elementDescriptor": "button"
    }
  ]
}
```
