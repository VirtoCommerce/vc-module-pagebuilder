import { Type, Injectable } from '@angular/core';
import {
    CalendarItemComponent,
    CheckboxItemComponent,
    ColorItemComponent,
    FileItemComponent,
    ImageItemComponent,
    NumberItemComponent,
    SelectItemComponent,
    StringItemComponent,
    TextItemComponent,
    UrlItemComponent,
    PopupListItemComponent
} from '.';

@Injectable({
    providedIn: 'root'
})
export class ControlsFactory {
    private controls: { [key: string]: Type<any> } = {};

    constructor() {
        this.controls['calendar'] = CalendarItemComponent;
        this.controls['checkbox'] = CheckboxItemComponent;
        this.controls['color'] = ColorItemComponent;
        this.controls['file'] = FileItemComponent;
        this.controls['image'] = ImageItemComponent;
        this.controls['number'] = NumberItemComponent;
        this.controls['select'] = SelectItemComponent;
        this.controls['string'] = StringItemComponent;
        this.controls['text'] = TextItemComponent;
        this.controls['url'] = UrlItemComponent;
        this.controls['popup-list'] = PopupListItemComponent;
    }

    resolve(type: string): Type<any> {
        const result = this.controls[type];
        return result;
    }
}
