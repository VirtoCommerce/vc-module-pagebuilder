import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogActions, MatDialogContent, MatDialogRef, MatDialogTitle } from '@angular/material/dialog';

import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';
import type { SharedComponentInsertionMode } from '@editor/helpers';

export interface SharedComponentInsertModeData {
    name: string;
    defaultMode: SharedComponentInsertionMode;
    allowShared: boolean;
    sharedComponentDisabledReason?: string;
}

export interface SharedComponentInsertModeResult {
    accept: boolean;
    mode?: SharedComponentInsertionMode;
}

@Component({
    selector: 'app-shared-component-insert-mode',
    templateUrl: './shared-component-insert-mode.component.html',
    styleUrls: ['./shared-component-insert-mode.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [MatDialogTitle, MatDialogContent, MatDialogActions, IconButtonComponent],
})
export class SharedComponentInsertModeComponent {
    private readonly dialogRef = inject(MatDialogRef<SharedComponentInsertModeComponent>);

    readonly data = inject<SharedComponentInsertModeData>(MAT_DIALOG_DATA);
    readonly mode = signal<SharedComponentInsertionMode>(
        this.data.allowShared && this.data.defaultMode === 'shared' ? 'shared' : 'copy',
    );

    select(mode: SharedComponentInsertionMode): void {
        if (mode === 'shared' && !this.data.allowShared) {
            return;
        }
        this.mode.set(mode);
    }

    confirm(): void {
        this.dialogRef.close({ accept: true, mode: this.mode() } satisfies SharedComponentInsertModeResult);
    }

    decline(): void {
        this.dialogRef.close({ accept: false } satisfies SharedComponentInsertModeResult);
    }
}
