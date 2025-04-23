# Select Control Descriptor

| Property | Type | Description |
| --- | --- | --- |
| `options` | `OptionModel[]` | Set of options for the `select` control |
| `optionsSelector` | `string` | Option selection from the current [`ComponentContext`](../component-context.md) |
| `request` | `OptionsRequest` | Load options via a web request ***todo: link to request description*** |
| `equalKey` | `string` | Key used to compare options |
| `filterList` | `boolean` | Whether the options can be filtered |
| `multiple` | `boolean` | Allows multiple selections ***todo: (not implemented)*** |

The `options` property defines an array of values that will be available in the dropdown list. These values can be grouped using the `group` property.

The `request` property allows specifying a request to fetch data. The `label` property from the result will be used for display, and the option's value is taken from the corresponding field. The fetched data is merged with the array from `options`.

The `optionsSelector` property allows specifying a JavaScript snippet that is executed in the context of [`ComponentContext`](../component-context.md) and must return an array.

Example:

```js
this.page.filter(function(x) { return x.type==='popup' }).map(function(x) { return { label: x.name || x.__id, value: x.__id }; })
```

This script filters all page blocks by type and selects the needed values.

The result is also merged with the list from the `options` property.

If the control has a pre-defined value, it will be searched using the `equalKey` after the data is loaded.

The `optionsSelector` is ignored if the `request` property is defined.

## OptionsRequest

Inherits from `ServerRequestDescriptor`, with added `group` and `label` properties. Both are of type `string`, indicating the names of the properties used from the server response.

More details on request formation can be found on the [`request`](../request.md) page.

## SelectValueDescriptor

In addition to the `label` and `value` properties, there is the `selectValueDescritor` property in the `ServerResponseDescriptor` interface. This property is used to specify the value of the option in the select control. It can be a string or an object with the following properties:

| Property | Type | Description |
| --- | --- | --- |
| `key` | `string` | Property name for the target value |
| `query` | `string` | Query to get the value (jsonpath) |
| `isArray` | `boolean` | Indicates if the value is an array |


## OptionModel

| Property | Type | Description |
| --- | --- | --- |
| `label` | `string` | Display label |
| `value` | `any` | Value of the option |
| `group` | `string` | Group name |

## Examples

### Basic Select

<details>
    <summary>Expand</summary>

```json
...
    "settings": [
        {
            "id": "theme",
            "type": "select",
            "label": "Theme",
            "placeholder": "Please select theme",
            "options": [
                { "label": "Base", "value": "base" },
                { "label": "Red", "value": "red" },
                { "label": "Green", "value": "green" },
                { "label": "Blue", "value": "blue" }
            ]
        },
        ...
    ]
...
```

Result

![Basic select control example](images/select-control-basic.png "Basic select control example")

To set a default value:

```json
...
    "settings": [
        {
            "id": "theme",
            "type": "select",
            "label": "Theme",
            "placeholder": "Please select theme",
            "default": "base",
            "options": [
                { "label": "Base", "value": "base" },
                { "label": "Red", "value": "red" },
                { "label": "Green", "value": "green" },
                { "label": "Blue", "value": "blue" }
            ]
        },
        ...
    ]
...
```

Result

![Basic select control with default value example](images/select-control-basic-default.png "Basic select control example with default value")

</details>

### Server Request

<details>
    <summary>Expand</summary>

```json
...
    "settings": [
        {
            "id": "category",
            "label": "Choose category",
            "type": "select",
            "equalKey": "id",
            "default": null,
            "request": {
                "url": "/api/reverse-proxy/{{location.params.storeId}}/odt/api/catalog/search/categories",
                "method": "post",
                "body": {
                    "objectType": "Category",
                    "catalogId": "4974648a41df4e6ea67ef2ad76d7bbd4"
                },
                "response": {
                    "result": "items",
                    "isArray": true,
                    "value": [
                        "id",
                        "name"
                    ]
                },
                "label": "name"
            }
        },
        ...
    ]
...
```

Result

![Select control with request example](images/select-control-request.png "Select control with request example")

</details>

### Context-based Selection

<details>
    <summary>Expand</summary>

```json
...
    "settings": [
        {
            "id": "popupId",
            "label": "Select target popup",
            "type": "select",
            "optionsSelector": "this.page.filter(x => x.type==='popup').map(x => ({ label: x.name || x.__id, value: x.__id }))"
        },
        ...
    ]
...
```

Result

The page contains 4 controls, 2 of which are of type `popup`.

![Select control context page example](images/select-control-context-page.png "Select control context page example")

The user can choose the desired block

![Select control context example](images/select-control-context.png "Select control context example")

</details>

