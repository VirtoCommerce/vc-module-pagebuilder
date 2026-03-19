import { Component, DestroyRef, Input, input, OnInit, output, ChangeDetectorRef, NgZone, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { ReactiveFormsModule, UntypedFormGroup } from '@angular/forms';
import { Subject } from 'rxjs';

import { formsHelpers } from '@core/helpers';
import { ControlContext, ModelChangedEventArgs } from '@core/models';
import { BaseControlDescriptor } from '@models/controls';
import { SectionModel } from '@models/document';
import { ControlsTabsComponent } from '@core/dynamics/controls-tabs/controls-tabs.component';

@Component({
    selector: 'app-dynamic-form',
    templateUrl: './dynamic-form.component.html',
    styleUrls: ['./dynamic-form.component.scss'],
    imports: [ReactiveFormsModule, ControlsTabsComponent]
})
export class DynamicFormComponent implements OnInit {

    private readonly destroyRef = inject(DestroyRef);
    private readonly cdr = inject(ChangeDetectorRef);
    private readonly zone = inject(NgZone);
    private readonly formReset$ = new Subject<void>();

    private _sectionModel!: SectionModel;
    private _descriptors!: BaseControlDescriptor[];
    private _currentSectionId: string | null = null;
    private _currentSection: object | null = null;

    @Input({ required: true }) get sectionModel(): SectionModel {
        return this._sectionModel;
    }
    set sectionModel(value: SectionModel) {
        if (this._sectionModel !== value) {
            this._sectionModel = value;
            this.generateForm(true);
        }
    }
    readonly context = input.required<ControlContext>();
    @Input({ required: true }) get descriptors(): BaseControlDescriptor[] {
        return this._descriptors;
    }
    set descriptors(value: BaseControlDescriptor[]) {
        if (this._descriptors !== value) {
            this._descriptors = value;
            this.generateForm();
        }
    }
    readonly modelChanged = output<ModelChangedEventArgs>();

    form: UntypedFormGroup | null = null;

    ngOnInit(): void {
        this.generateForm();
    }

    private generateForm(modelChanged: boolean = false) {
        const m = this.sectionModel;
        if (m && !!this.descriptors && (!this.form || m.id !== this._currentSectionId)) {
            this._currentSectionId = m.id;
            this.form = null;
            this.formReset$.next();
            setTimeout(() => {
                const form = formsHelpers.generateForm(m, this.descriptors);
                form.valueChanges.pipe(
                    takeUntilDestroyed(this.destroyRef)
                ).subscribe(value => {
                    this.modelChanged.emit({
                        model: { ...this.sectionModel, ...value },
                        changes: { ...value }
                    });
                });
                this.zone.run(() => {
                    this.form = form;
                    this.cdr.detectChanges(); // todo: here or out of a zone cycle?
                });
            });
        } else if (m && !!this.descriptors && this.form && modelChanged) {
            if (!this.equalsModels(m, this._currentSection)) {
                this._currentSection = m;
                this.form.patchValue(m);
            }
        }
    }

    private equalsModels(a: any, b: any): boolean {
        return !!a && !!b && JSON.stringify(a) === JSON.stringify(b);
    }
}
