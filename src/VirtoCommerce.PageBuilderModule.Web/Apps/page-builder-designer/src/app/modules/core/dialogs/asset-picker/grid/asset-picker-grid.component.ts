import { NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

import { IconComponent } from '@core/components/icon/icon.component';
import { AssetLibraryEntry } from '@core/services';

import { AssetPickerEntryDragEvent, AssetPickerGridItem } from '../asset-picker.models';

@Component({
    selector: 'app-asset-picker-grid',
    templateUrl: './asset-picker-grid.component.html',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgClass, IconComponent]
})
export class AssetPickerGridComponent {
    readonly entries = input<AssetPickerGridItem[]>([]);
    readonly multiple = input(false);
    readonly entryClick = output<AssetLibraryEntry>();
    readonly assetDoubleClick = output<void>();
    readonly folderDragOver = output<AssetPickerEntryDragEvent>();
    readonly folderDragLeave = output<AssetPickerEntryDragEvent>();
    readonly folderDrop = output<AssetPickerEntryDragEvent>();
}
