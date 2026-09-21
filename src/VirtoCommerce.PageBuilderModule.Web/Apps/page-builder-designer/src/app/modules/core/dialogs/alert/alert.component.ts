import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatDialogContent, MatDialogActions, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';

@Component({
    selector: 'app-alert',
    templateUrl: './alert.component.html',
    styleUrls: ['./alert.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [MatDialogContent, MatDialogActions, IconButtonComponent]
})
export class AlertComponent {

    private readonly dialogRef = inject(MatDialogRef<AlertComponent>);
    private readonly data = inject(MAT_DIALOG_DATA);

    readonly title: string = this.data.title;
    readonly confirmText: string = this.data.confirmText || 'OK';

    confirm() {
        this.dialogRef.close();
    }
}
