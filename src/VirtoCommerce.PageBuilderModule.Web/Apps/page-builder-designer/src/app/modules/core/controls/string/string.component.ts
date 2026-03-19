import { ChangeDetectionStrategy, Component, ElementRef, viewChild } from '@angular/core';
import { KeyValuePipe } from '@angular/common';

import { BaseControlDirective } from '@core/controls/base-control.directive';
import { StringDescriptor } from '@models/controls';
import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';

@Component({
    selector: 'app-string',
    templateUrl: './string.component.html',
    styleUrls: ['./string.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [KeyValuePipe, IconButtonComponent]
})
export class StringComponent extends BaseControlDirective<StringDescriptor> {
    readonly control = viewChild.required<ElementRef>('control');
    readonly textarea = viewChild.required<ElementRef>('textarea');

    override getFocusableControl(): ElementRef {
        return this.descriptor?.multiline
            ? this.textarea()
            : this.control();
    }

    raiseOnChange(event: Event) {
        const element = <HTMLInputElement>event.target;
        this.onValueChanged(element.value);
    }

    onTextareaInput(event: Event): void {
        const el = event.target as HTMLTextAreaElement;
        const maxRows = this.descriptor?.maxRowsCount ?? 4;

        const style = getComputedStyle(el);
        const lineHeight = parseFloat(style.lineHeight);
        const paddingY = parseFloat(style.paddingTop) + parseFloat(style.paddingBottom);
        const maxHeight = lineHeight * maxRows + paddingY;

        el.style.height = 'auto';
        el.style.height = `${Math.min(el.scrollHeight, maxHeight)}px`;
        el.style.overflowY = el.scrollHeight > maxHeight ? 'auto' : 'hidden';

        this.onValueChanged(el.value);
    }
}
