import { MatDialog } from '@angular/material/dialog';
import { Injectable } from '@angular/core';
import { Observable, from } from 'rxjs';
import { BlockValuesModel, PasteResultModel } from '@shared/models';
import { PastePopupComponent } from '@shared/components/paste-popup/paste-popup.component';

import * as clipboard from 'clipboard-polyfill';

@Injectable({
    providedIn: 'root'
})
export class ClipboardService {

    constructor(private dialog: MatDialog) { }

    copyTo(block: BlockValuesModel) {
        const blockToCopy = { ...block };
        delete blockToCopy.id;
        const value = JSON.stringify(blockToCopy);
        clipboard.writeText(value).catch(error => {
            console.log(error);
        });
    }

    pasteFrom(): Observable<PasteResultModel> {
        const result: Promise<PasteResultModel> = clipboard.readText()
            .then(text => {
                return {
                    success: !!text,
                    data: text,
                    error: null
                };
            })
            .catch(error => {
                console.log(error);
                return {
                    error: error,
                    success: false,
                    data: null
                };
            });
        return from(result);
    }

    pasteThroughPopup(): Observable<PasteResultModel> {
        const dialogRef = this.dialog.open(PastePopupComponent, {
            width: '680px',
            height: '370px'
        });
        return dialogRef.afterClosed();
    }
}
