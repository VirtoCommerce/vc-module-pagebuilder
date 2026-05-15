import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NgClass } from '@angular/common';
import { MatDialogActions, MatDialogContent, MatDialogRef } from '@angular/material/dialog';

import { IconComponent } from '@core/components/icon/icon.component';
import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';

import { AssetPickerBreadcrumbsComponent } from './asset-picker-breadcrumbs.component';
import { AssetPickerGridComponent } from './asset-picker-grid.component';
import { AssetPickerStateService } from './asset-picker-state.service';
import { AssetPickerToolbarComponent } from './asset-picker-toolbar.component';
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
    readonly state = inject(AssetPickerStateService);

    confirm() {
        const result = this.state.getSelectionResult();
        if (result) {
            this.dialogRef.close(result);
        }
    }

    decline() {
        this.dialogRef.close(null);
    }
}
