import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

import { IconComponent } from '@core/components/icon/icon.component';
import { AssetLibraryLabels } from '@core/services';

@Component({
    selector: 'app-asset-picker-toolbar',
    templateUrl: './asset-picker-toolbar.component.html',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [IconComponent]
})
export class AssetPickerToolbarComponent {
    readonly labels = input.required<AssetLibraryLabels>();
    readonly uploading = input(false);
    readonly loading = input(false);
    readonly multiple = input(false);
    readonly acceptAttribute = input<string | null>(null);
    readonly searchValue = input('');
    readonly counterText = input('');
    readonly search = output<string>();
    readonly upload = output<File[]>();

    onUpload(event: Event) {
        const inputElement = event.target as HTMLInputElement;
        const files = Array.from(inputElement.files ?? []);
        inputElement.value = '';
        this.upload.emit(files);
    }
}
