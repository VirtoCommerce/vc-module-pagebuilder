import { ChangeDetectionStrategy, Component, ElementRef, viewChild, inject } from '@angular/core';
import { NgClass } from '@angular/common';
import { MatSlider, MatSliderThumb } from '@angular/material/slider';

import { EnvironmentRef } from '@integration/services';
import { BaseControlDirective } from '@core/controls/base-control.directive';
import { NumberDescriptor } from '@models/controls';

@Component({
    selector: 'app-number',
    templateUrl: './number.component.html',
    styleUrls: ['./number.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgClass, MatSlider, MatSliderThumb]
})
export class NumberComponent extends BaseControlDirective<NumberDescriptor> {
    private readonly windowRef = inject(EnvironmentRef);
    readonly control = viewChild<ElementRef<HTMLInputElement>>('control');

    onPaste(event: ClipboardEvent) {
        const value = (event.clipboardData || this.windowRef.nativeWindow.clipboardData).getData('text');
        if (!isNaN(value)) {
            this.onValueChanged(Number(value));
        }
    }

    raiseOnChange(target: EventTarget | null) {
        const element = <HTMLInputElement>target;
        if (element) {
            this.onValueChanged(element.valueAsNumber);
        }
    }

    raiseOnTouched(target: EventTarget | null) {
        const element = <HTMLInputElement>target;
        if (!!element) {
            this.onControlTouched(element.valueAsNumber);
        }
    }

    sliderChanged(value: number) {
        this.onValueChanged(value);
    }

    override getFocusableControl(): ElementRef | null {
        return this.control() ?? null;
    }
}
