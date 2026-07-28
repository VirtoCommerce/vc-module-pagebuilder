import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';

import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';
import type { LinkedComponentInsertionMode } from '@editor/helpers';

export interface LinkedComponentInsertModeData {
    name: string;
    defaultMode: LinkedComponentInsertionMode;
    allowLinked: boolean;
    linkedDisabledReason?: string;
}

export interface LinkedComponentInsertModeResult {
    accept: boolean;
    mode?: LinkedComponentInsertionMode;
}

@Component({
    selector: 'app-linked-component-insert-mode',
    templateUrl: './linked-component-insert-mode.component.html',
    styleUrls: ['./linked-component-insert-mode.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [MatDialogTitle, MatDialogContent, MatDialogActions, IconButtonComponent],
})
export class LinkedComponentInsertModeComponent {
    private readonly dialogRef = inject(MatDialogRef<LinkedComponentInsertModeComponent>);

    readonly data = inject<LinkedComponentInsertModeData>(MAT_DIALOG_DATA);
    readonly mode = signal<LinkedComponentInsertionMode>(
        this.data.allowLinked && this.data.defaultMode === 'linked' ? 'linked' : 'copy',
    );

    select(mode: LinkedComponentInsertionMode): void {
        if (mode === 'linked' && !this.data.allowLinked) {
            return;
        }
        this.mode.set(mode);
    }

    confirm(): void {
        this.dialogRef.close({ accept: true, mode: this.mode() } satisfies LinkedComponentInsertModeResult);
    }

    decline(): void {
        this.dialogRef.close({ accept: false } satisfies LinkedComponentInsertModeResult);
    }
}
