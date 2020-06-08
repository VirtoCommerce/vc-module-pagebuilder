import { OnInit, Component, Inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { PasteResultModel } from '@shared/models';
import { FormGroup, FormBuilder } from '@angular/forms';

@Component({
    selector: 'app-paste-popup',
    templateUrl: './paste-popup.component.html'
})
export class PastePopupComponent implements OnInit {

    public form: FormGroup;

    constructor(fb: FormBuilder,
        private dialogRef: MatDialogRef<PastePopupComponent>,
        @Inject(MAT_DIALOG_DATA) public model: PasteResultModel) {
        this.form = fb.group({
            data: fb.control((!!model ? model.data : '') || '')
        });
    }

    ngOnInit() { }

    onCancelClick() {
        this.dialogRef.close({
            success: false,
            error: null,
            data: null
        });
    }

    onClose() {
        this.dialogRef.close({
            success: true,
            error: null,
            data: this.form.value.data
        });
    }
}
