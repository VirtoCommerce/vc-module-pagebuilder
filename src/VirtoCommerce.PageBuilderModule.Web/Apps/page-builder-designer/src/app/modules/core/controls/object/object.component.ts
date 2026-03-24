import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { UntypedFormGroup } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';

import { BaseControlDirective } from '@core/controls/base-control.directive';
import { ControlDescriptor, ObjectDescriptor } from '@models/controls';

import { ControlContext } from '@core/models';
import { formsHelpers } from '@core/helpers';
import { ChevronComponent } from '@core/components/chevron/chevron.component';
import { ControlsListComponent } from '@core/dynamics/controls-list/controls-list.component';

@Component({
    selector: 'app-object',
    templateUrl: './object.component.html',
    styleUrls: ['./object.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ChevronComponent, ControlsListComponent]
})
export class ObjectComponent extends BaseControlDirective<ObjectDescriptor> {

    private readonly destroyRef = inject(DestroyRef);
    private readonly formReset$ = new Subject<void>();

    objectForm!: UntypedFormGroup;
    readonly expanded = signal(false);

    readonly displayTitle = computed(() =>
        (!!this.descriptor?.displayField && this.controlValue()?.[this.descriptor.displayField])
        || this.descriptor?.label || this.descriptor?.title || '[no title]'
    );

    readonly objectDescriptors = computed<ControlDescriptor[]>(() => this.descriptor?.element || []);

    readonly objectContext = computed<ControlContext>(() =>
        ({ ...this.context, item: this.controlValue(), parent: this.context })
    );

    toggle() {
        this.expanded.update(v => !v);
    }

    protected override applyNewValue(): void {
        const v = this.controlValue() || {};
        if (this.objectForm) {
            this.objectForm.patchValue(v, { emitEvent: false });
            return;
        }
        const descriptors = this.objectDescriptors();
        this.objectForm = formsHelpers.generateForm(v, descriptors);
        this.objectForm.valueChanges.pipe(
            takeUntil(this.formReset$),
            takeUntilDestroyed(this.destroyRef)
        ).subscribe(x => {
            this.defaultValueChanged(x);
        });
    }

}
