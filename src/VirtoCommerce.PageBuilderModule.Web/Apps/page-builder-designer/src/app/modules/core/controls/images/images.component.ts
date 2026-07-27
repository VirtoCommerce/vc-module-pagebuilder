import { AssetFile } from '@core/models';
import { Component, computed } from '@angular/core';
import { NgClass, NgStyle } from '@angular/common';
import { CdkDragDrop, DragDropModule } from '@angular/cdk/drag-drop';
import { FileUploadModule } from '@iplab/ngx-file-upload';

import { BaseFilesComponent } from '../base-files.component';
import { ImagesDescriptor } from '@models/controls';
import { ChevronComponent } from '@core/components/chevron/chevron.component';
import { IconComponent } from '@core/components/icon/icon.component';
import { DragHandleComponent } from '@core/components/drag-handle/drag-handle.component';
import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';
import { ControlsListComponent } from '@core/dynamics/controls-list/controls-list.component';
import { SpinnerComponent } from '../../components/spinner/spinner.component';

@Component({
    selector: 'app-images',
    templateUrl: './images.component.html',
    styleUrls: ['./images.component.scss'],
    imports: [NgClass, NgStyle, DragDropModule, FileUploadModule, SpinnerComponent, ChevronComponent, IconComponent, DragHandleComponent, IconButtonComponent, ControlsListComponent]
})
export class ImagesComponent extends BaseFilesComponent<ImagesDescriptor> {

    readonly maxListHeight = computed(() =>
        this.innerValue().length <= (this.descriptor?.collapseThreshold || 4) || this.expanded() || !!this.selectedFile()
            ? 'inherit'
            : '12rem'
    );

    protected override get defaultLabel(): string {
        return this.assetLibraryLabels.imageLabel;
    }

    onReorderItems(event: CdkDragDrop<any>) {
        this.reorderItems(event.previousIndex, event.currentIndex);
    }

    getBackground(item: AssetFile) {
        return item.previewUrl ? `url("${this.escapeCssUrl(item.previewUrl)}")` : null;
    }

    protected override getControlOptions() {
        const result = super.getControlOptions();
        if (!result.accept?.length) {
            result.accept = ['image/*'];
        }
        return result;
    }

    private escapeCssUrl(value: string): string {
        return value
            .replace(/\\/g, '\\\\')
            .replace(/"/g, '\\"')
            .replace(/\r?\n/g, '%0A');
    }
}
