import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';


import { ActionButtonDescriptor } from '@core/models';
import { ActionButtonsComponent } from '@core/components/action-buttons/action-buttons.component';

@Component({
    selector: 'app-actions-panel',
    templateUrl: './actions-panel.component.html',
    styleUrls: ['./actions-panel.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ActionButtonsComponent]
})
export class ActionsPanelComponent {

    readonly panels = input.required<ActionButtonDescriptor[][]>();

    readonly actionExecuted = output<string>();

    onActionExecuted(item: ActionButtonDescriptor) {
        if (item.alias) {
            this.actionExecuted.emit(item.alias);
        }
    }
}
