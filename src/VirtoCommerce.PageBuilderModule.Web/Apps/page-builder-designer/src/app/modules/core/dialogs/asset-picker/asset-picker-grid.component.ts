import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { NgClass } from '@angular/common';

import { IconComponent } from '@core/components/icon/icon.component';
import { AssetLibraryEntry } from '@core/services';

import { AssetPickerEntryDragEvent, AssetPickerGridItem } from './asset-picker.models';

@Component({
    selector: 'app-asset-picker-grid',
    template: `
        <div class="grid">
            @for (item of entries; track item.key) {
                <button
                    type="button"
                    class="card"
                    [ngClass]="{
                        'card--folder': item.entry.type === 'folder',
                        'card--selected': item.selected,
                        'card--drop-target': item.folderDropTarget
                    }"
                    (dragover)="item.entry.type === 'folder' ? folderDragOver.emit({ entry: item.entry, event: $event }) : null"
                    (dragleave)="item.entry.type === 'folder' ? folderDragLeave.emit({ entry: item.entry, event: $event }) : null"
                    (drop)="item.entry.type === 'folder' ? folderDrop.emit({ entry: item.entry, event: $event }) : null"
                    (click)="entryClick.emit(item.entry)"
                    (dblclick)="item.entry.type === 'blob' && !multiple && item.selected ? assetDoubleClick.emit() : null">
                    <div class="preview">
                        @if (item.entry.type === 'folder') {
                            <app-icon class="folder-icon">folder</app-icon>
                        } @else if (item.previewUrl) {
                            <div class="image-frame">
                                <img class="image" [src]="item.previewUrl" [alt]="item.entry.name" loading="lazy" />
                            </div>
                        } @else {
                            <app-icon class="file-icon">insert_drive_file</app-icon>
                        }
                    </div>
                    <div class="meta">
                        <div class="name" [title]="item.entry.name">{{ item.entry.name }}</div>
                        @if (item.size) {
                            <div class="size">{{ item.size }}</div>
                        }
                    </div>
                </button>
            }
        </div>
    `,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgClass, IconComponent]
})
export class AssetPickerGridComponent {
    @Input() entries: AssetPickerGridItem[] = [];
    @Input() multiple = false;
    @Output() entryClick = new EventEmitter<AssetLibraryEntry>();
    @Output() assetDoubleClick = new EventEmitter<void>();
    @Output() folderDragOver = new EventEmitter<AssetPickerEntryDragEvent>();
    @Output() folderDragLeave = new EventEmitter<AssetPickerEntryDragEvent>();
    @Output() folderDrop = new EventEmitter<AssetPickerEntryDragEvent>();
}
