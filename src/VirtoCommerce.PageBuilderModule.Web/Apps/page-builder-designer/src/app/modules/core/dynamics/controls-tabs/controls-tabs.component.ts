import { ChangeDetectionStrategy, Component, input, OnInit } from '@angular/core';
import { ReactiveFormsModule, UntypedFormGroup } from '@angular/forms';
import { MatTabGroup, MatTab } from '@angular/material/tabs';

import { ControlContext, GroupsStateModel, TabModel } from '@core/models';
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
                    tabOrder: undefined as number | undefined,
                    groups: {},
                    ungrouped: []
                };
            }
            if (item.tabOrder != null && (acc[key].tabOrder == null || item.tabOrder < acc[key].tabOrder)) {
                acc[key].tabOrder = item.tabOrder;
            }
            if (!!item.group) {
                if (!acc[key].groups[item.group]) {
                    acc[key].groups[item.group] = {
                        order: Object.keys(acc[key].groups).length,
                        groupOrder: undefined as number | undefined,
                        descriptors: []
                    };
                    this.groupsState[item.group] = { opened: false };
                }
                if (item.groupOrder != null && (acc[key].groups[item.group].groupOrder == null || item.groupOrder < acc[key].groups[item.group].groupOrder)) {
                    acc[key].groups[item.group].groupOrder = item.groupOrder;
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
            })).sort((a, b) => {
                const aHasOrder = a.groupOrder != null;
                const bHasOrder = b.groupOrder != null;
                if (aHasOrder && bHasOrder) return a.groupOrder - b.groupOrder;
                if (aHasOrder) return -1;
                if (bHasOrder) return 1;
                return a.order - b.order;
            })
        })).filter(x => x.ungrouped.length || Object.keys(x.groups).length) // hide empty tabs
            .sort((a, b) => {
                if (!a.label) return -1; // default tab always first
                if (!b.label) return 1;
                const aHasOrder = a.tabOrder != null;
                const bHasOrder = b.tabOrder != null;
                if (aHasOrder && bHasOrder) return a.tabOrder - b.tabOrder;
                if (aHasOrder) return -1;
                if (bHasOrder) return 1;
                return a.order - b.order;
            });

        const keys = Object.keys(tabs);
        this.singleList = keys.length === 1 && keys[0] === '';
        this.hasTabs = keys.length > 1 || (keys.length === 1 && keys[0] !== '');
    }
}
