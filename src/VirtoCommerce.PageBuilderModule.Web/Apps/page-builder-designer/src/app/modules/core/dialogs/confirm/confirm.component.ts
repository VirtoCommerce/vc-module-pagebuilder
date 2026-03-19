import { ChangeDetectionStrategy, Component, inject } from '@angular/core';

import { MatDialogContent, MatDialogActions, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { IconComponent } from '@core/components/icon/icon.component';
import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';

@Component({
    selector: 'app-confirm',
    templateUrl: './confirm.component.html',
    styleUrls: ['./confirm.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [MatDialogContent, MatDialogActions, IconComponent, IconButtonComponent]
})
export class ConfirmComponent {

    private readonly dialogRef = inject(MatDialogRef<ConfirmComponent>);
    private readonly data = inject(MAT_DIALOG_DATA);

    readonly title: string = this.data.title;
    readonly icon: string = this.data.icon;
    readonly confirmText: string = this.data.confirmText || 'OK';
    readonly declineText: string = this.data.declineText || 'Cancel';

    confirm() {
        this.dialogRef.close(true);
    }

    decline() {
        this.dialogRef.close(false);
    }
}
