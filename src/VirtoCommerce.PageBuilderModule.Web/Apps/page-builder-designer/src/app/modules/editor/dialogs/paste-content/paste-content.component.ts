import { ReactiveFormsModule, UntypedFormControl, UntypedFormGroup } from '@angular/forms';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatDialogContent, MatDialogActions, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';

@Component({
    selector: 'app-paste-content',
    templateUrl: './paste-content.component.html',
    styleUrls: ['./paste-content.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, MatDialogContent, MatDialogActions, IconButtonComponent]
})
export class PasteContentComponent {

    private readonly dialogRef = inject(MatDialogRef<PasteContentComponent>);
    private readonly data = inject(MAT_DIALOG_DATA);

    readonly form: UntypedFormGroup = (() => {
        let text = this.data.clipboardData;
        try {
            const obj = JSON.parse(text);
            text = JSON.stringify(obj, null, 4);
        } catch {
            // ignore any error
        }
        return new UntypedFormGroup({ value: new UntypedFormControl(text) });
    })();

    confirm() {
        const result = { ...this.form.value, accept: true };
        this.dialogRef.close(result);
    }

    decline() {
        this.dialogRef.close({ accept: false });
    }
}
