import { Component, OnInit, HostBinding, ViewContainerRef } from '@angular/core';
import { ColorControlDescriptor } from '@shared/models';
import { BaseControlDirective } from './../base-control.component';

@Component({
    selector: 'app-color-item',
    templateUrl: './color-item.component.html',
    styleUrls: ['./color-item.component.scss']
})
export class ColorItemComponent extends BaseControlDirective<ColorControlDescriptor> {

    @HostBinding('class') css = 'form-color';

    constructor() { super(); }

    registerOnChange(fn: any): void {
        this.onChange = value => {
            this.value = value;
            fn(value);
        };
    }

    onClear($event) {
        this.onChange('');
    }
}
