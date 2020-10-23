import { Component, ChangeDetectorRef } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { BaseControlDirective } from '../base-control.component';
import { OptionModel, SelectControlDescriptor } from '@shared/models';
import { RequestItemsService } from '../../services/request-items.service';
import { Subject } from 'rxjs';
import { debounceTime, map } from 'rxjs/operators';

import { cloneDeep } from 'lodash-es';
import { getValueOrDefault } from '@app/services/utils';

@Component({
    selector: 'app-select-item',
    templateUrl: './select-item.component.html',
    styleUrls: ['./select-item.component.scss']
})
export class SelectItemComponent extends BaseControlDirective<SelectControlDescriptor> {

    private _options: OptionModel[] = [];

    searchPhrase: string = null;
    groupItems = false;
    groups: { [key: string]: { label: string; value: string; }[] };
    value: any;
    isOpen: boolean;
    get option(): OptionModel {
        return this.options.find(x => this.isEqual(x.value, this.value, this.descriptor.equalKey));
    }
    get title(): string {
        return this.option?.label || (this.value ? this.value[this.descriptor.label] : null) || this.descriptor.placeholder;
    }

    get options(): OptionModel[] {
        return this._options;
    }
    set options(value: OptionModel[]) {
        this._options = value;
        this.refreshValue(null, false);
    }

    constructor(private sanitizer: DomSanitizer, private requestItemsService: RequestItemsService, private cdk: ChangeDetectorRef) {
        super();

        this.searchEvent$.pipe(
            debounceTime(1000)
        ).subscribe(searchQuery => {
            this.searchPhrase = searchQuery;
            this.initContent();
        });
    }

    private searchEvent$ = new Subject<string>();

    initContent() {
        const context = cloneDeep(this.context);
        context.__searchQuery = this.searchPhrase;
        this.requestItemsService.getRequestedOptions(this.descriptor.request, context).pipe(
            map(items => items.map<OptionModel>(x => <OptionModel>{
                label: x[this.descriptor.request.label],
                group: this.descriptor.request.group ? x[this.descriptor.request.group] : null,
                value: x
            }))
        ).subscribe(result => {
            this.options = this.descriptor.options ? this.descriptor.options.concat(result) : result;

            this.groupItems = this.options.some(x => !!x.group);
            if (this.groupItems) {
                this.groups = {};
                this.options.forEach(x => {
                    if (!this.groups[x.group]) {
                        this.groups[x.group] = [];
                    }
                    this.groups[x.group].push(x);
                });
            }
            this.cdk.detectChanges();
        });
    }

    setValue(value: any) {
        const v = getValueOrDefault(value, this.descriptor.default);
        this.refreshValue(v, false);
    }

    private refreshValue(value: any = null, notify: boolean = true) {
        const v = value || this.value;
        const newValue = this.options.map(x => x.value).find(x => this.isEqual(x, v, this.descriptor.equalKey)) || value;
        this.value = newValue;
        if (notify) {
            this.onChange(this.value);
        }
    }

    isEqual(x, y, key) {
        return !key ? x === y : (!x && !y) || ((x && y) && (x[key] === y[key]));
    }

    getDisplayValue(option: OptionModel) {
        return this.sanitizer.bypassSecurityTrustHtml(option?.label);
    }

    getTrackValue(option: OptionModel) {
        return option.value;
    }

    selectValue(value: any) {
        this.value = value;
        this.isOpen = false;
        this.refreshValue();
    }

    toggle() {
        this.isOpen = !this.isOpen;
    }

    onSearch(event) {
        this.searchEvent$.next(event.target.value);
    }
}
