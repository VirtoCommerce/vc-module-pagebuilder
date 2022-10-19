# calendar control descriptor

Нет дополнительных настроек.

Котрол использует [MatDatepickerModule](https://material.angular.io/components/datepicker/overview) из Material Angular. В качестве адаптера используется [moment.js](https://momentjs.com/).

## Пример:

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

Результат

![Calendar control example](images/calendar-control.png "Calendar control example")
![Calendar control example in open state](images/calendar-control-opened.png "Calendar control example in open state")
