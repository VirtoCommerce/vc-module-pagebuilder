import { ChangeDetectionStrategy, Component, input } from "@angular/core";
import { DisplayTextDescriptor } from '@models/controls';

import { ReactiveFormsModule, UntypedFormGroup } from '@angular/forms';

import { ControlContext } from '@core/models';
import { BaseControlDescriptor } from '@models/controls';
import { appHelpers } from '@integration/helpers';
import { ControlHolderComponent } from '@core/dynamics/control-holder.component';

@Component({
    selector: 'app-controls-list',
    templateUrl: './controls-list.component.html',
    styleUrls: ['./controls-list.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, ControlHolderComponent]
})
export class ControlsListComponent {

    readonly currentForm = input.required<UntypedFormGroup>();
    readonly context = input.required<ControlContext>();
    readonly descriptors = input.required<BaseControlDescriptor[]>(); // todo: controls order

    isTextBlock(control: BaseControlDescriptor): control is DisplayTextDescriptor {
        return control.type === 'header' || control.type === 'paragraph';
    }

    checkVisibility(descriptor: BaseControlDescriptor): boolean {
        if (!!descriptor.visibility && !descriptor.hidden) {
            try {
                const result = appHelpers.evalInContext(descriptor.visibility!, this.context());
                return result;
            } catch (error) {
                console.error(error);
            }
        }
        return !descriptor.hidden;
    }
}
