# ServerRequestDescriptor

этот интерфейс описывает запрос к серверу, который будет выполнен для получения данных. Используется в [настройках](./builder-settings.md) билдера, а также в контролах [search](./controls/search.md) и [select](./controls/select.md) Он включает в себя URL, метод HTTP, тело запроса и другие параметры. Также он может содержать информацию о кэшировании и инициализации.

| Property | Type | Description |
| --- | --- | --- |
| `url` | `string` | URL for the request |
| `method` | `string` | HTTP method (GET, POST, etc.) |
| `body` | `any` | Body of the request |
| `form` | `any` | Form data for the request |
| `options` | `any` | Additional options for the request, like `headers` and so on |
| `response` | `ServerResponseDescriptor` | Describes response from the server |
| `cacheable` | `boolean` | Indicates if the request is cacheable |
| `init` | `boolean \| string` | Indicates if the request is an initialization request in settings. If `true`, the request will be sent when the settings file was loaded. If a string, it will be property name where the descriptor is written. |
| `fallbackValue` | `any` | Fallback value for the request. If the request fails, this value will be used. |

## ServerResponseDescriptor

| Property | Type | Description |
| --- | --- | --- |
| `selector` | `string` | Script to eval over response |
| `result` | `string` | Path to result field (jsonpath) |
| `isArray` | `boolean` | Indicates if the result should be an array |
| `value` | `string \| (string \| SelectValueDescriptor)[]` | Value of the request. If it is an array, it will be used as a list of options for the select control. If it is a string, it will be used as a value for the control. |

todo: examples
