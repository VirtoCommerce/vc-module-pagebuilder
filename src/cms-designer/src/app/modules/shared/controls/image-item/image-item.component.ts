import { Component, ViewChild, ElementRef } from '@angular/core';
import { ApiUrlsService } from '@app/services';
import { FilesService } from '@shared/services';
import { BaseControlComponent } from '../base-control.component';
import { ImageControlDescriptor, ImageDescriptor } from '@shared/models';

@Component({
    selector: 'app-image-item',
    templateUrl: './image-item.component.html',
    styleUrls: ['./image-item.component.scss']
})
export class ImageItemComponent extends BaseControlComponent<ImageControlDescriptor> {

    @ViewChild('fileInput', { static: false, read: ElementRef }) fileInput: ElementRef;

    constructor(private files: FilesService, private urls: ApiUrlsService) {
        super();
    }

    openFileDialog() {
        this.fileInput.nativeElement.click();
    }

    registerOnChange(fn: any): void {
        this.onChange = (event) => {
            if (!event || !event.target) {
                console.log(this.value);
                fn(this.value);
            } else {
                const file = event.target.files[0];
                const subscription = this.files.uploadFile(file, file.name).subscribe(x => {
                    subscription.unsubscribe();
                    this.setValue({ url: x });
                    fn(this.value);
                });
            }
        };
    }

    getAssetUrl(): string {
        return this.urls.getAssetsUrl(this.value.url);
    }

    changeAlt(value: string) {
        this.setValue({ altText: value });
        this.onChange(this.value);
    }

    changeWidth(value: number) {
        this.setValue({ width: (value || undefined) });
        this.onChange(this.value);
    }

    changeHeight(value: number) {
        this.setValue({ height: (value || undefined) });
        this.onChange(this.value);
    }

    removeImage($event: MouseEvent) {
        this.setValue({ url: null });
        this.onChange(this.value);
    }

    setValue(value: ImageDescriptor) {
        const result = { ...this.value, ...value };
        if (!result.altText) {
            result.altText = null;
        }
        super.setValue(result);
    }
}
