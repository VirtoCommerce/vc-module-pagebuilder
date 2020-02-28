import { Injectable } from '@angular/core';
import { Observable, from } from 'rxjs';
import { BlockValuesModel } from '@shared/models';

import * as clipboard from 'clipboard-polyfill';

@Injectable({
    providedIn: 'root'
})
export class ClipboardService {

    copyTo(block: BlockValuesModel) {
        const blockToCopy = { ...block };
        delete blockToCopy.id;
        const value = JSON.stringify(blockToCopy);
        clipboard.writeText(value).catch(error => {
            console.log(error);
        });
    }

    pasteFrom(): Observable<BlockValuesModel> {
        const result: Promise<BlockValuesModel> = clipboard.readText()
            .then(text => {
                let block: BlockValuesModel = null;
                try {
                    block = JSON.parse(text);
                } catch (error) {
                    console.log(error);
                }
                return block;
            })
            .catch(error => {
                console.log(error);
                return null;
            });
        return from(result);
    }
}
