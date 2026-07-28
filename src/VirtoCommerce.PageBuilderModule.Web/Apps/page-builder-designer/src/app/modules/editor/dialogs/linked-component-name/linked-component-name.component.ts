import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AbstractControl, FormControl, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';

import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';

@Component({
    selector: 'app-linked-component-name',
    templateUrl: './linked-component-name.component.html',
    styleUrls: ['./linked-component-name.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, MatDialogTitle, MatDialogContent, MatDialogActions, IconButtonComponent],
})
export class LinkedComponentNameComponent {
    private readonly dialogRef = inject(MatDialogRef<LinkedComponentNameComponent>);

    readonly name = new FormControl('', {
        nonNullable: true,
        validators: [trimmedRequired, Validators.maxLength(128)],
    });

    confirm(): void {
        this.name.updateValueAndValidity();
        const name = this.name.value.trim();
        if (this.name.invalid) {
            this.name.markAsTouched();
            return;
        }
        this.dialogRef.close({ accept: true, name });
    }

    decline(): void {
        this.dialogRef.close({ accept: false });
    }
}

function trimmedRequired(control: AbstractControl<string>): ValidationErrors | null {
    return control.value.trim().length > 0 ? null : { required: true };
}
