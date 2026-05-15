import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';

import { IconComponent } from '@core/components/icon/icon.component';
import { AssetLibraryLabels } from '@core/services';

@Component({
    selector: 'app-asset-picker-toolbar',
    template: `
        <div class="toolbar">
            <button
                type="button"
                class="upload"
                [disabled]="uploading || loading"
                (click)="uploadInput.click()">
                <app-icon>upload</app-icon>
                <span>{{ uploading ? labels.uploading : labels.upload }}</span>
            </button>
            <input
                #uploadInput
                type="file"
                hidden
                [multiple]="multiple"
                [attr.accept]="acceptAttribute"
                (change)="onUpload($event)" />
            <input
                type="text"
                [value]="searchValue"
                [placeholder]="labels.searchPlaceholder"
                (input)="search.emit($any($event.target).value)" />
            <span class="counter">{{ counterText }}</span>
        </div>
    `,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [IconComponent]
})
export class AssetPickerToolbarComponent {
    @Input({ required: true }) labels!: AssetLibraryLabels;
    @Input() uploading = false;
    @Input() loading = false;
    @Input() multiple = false;
    @Input() acceptAttribute: string | null = null;
    @Input() searchValue = '';
    @Input() counterText = '';
    @Output() search = new EventEmitter<string>();
    @Output() upload = new EventEmitter<File[]>();

    onUpload(event: Event) {
        const input = event.target as HTMLInputElement;
        const files = Array.from(input.files ?? []);
        input.value = '';
        this.upload.emit(files);
    }
}
