import { Component } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { BaseControlDirective } from '../base-control.component';
import { OptionModel, SelectControlDescriptor } from '@shared/models';
import { SelectItemService } from '../../services/select-item.service';
import { TypeaheadOptions } from 'ngx-bootstrap';

@Component({
    selector: 'app-select-item',
    templateUrl: './select-item.component.html',
    styleUrls: ['./select-item.component.scss']
})
export class SelectItemComponent extends BaseControlDirective<SelectControlDescriptor> {

    private _options: OptionModel[];

    groupItems = false;
    groups: { [key: string]: { label: string; value: string; }[] };
    value: OptionModel;
    isOpen: boolean;

    get options(): OptionModel[] {
        return this._options;
    }
    set options(value: OptionModel[]) {
        this._options = value;
        this.refreshValue();
    }

    constructor(private sanitizer: DomSanitizer, private readonly selectItemService: SelectItemService) {
        super();
    }

    initContent() {
        this.selectItemService.getRequestedOptions(this.descriptor).subscribe(result => {
            this.options = this.descriptor.options.concat(result);

            this.groupItems = this.descriptor.options.some(x => !!x.group);
            if (this.groupItems) {
                this.groups = {};
                this.descriptor.options.forEach(x => {
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

    private refreshValue(value: any = null) {
        const v = value || this.value;
        const newValue = this.options?.find(x => this.isEqual(x.value, v, this.descriptor.equalKey)) || value;
        this.value = newValue;
        this.onChange(this.value);
    }

    private isEqual(x, y, key) {
        return !key ? x === y : x[key] === y[key];
    }

    getTitle(): string {
        return !!this.value ? this.value.label : this.descriptor.placeholder;
    }

    getDisplayValue(option: OptionModel) {
        return this.sanitizer.bypassSecurityTrustHtml(option?.label);
    }

    getTrackValue(option: OptionModel) {
        return option.value;
    }

    selectValue(option: OptionModel) {
        this.value = option;
        this.isOpen = false;
        this.onChange(option?.value);
    }

    toggle() {
        this.isOpen = !this.isOpen;
    }
}
