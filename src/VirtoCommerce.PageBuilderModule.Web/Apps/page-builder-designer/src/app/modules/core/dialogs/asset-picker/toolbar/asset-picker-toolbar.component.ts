import { ChangeDetectionStrategy, Component, ElementRef, input, output, viewChild } from '@angular/core';

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
    readonly creatingFolder = input(false);
    readonly canCreateFolder = input(false);
    readonly canUpload = input(false);
    private readonly newFolderButton = viewChild<ElementRef<HTMLButtonElement>>('newFolderButton');
    readonly multiple = input(false);
    readonly acceptAttribute = input<string | null>(null);
    readonly searchValue = input('');
    readonly counterText = input('');
    readonly searchValueChange = output<string>();
    readonly upload = output<File[]>();
    readonly newFolder = output<void>();

    focusNewFolder() {
        this.newFolderButton()?.nativeElement.focus();
    }

    onUpload(event: Event) {
        const inputElement = event.target as HTMLInputElement;
        const files = Array.from(inputElement.files ?? []);
        inputElement.value = '';
        this.upload.emit(files);
    }
}
