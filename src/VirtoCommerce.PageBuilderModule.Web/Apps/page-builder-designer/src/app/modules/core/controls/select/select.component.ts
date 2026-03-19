import { NgSelectComponent, NgSelectModule } from '@ng-select/ng-select';
import { of } from 'rxjs';
import { DataService } from '@core/services';
import { Component, computed, DestroyRef, signal, viewChild, inject } from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, Subject } from 'rxjs';
import { distinctUntilChanged, map } from 'rxjs/operators';

import { SelectDescriptor } from '@models/controls';
import { BaseControlDirective } from '@core/controls/base-control.directive';
import { ReactiveFormsModule, UntypedFormControl, UntypedFormGroup } from '@angular/forms';

import { appHelpers } from '@integration/helpers';

/**
 * https://ng-select.github.io/ng-select#/data-sources
 * https://github.com/ng-select/ng-select
 */

@Component({
    selector: 'app-select',
    templateUrl: './select.component.html',
    styleUrls: ['./select.component.scss'],
    imports: [ReactiveFormsModule, NgSelectModule]
})
export class SelectComponent extends BaseControlDirective<SelectDescriptor> {

    private readonly destroyRef = inject(DestroyRef);
    private readonly data = inject(DataService);

    form!: UntypedFormGroup;
    searchEvent$ = new Subject<string>();

    private readonly searchQuery = signal<string | null>(null);

    readonly optionsResource = rxResource({
        params: () => this.searchQuery(),
        stream: ({ params: query }) => this.doRequest(query)
    });

    readonly effectiveOptions = computed<any[]>(() => {
        if (this.descriptor?.optionsSelector) {
            return appHelpers.evalInContext(this.descriptor.optionsSelector, this.context) as any[] ?? [];
        }
        return this.optionsResource.value() as any[] ?? [];
    });

    readonly select = viewChild.required(NgSelectComponent);

    raiseValueChanged(_event: any) { }

    compareWith = (itemInSelect: any, itemInSource: any) => {
        const vA = (itemInSelect.value || itemInSelect)[this.descriptor?.equalKey || 'value'] || itemInSelect.value || itemInSelect;
        const v0 = (itemInSource.value || itemInSource)[this.descriptor?.equalKey || 'value'] || itemInSource.value || itemInSource;
        return vA === v0;
    }

    override initContent() {
        super.initContent();
        this.form = new UntypedFormGroup({
            value: new UntypedFormControl(this.selectControlValue)
        });

        if (this.descriptor?.searchable) {
            this.searchEvent$.pipe(
                distinctUntilChanged(),
                takeUntilDestroyed(this.destroyRef)
            ).subscribe(q => this.searchQuery.set(q));
        }

        this.form.valueChanges.pipe(
            takeUntilDestroyed(this.destroyRef)
        ).subscribe({
            next: (v) => {
                this.onValueChanged(v.value);
            }
        });
    }

    get selectControlValue(): any {
        if (this.controlValue() && Array.isArray(this.controlValue())) {
            return this.controlValue().map((x: any) => this.convertItemToOption(x));
        }
        return this.convertItemToOption(this.controlValue());
    }

    unselect(item: any) {
        this.form.controls['value'].setValue(
            this.selectControlValue.filter((x: any) => !this.compareWith(x, item)),
            { emitEvent: true }
        );
    }

    private convertItemToOption(item: any) {
        if (!item) {
            return null;
        }
        return {
            label: this.descriptor!.request?.label && item[this.descriptor!.request.label] || item,
            group: this.descriptor!.request?.group && item[this.descriptor!.request.group] || null,
            value: this.descriptor!.request?.value && item[this.descriptor!.request.value] || item,
        };
    }

    private doRequest(filter: string | null): Observable<any[]> {
        let result: Observable<any[]> = of([]);
        if (this.descriptor?.request) {
            const context = { ...this.context, __searchQuery: filter };
            result = this.data.doRequest(this.descriptor.request, context).pipe(
                map(items => items?.map((x: any) => this.convertItemToOption(x)) || []),
            );
        }
        return result.pipe(
            map(items => [...this.descriptor?.options || [], ...items]),
            map(items => !filter || !this.descriptor?.searchable
                ? items
                : items.filter(item => item.label.toLocaleUpperCase().indexOf(filter.toLocaleUpperCase()) !== -1)
            )
        );
    }
}
