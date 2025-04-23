# Calendar Control Descriptor

[datetime-picker](https://h2qutc.github.io/angular-material-components) is used for this control.

| Property | Type | Description |
| - | - | - |
| `mode` | `date` \| `datetime` \| `time` \| `month` \| `year` | Mode of the calendar |
| `minDate` | `Date` | The minimum available date |
| `maxDate` | `Date` | The maximum available date |
| `inline` | `boolean` | Calendar will be displayed inline, not popup. |


## Example:

```json
...
    "settings": [
        {
            "id": "date",
            "label": "Date",
            "type": "calendar",
            "placeholder": "Please choose a date",
            "hint": "You can select date in calendar",
            "info": "Calendar control returns a Javascript Date object"
        },
        ...
    ]
...
```

![Calendar control example](images/calendar-raw.png "Calendar control example")
![Calendar example in inline mode](images/calendar-inline.png "Calendar example in inline mode")
![Calendar example, choose month](images/calendar-month.png "Calendar example, choose month")
![Calendar example, choose time](images/calendar-time.png "Calendar example, choose time")
![Calendar example, mixed mode](images/calendar-datetime.png "Calendar example, mixed mode")
