import { Injectable, inject } from '@angular/core';
import { ComponentType } from '@angular/cdk/portal';
import { MatDialog, MatDialogConfig } from '@angular/material/dialog';
import { Observable } from 'rxjs';
import { ConfirmComponent, AlertComponent } from '../dialogs';

@Injectable({
    providedIn: 'root'
})
export class ModalService {
    private readonly modals = inject(MatDialog);

    show<T>(content: ComponentType<any>, config: MatDialogConfig): Observable<T> {
        const dialog = this.modals.open(content, config);
        return dialog.afterClosed();
    }

    confirm(title: string): Observable<boolean> {
        return this.show(ConfirmComponent, { data: { title, icon: 'error' }, panelClass: 'confirm-dialog' });
    }

    alert(title: string): Observable<boolean> {
        return this.show(AlertComponent, { data: { title } });
    }
}
