import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

import { ActionButtonDescriptor } from '@core/models';
import { IconButtonComponent } from '../icon-button/icon-button.component';

@Component({
    selector: 'app-action-buttons',
    templateUrl: './action-buttons.component.html',
    styleUrls: ['./action-buttons.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [IconButtonComponent]
})
export class ActionButtonsComponent {

    readonly actions = input<ActionButtonDescriptor[]>([]);

    readonly onClick = output<ActionButtonDescriptor>();

    raiseOnClick(action: ActionButtonDescriptor) {
        this.onClick.emit(action);
    }
}
