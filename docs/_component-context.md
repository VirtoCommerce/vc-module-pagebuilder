# Component Context

описать что это за контекст

| Property | Type | Description |
| --- | --- | --- |
| `model` | `any` | Current item under editing, can be block, section or settings |
| `block` | `SectionModel` \| `null` | Current block or null |
| `section` | `SectionModel` \| `null` | Current section, useful in child blocks |
| `template` | `TemplateModel` \| `null` | Current template |
| `page` | `SectionModel[]` | Current page, array of sections |
| `settings` | `SectionModel` | Settings of the current template |
| `schema` | `SectionPropertyDescriptor[]` | Current schema of the current section or the current block |
| `blockSchema` | `SectionPropertyDescriptor[]` | Current schema of the current block or null |
| `sectionSchema` | `SectionPropertyDescriptor[]` | Current schema of the current section or parent section of the current block |
| `objects` | `ObjectsSchemasList` | List of objects schemas |
| `index` | `number` | Index of the current item in the list if current section is list-like control |
| `item` | `any` | Current value |
| `parent` | `ControlContext` | Parent context for inner lists |
| `file` | `AssetFile` | file or image for correspond control |
| `element` | `any` | Current item in the list-like control |
| `utils` | list of functions | some helpers functions. see below |
| `__searchQuery` | `string` | current request when in `search` and `select` controls |


## utils

* `spreadPropertyByOther(obj: any, keyProperty: string, ...spreadProperties: string[]): any`

* `generateAnchor(value: string): string`

* `generateUniqueString(length: number): string`

* `onlyLettersAndDigits(value: string): string`

* `template(value: string, ...args: any): string`

* `evalInContext(expr: string, context: any): any`

* `getValueOrDefault(value: any, defaultValue: any = null): any`

* `getValueByPath(model: any, path: any): any`

* `stripHtmlTags(str: string): string`

* `combine(...parts: string[]): string`

* `toList(obj: any, keyPropertyName: string): any[]`

* `tryParseJson(value: string): any`

* `getItemValue(item: any, descriptor: (string | ValueDescriptorModel)[]): any`

* `arrayCastByConfig(item: any, isArray: boolean | null = null): any`

* `cutString(value: string, length = 50): string`

