import { Component, OnInit, ViewChild, AfterContentInit, ElementRef } from '@angular/core';
import { BaseControlDirective } from './../base-control.component';
import { NumberControlDescriptor } from '@shared/models';

@Component({
    selector: 'app-number-item',
    templateUrl: './number-item.component.html'
})
export class NumberItemComponent extends BaseControlDirective<NumberControlDescriptor> {

    @ViewChild('control') control: ElementRef<HTMLInputElement>;

    constructor() {
        super();
    }

    getFocusableControl(): ElementRef {
        return this.control;
    }
}
