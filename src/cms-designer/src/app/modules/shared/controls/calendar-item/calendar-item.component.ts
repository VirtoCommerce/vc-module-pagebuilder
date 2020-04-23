import { Component, HostListener, ViewChild, ElementRef } from '@angular/core';
import { BaseControlDirective } from '../base-control.component';
import { CheckboxControlDescriptor } from '@shared/models';

@Component({
    selector: 'app-calendar-item',
    templateUrl: './calendar-item.component.html',
    styleUrls: ['./calendar-item.component.scss']
})
export class CalendarItemComponent extends BaseControlDirective<CheckboxControlDescriptor> {

    @ViewChild('control') control: ElementRef;

    onDateChange(event) {
        this.onChange(event.target.value);
    }

    getFocusableControl(): ElementRef {
        return this.control;
    }
}
