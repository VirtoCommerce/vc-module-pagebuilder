import { Component, Inject, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';

@Component({
    selector: 'app-login',
    templateUrl: './login.component.html',
    styleUrls: ['./login.component.scss']
})
export class LoginComponent implements OnInit {
    public form: FormGroup;

    constructor(fb: FormBuilder,
        private dialogRef: MatDialogRef<LoginComponent>,
        @Inject(MAT_DIALOG_DATA) public model: { save: boolean }) {
        this.form = fb.group({
            username: ['', Validators.required],
            password: ['', Validators.required],
            save: model.save
        });
    }

    ngOnInit() { }

    onCancelClick() {
        this.dialogRef.close(null);
    }

    onClose() {
        if (this.form.dirty && this.form.valid) {
            this.dialogRef.close({
                data: {
                    username: this.form.value.username,
                    password: this.form.value.password,
                    save: this.form.value.save
                }
            });
        }
    }

}
