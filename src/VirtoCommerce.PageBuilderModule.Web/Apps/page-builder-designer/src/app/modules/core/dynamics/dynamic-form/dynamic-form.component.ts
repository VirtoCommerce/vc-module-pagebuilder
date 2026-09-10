import { Component, DestroyRef, effect, input, output, signal, untracked, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, takeUntil } from 'rxjs';

import { ReactiveFormsModule, UntypedFormGroup } from '@angular/forms';

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
export class DynamicFormComponent {

  private readonly destroyRef = inject(DestroyRef);
  private readonly formReset$ = new Subject<void>();
  private _currentSectionId: string | null = null;

  readonly sectionModel = input.required<SectionModel>();
  readonly context = input.required<ControlContext>();
  readonly descriptors = input.required<BaseControlDescriptor[]>();
  readonly readOnly = input(false);
  readonly modelChanged = output<ModelChangedEventArgs>();
  readonly form = signal<UntypedFormGroup | null>(null);
  readonly formKey = signal(0);

  constructor() {
    effect(() => {
      const section = this.sectionModel();
      const descriptors = this.descriptors();
      const readOnly = this.readOnly();

      if (!section || !descriptors?.length) return;

      if (section.id !== this._currentSectionId) {
        this._currentSectionId = section.id;
        this.formReset$.next();

        const newForm = formsHelpers.generateForm(section, descriptors);
        if (readOnly) {
          newForm.disable({ emitEvent: false });
        }
        newForm.valueChanges
          .pipe(takeUntil(this.formReset$), takeUntilDestroyed(this.destroyRef))
          .subscribe(value => {
            this.modelChanged.emit({
              model: { ...this.sectionModel(), ...value },
              changes: { ...value }
            });
          });

        // Both signals are set synchronously and batched into a single render.
        // Incrementing formKey causes @for in the template to destroy and recreate
        // the <form> DOM via track, which resets all child control components
        // (CKEditor, color pickers, etc.) without a null intermediate state or setTimeout.
        this.form.set(newForm);
        this.formKey.update(k => k + 1);
      } else {
        const currentForm = untracked(() => this.form());
        currentForm?.patchValue(section, { emitEvent: false });
        if (readOnly) {
          currentForm?.disable({ emitEvent: false });
        } else {
          currentForm?.enable({ emitEvent: false });
        }
      }
    }, { allowSignalWrites: true });
  }
}
