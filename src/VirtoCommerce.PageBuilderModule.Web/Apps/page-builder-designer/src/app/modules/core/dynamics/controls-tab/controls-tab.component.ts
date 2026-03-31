import { ChangeDetectionStrategy, Component, input } from '@angular/core';

import { ReactiveFormsModule, UntypedFormGroup } from '@angular/forms';

import { ControlContext, TabModel, GroupsStateModel } from '@core/models';
import { ControlsListComponent } from '@core/dynamics/controls-list/controls-list.component';
import { ControlsGroupComponent } from '@core/dynamics/controls-group/controls-group.component';

@Component({
    selector: 'app-controls-tab',
    templateUrl: './controls-tab.component.html',
    styleUrls: ['./controls-tab.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, ControlsListComponent, ControlsGroupComponent]
})
export class ControlsTabComponent {
    readonly tab = input.required<TabModel>();
    readonly state = input.required<GroupsStateModel>();
    readonly currentForm = input.required<UntypedFormGroup>();
    readonly context = input.required<ControlContext>();
}
