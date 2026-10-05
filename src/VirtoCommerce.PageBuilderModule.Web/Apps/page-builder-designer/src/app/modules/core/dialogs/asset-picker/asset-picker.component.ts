import { afterRenderEffect, ChangeDetectionStrategy, Component, ElementRef, inject, viewChild } from '@angular/core';
import { DOCUMENT, NgClass } from '@angular/common';
import { FormField } from '@angular/forms/signals';
import { MatDialogActions, MatDialogContent, MatDialogRef } from '@angular/material/dialog';

import { IconComponent } from '@core/components/icon/icon.component';
import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';

import { AssetPickerBreadcrumbsComponent } from './breadcrumbs/asset-picker-breadcrumbs.component';
import { AssetPickerGridComponent } from './grid/asset-picker-grid.component';
import { AssetPickerStateService } from './asset-picker-state.service';
import { AssetPickerToolbarComponent } from './toolbar/asset-picker-toolbar.component';
import { AssetPickerDialogResult } from './asset-picker.models';

export type {
    AssetPickerDialogData,
    AssetPickerDialogItem,
    AssetPickerDialogResult,
} from './asset-picker.models';

@Component({
    selector: 'app-asset-picker',
    templateUrl: './asset-picker.component.html',
    styleUrls: ['./asset-picker.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    providers: [AssetPickerStateService],
    imports: [
        NgClass,
        FormField,
        MatDialogContent,
        MatDialogActions,
        IconComponent,
        IconButtonComponent,
        AssetPickerBreadcrumbsComponent,
        AssetPickerToolbarComponent,
        AssetPickerGridComponent,
    ]
})
export class AssetPickerComponent {

    private readonly dialogRef = inject(MatDialogRef<AssetPickerComponent, AssetPickerDialogResult | null>);
    private readonly document = inject(DOCUMENT);
    readonly state = inject(AssetPickerStateService);
    private readonly folderInput = viewChild<ElementRef<HTMLInputElement>>('folderInput');
    private readonly toolbar = viewChild(AssetPickerToolbarComponent);
    private restoreFolderFocus = false;

    constructor() {
        afterRenderEffect(() => {
            const input = this.folderInput();
            if (input) {
                input.nativeElement.focus();
                this.restoreFolderFocus = true;
            } else if (this.restoreFolderFocus && !this.state.creatingFolder() && !this.state.loading() && !this.state.uploading()) {
                if (this.document.activeElement === this.document.body) {
                    this.toolbar()?.focusNewFolder();
                }
                this.restoreFolderFocus = false;
            }
        });
    }

    closeFolderForm(event?: Event) {
        event?.preventDefault();
        event?.stopPropagation();
        if (!this.state.creatingFolder()) {
            this.state.folderFormOpen.set(false);
        }
    }

    confirm() {
        if (this.state.creatingFolder() || this.state.uploading()) {
            return;
        }
        const result = this.state.getSelectionResult();
        if (result) {
            this.dialogRef.close(result);
        }
    }

    decline() {
        this.dialogRef.close(null);
    }
}
