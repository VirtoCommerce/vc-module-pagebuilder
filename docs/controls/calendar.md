# Calendar Control Descriptor

This control allows users to select dates, times, or both, using a configurable date/time picker based on [datetime-picker](https://h2qutc.github.io/angular-material-components).

| Property  | Type                                                | Description                                                                 |
| --------- | --------------------------------------------------- | --------------------------------------------------------------------------- |
| `mode`    | date <br> datetime <br> time <br> month <br> year   | Defines the mode of the calendar: full datetime, date only, time only, etc. |
| `minDate` | Date \| string                                      | The minimum selectable date. Accepts a Date object or a natural language string parsed by [chrono-node](https://github.com/wanasit/chrono). |
| `maxDate` | Date \| string                                      | The maximum selectable date. Accepts a Date object or a natural language string parsed by [chrono-node](https://github.com/wanasit/chrono). |
| `inline`  | boolean                                             | Displays the calendar inline instead of as a popup.                         |

### Natural language dates (chrono-node)

`minDate` and `maxDate` accept human-readable strings that are resolved at runtime using [chrono-node](https://github.com/wanasit/chrono) — a natural language date parser. This makes it easy to express relative date constraints without hardcoding exact values.

Common expressions:

| Expression | Resolves to |
| --- | --- |
| `"today"` | Start of the current day |
| `"tomorrow"` | Start of the next day |
| `"yesterday"` | Start of the previous day |
| `"this week"` | First day of the current week |
| `"last week"` | First day of the previous week |
| `"next week"` | First day of the next week |
| `"this month"` | First day of the current month |
| `"last month"` | First day of the previous month |
| `"next month"` | First day of the next month |
| `"this year"` | January 1st of the current year |
| `"in 7 days"` | 7 days from now |
| `"3 months ago"` | 3 months before today |
| `"next Monday"` | The coming Monday |

## Example

```json
...
    "settings": [
        {
            "id": "date",
            "label": "Date",
            "type": "calendar",
            "placeholder": "Please choose a date",
            "hint": "You can select date in calendar",
            "info": "This control returns a JavaScript Date object"
        },
        {
            "id": "futureDate",
            "label": "Future date only",
            "type": "calendar",
            "mode": "date",
            "minDate": "today"
        },
        {
            "id": "currentMonth",
            "label": "From start of month",
            "type": "calendar",
            "mode": "date",
            "minDate": "this month"
        },
        ...
    ]
...
```

Some of the available calendar modes are as follows:

![Calendar modes](media/calendar-modes.png)

<br>
<br>
********

<div style="display: flex; justify-content: space-between;">
    <a href="../component-context">← Component context </a>
    <a href="../checkbox">Checkbox →</a>
</div>