import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

import { MatButton } from '@angular/material/button';
import { MatMenu, MatMenuTrigger, MatMenuItem } from '@angular/material/menu';
import { ActionButtonDescriptor } from '@core/models';
import { IconComponent } from '../icon/icon.component';
import { ChevronComponent } from '../chevron/chevron.component';

@Component({
    selector: 'app-actions-dropdown',
    templateUrl: './actions-dropdown.component.html',
    styleUrls: ['./actions-dropdown.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [MatButton, MatMenu, MatMenuTrigger, MatMenuItem, IconComponent, ChevronComponent]
})
export class ActionsDropdownComponent {

    isOpened = false;

    readonly defaultTitle = input<string>();
    readonly panelClass = input<string>();
    readonly displayChevron = input(true);
    readonly displayCurrent = input(true);
    readonly active = input<string>();
    readonly placeholder = input<ActionButtonDescriptor | null>(null);
    readonly actions = input<ActionButtonDescriptor[]>([]);

    readonly executeAction = output<ActionButtonDescriptor>()

    get activeItem(): ActionButtonDescriptor | null {
        return this.placeholder() || this.actions().find(x => x.alias === this.active() || (!x.alias && !this.active())) || null;
    }

    actionChoosed(action: ActionButtonDescriptor) {
        this.executeAction.emit(action);
    }
}
