import { Component, ChangeDetectorRef } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { BaseControlDirective } from '../base-control.component';
import { OptionModel, SelectControlDescriptor } from '@shared/models';
import { SelectItemService } from '../../services/select-item.service';

@Component({
    selector: 'app-select-item',
    templateUrl: './select-item.component.html',
    styleUrls: ['./select-item.component.scss']
})
export class SelectItemComponent extends BaseControlDirective<SelectControlDescriptor> {

    private _options: OptionModel[] = [];

    groupItems = false;
    groups: { [key: string]: { label: string; value: string; }[] };
    value: any;
    isOpen: boolean;
    get option(): OptionModel {
        return this.options.find(x => this.isEqual(x.value, this.value, this.descriptor.equalKey));
    }
    get title(): string {
        return this.option?.label || this.descriptor.placeholder;
    }

    get options(): OptionModel[] {
        return this._options;
    }
    set options(value: OptionModel[]) {
        this._options = value;
        this.refreshValue(null, false);
    }

    constructor(private sanitizer: DomSanitizer, private selectItemService: SelectItemService) {
        super();
    }

    initContent() {
        this.selectItemService.getRequestedOptions(this.descriptor).subscribe(result => {
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
        });
    }

    setValue(value: any) {
        const v = !value && this.descriptor.default ? this.descriptor.default : value;
        this.refreshValue(v);
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
}
