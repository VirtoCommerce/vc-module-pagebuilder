import { GroupsStateModel, TabModel } from '@core/models';
import { ChangeDetectionStrategy, Component, input, OnInit } from '@angular/core';

import { ReactiveFormsModule, UntypedFormGroup } from '@angular/forms';
import { MatTabGroup, MatTab } from '@angular/material/tabs';

import { ControlContext } from '@core/models';
import { BaseControlDescriptor } from '@models/controls';
import { ControlsTabComponent } from '@core/dynamics/controls-tab/controls-tab.component';

@Component({
    selector: 'app-controls-tabs',
    templateUrl: './controls-tabs.component.html',
    styleUrls: ['./controls-tabs.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, MatTabGroup, MatTab, ControlsTabComponent]
})
export class ControlsTabsComponent implements OnInit {

    readonly currentForm = input.required<UntypedFormGroup>();
    readonly context = input.required<ControlContext>();
    readonly descriptors = input.required<BaseControlDescriptor[]>();

    groupsState: GroupsStateModel = {};

    tabs: TabModel[] = [];
    hasTabs: boolean = false;
    singleList: boolean = false;

    ngOnInit(): void {
        this.groupsState = {};
        const tabs = this.descriptors().reduce((acc, item) => {
            const key = item.tab || '';
            if (!acc[key]) {
                acc[key] = {
                    order: Object.keys(acc).length,
                    groups: {},
                    ungrouped: []
                };
            }
            if (!!item.group) {
                if (!acc[key].groups[item.group]) {
                    acc[key].groups[item.group] = {
                        order: Object.keys(acc[key].groups).length,
                        descriptors: []
                    };
                    this.groupsState[item.group] = { opened: false };
                }
                acc[key].groups[item.group].descriptors.push(item);
            } else {
                acc[key].ungrouped.push(item);
            }
            return acc;
        }, <any>{});

        this.tabs = Object.keys(tabs).map(x => ({
            ...tabs[x],
            label: x,
            groups: Object.keys(tabs[x].groups).map(y => ({
                ...tabs[x].groups[y],
                name: y
            })).sort((a, b) => a.order - b.order) // sort groups
        })).filter(x => x.ungrouped.length || Object.keys(x.groups).length) // hide empty tabs
            .sort((a, b) => !a.label && -1 || a.order - b.order); // default tab is first

        const keys = Object.keys(tabs);
        this.singleList = keys.length === 1 && keys[0] === '';
        this.hasTabs = keys.length > 1 || (keys.length === 1 && keys[0] !== '');
    }
}
