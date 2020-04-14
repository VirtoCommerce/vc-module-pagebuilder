import { Component, ViewChild, ElementRef } from '@angular/core';
import { BaseControlDirective } from '../base-control.component';
import { StringControlDescriptor } from '@shared/models';
import { WindowRef } from '@app/services';

@Component({
    selector: 'app-string-item',
    templateUrl: './string-item.component.html',
    styleUrls: ['./string-item.component.scss']
})
export class StringItemComponent extends BaseControlDirective<StringControlDescriptor> {

    @ViewChild('control') control: ElementRef;
    @ViewChild('textarea') textarea: ElementRef;

    constructor(private windowRef: WindowRef) {
        super();
    }

    onPaste(event) {
        const value = (event.clipboardData || this.windowRef.nativeWindow.clipboardData).getData('text');
        this.onChange(value);
    }

    getFocusableControl(): ElementRef {
        return this.descriptor.multiline ? this.textarea : this.control;
    }
}
