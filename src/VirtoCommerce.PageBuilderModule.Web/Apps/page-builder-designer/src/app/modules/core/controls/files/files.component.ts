import { CdkDragDrop, DragDropModule } from '@angular/cdk/drag-drop';
import { Component } from '@angular/core';
import { NgClass, NgStyle } from '@angular/common';
import { MatProgressBar } from '@angular/material/progress-bar';
import { FileUploadModule } from '@iplab/ngx-file-upload';

import { FilesDescriptor } from '@models/controls';
import { BaseFilesComponent } from '../base-files.component';
import { ChevronComponent } from '@core/components/chevron/chevron.component';
import { IconComponent } from '@core/components/icon/icon.component';
import { DragHandleComponent } from '@core/components/drag-handle/drag-handle.component';
import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';
import { ControlsListComponent } from '@core/dynamics/controls-list/controls-list.component';

@Component({
    selector: 'app-files',
    templateUrl: './files.component.html',
    styleUrls: ['./files.component.scss'],
    imports: [NgClass, NgStyle, DragDropModule, MatProgressBar, FileUploadModule, ChevronComponent, IconComponent, DragHandleComponent, IconButtonComponent, ControlsListComponent]
})
export class FilesComponent extends BaseFilesComponent<FilesDescriptor> {

    getMaxListHeight(): string {
        return this.innerValue.length <= (this.descriptor?.collapseThreshold || 6) || this.expanded || !!this.selectedFile
            ? 'inherit'
            : 'calc((' + (this.descriptor?.collapseCount || 4) + ' + .5) * (.5rem + 20px))'
    }

    onReorderItems(event: CdkDragDrop<any>) {
        this.reorderItems(event.previousIndex, event.currentIndex);
    }
}
