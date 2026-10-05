import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { MatCheckbox } from '@angular/material/checkbox';

import { BaseControlDirective } from '@core/controls/base-control.directive';
import { CheckboxDescriptor } from '@models/controls';

@Component({
    selector: 'app-checkbox',
    templateUrl: './checkbox.component.html',
    styleUrls: ['./checkbox.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [MatCheckbox]
})
export class CheckboxComponent extends BaseControlDirective<CheckboxDescriptor> {
    readonly accessibleLabel = input('');
    raiseValueChanged(value: boolean) {
        this.onValueChanged(value);
    }
}
