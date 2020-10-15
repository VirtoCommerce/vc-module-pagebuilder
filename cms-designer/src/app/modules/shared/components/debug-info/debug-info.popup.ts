import { OnInit, Component, Inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { PasteResultModel } from '@shared/models';
import { FormGroup, FormBuilder } from '@angular/forms';

@Component({
    selector: 'app-debug-info',
    templateUrl: './debug-info.popup.html'
})
export class DebugInfoPopupComponent implements OnInit {

    form: FormGroup;

    constructor(fb: FormBuilder, private dialogRef: MatDialogRef<DebugInfoPopupComponent>,
        @Inject(MAT_DIALOG_DATA) model: PasteResultModel) {
        this.form = fb.group({
            data: fb.control((!!model ? model.data : '') || '')
        });
    }

    ngOnInit() { }

    onClose() {
        this.dialogRef.close();
    }
}
