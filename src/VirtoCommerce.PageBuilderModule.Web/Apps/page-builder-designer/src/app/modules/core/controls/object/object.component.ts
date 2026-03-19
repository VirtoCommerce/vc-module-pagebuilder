import { Component, DestroyRef, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NgClass } from '@angular/common';
import { UntypedFormGroup } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';

import { BaseControlDirective } from '@core/controls/base-control.directive';
import { ControlDescriptor, ObjectDescriptor } from '@models/controls';

import { ControlContext } from '@core/models';
import { coreHelpers, formsHelpers } from '@core/helpers';
import { ChevronComponent } from '@core/components/chevron/chevron.component';
import { ControlsListComponent } from '@core/dynamics/controls-list/controls-list.component';

@Component({
    selector: 'app-object',
    templateUrl: './object.component.html',
    styleUrls: ['./object.component.scss'],
    imports: [NgClass, ChevronComponent, ControlsListComponent]
})
export class ObjectComponent extends BaseControlDirective<ObjectDescriptor> {

    private readonly destroyRef = inject(DestroyRef);
    private readonly formReset$ = new Subject<void>();

    objectForm!: UntypedFormGroup;
    expanded = false;

    getTitle(): string {
        return (!!this.descriptor?.displayField && this.controlValue()[this.descriptor.displayField]) || this.descriptor?.label || this.descriptor?.title || '[no title]';
    }

    getDescriptors(): ControlDescriptor[] {
        return this.descriptor?.element || []; // formsHelpers.mergeDescriptors(this.context.objects, this.descriptor);
    }

    toggle() {
        this.expanded = !this.expanded;
    }

    getContext(): ControlContext {
        return { ...this.context, item: this.controlValue(), parent: this.context /*, filter: null */ };
    }

    override setControlValue(value: any) {
        if (this.controlValue() !== value || !this.objectForm) {
            const descriptors = this.getDescriptors();
            // we don't need create default value for empty object. Only when create new section or list item
            // const v = value || coreHelpers.createDefaultObject(descriptors);
            const v = value || {};
            super.setControlValue(v);
            this.objectForm = formsHelpers.generateForm(v, descriptors);
            this.formReset$.next();
            this.objectForm.valueChanges.pipe(
                takeUntil(this.formReset$),
                takeUntilDestroyed(this.destroyRef)
            ).subscribe(x => {
                this.onValueChanged(x);
            });
        }
    }

    override registerOnValueChanged(fn: any): void {
        this.onValueChanged = value => {
            this.controlValue.set(value);
            fn(value);
        };
    }

}
