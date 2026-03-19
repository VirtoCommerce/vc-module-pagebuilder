import { ChangeDetectionStrategy, Component, inject } from '@angular/core';

import { NgvMarkdownComponent } from 'ngv-markdown';

import { MarkdownModel } from 'ngv-markdown';
import { BaseControlDirective } from '@core/controls/base-control.directive';
import { AssetsService } from '@core/services';
import { MarkdownDescriptor } from '@models/controls';
import { AssetFile } from '../../models';

@Component({
    selector: 'app-markdown',
    templateUrl: './markdown.component.html',
    styleUrls: ['./markdown.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgvMarkdownComponent]
})
export class MarkdownComponent extends BaseControlDirective<MarkdownDescriptor> {

    private readonly data = inject(AssetsService);

    uploadImage = (file: File) => this.data.uploadAsset(<AssetFile>file, this.descriptor || {}, this.context, () => { }, { randomizeAssetName: true });

    override setControlValue(value: any): void {
        const isMarkdown = this.descriptor?.resultType === 'markdown';
        const isHtml = this.descriptor?.resultType === 'html';
        const isMixed = !isMarkdown && !isHtml;
        const isValueStringOrNull = typeof value === 'string' || value === null;

        const result = {
            markdown: (isMarkdown || isMixed
                ? (isValueStringOrNull ? value : value?.markdown)
                : '') || '',
            html: (isHtml || isMixed
                ? (isValueStringOrNull ? value : value?.html)
                : '') || ''
        };
        this.controlValue.set(result);
    }

    onInputValueChanged(event: MarkdownModel) {
        let value: MarkdownModel | string | null;
        if (this.descriptor?.resultType === 'markdown') {
            value = event.markdown;
        } else if (this.descriptor?.resultType === 'html') {
            value = event.html;
        } else {
            value = event;
        }
        this.onValueChanged(value);
    }
}
