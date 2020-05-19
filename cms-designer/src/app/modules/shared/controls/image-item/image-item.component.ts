import { Component, ViewChild, ElementRef } from '@angular/core';
import { ApiUrlsService } from '@app/services';
import { FilesService } from '@shared/services';
import { BaseControlDirective } from '../base-control.component';
import { ImageControlDescriptor, ImageDescriptor } from '@shared/models';

@Component({
    selector: 'app-image-item',
    templateUrl: './image-item.component.html',
    styleUrls: ['./image-item.component.scss']
})
export class ImageItemComponent extends BaseControlDirective<ImageControlDescriptor> {

    @ViewChild('fileInput', { read: ElementRef }) fileInput: ElementRef;

    constructor(private files: FilesService, private urls: ApiUrlsService) {
        super();
    }

    openFileDialog() {
        this.fileInput.nativeElement.click();
    }

    registerOnChange(fn: any): void {
        this.onChange = (event) => {
            if (!event || !event.target) {
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
        if (!this.value) return null;
        if (typeof this.value === 'string') {
            return this.urls.getAssetsUrl(this.value);
        } else if (this.value.url) {
            return this.urls.getAssetsUrl(this.value.url);
        }
        return null;
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

    setValue(value: ImageDescriptor | string) {
        if (value == null) {
            super.setValue(value);
        } else {
            if (typeof value === 'string') {
                value = { url: value };
            }
            if (this.descriptor.inline) {
                super.setValue(value.url);
            } else {
                const result = { ...this.value, ...value };
                if (!result.altText) {
                    result.altText = null;
                }
                super.setValue(result);
            }
        }
    }
}