import { Component, OnInit, ViewChild, AfterContentInit, ElementRef } from '@angular/core';
import { BaseControlDirective } from './../base-control.component';
import { PopupListControlDescriptor } from '@shared/models';
import { MatDialog, MatDialogConfig } from '@angular/material/dialog';
import { PopupContentComponent } from './popup-content.component';
import { tap, filter, map } from 'rxjs/operators';

@Component({
    selector: 'app-popup-item',
    templateUrl: './popup-item.component.html'
})
export class PopupListItemComponent extends BaseControlDirective<PopupListControlDescriptor> {
    constructor(private dialog: MatDialog) {
        super();
    }

    openPopup() {

        const options = <MatDialogConfig>{
            disableClose: true,
            ...this.descriptor.options,
            data: {
                model: this.value,
                context: this.context,
                descriptor: this.descriptor
            },
        };

        const dialogRef = this.dialog.open(PopupContentComponent, options);

        dialogRef.afterClosed().pipe(
            filter(result => result.success),
            map(result => result.data)
        ).subscribe(
            result => {
                this.setValue(result);
                this.onChange(result);
            }
        );
    }
}
