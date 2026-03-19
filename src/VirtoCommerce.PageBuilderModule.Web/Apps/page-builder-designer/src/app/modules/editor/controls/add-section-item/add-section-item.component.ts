import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { NgClass } from '@angular/common';
import { cutString, stripHtmlTags } from '@app/modules/integration/helpers/utils';
import { BaseControlDescriptor, SectionPropertyDescriptor } from '@app/modules/models';
import { SectionSchema } from '@models/document';
import { isArray } from 'lodash-es';
import { IconComponent } from '@core/components/icon/icon.component';

@Component({
    selector: 'app-add-section-item',
    templateUrl: './add-section-item.component.html',
    styleUrls: ['./add-section-item.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgClass, IconComponent]
})
export class AddSectionItemComponent {

    readonly section = input.required<SectionSchema>();
    readonly inPreview = input(false);
    readonly child = input(false);
    readonly descriptor = input<BaseControlDescriptor | undefined>(undefined);

    readonly onPreview = output();
    readonly onAdd = output();

    raiseOnPreview() {
        this.onPreview.emit();
    }

    getSectionName(): string {
        const properties = ['name', 'type'];
        const descriptor = this.descriptor();
        if (descriptor && descriptor.displayPropertyName) {
            const otherPropertiese = Array.isArray(descriptor.displayPropertyName) ? descriptor.displayPropertyName : [descriptor.displayPropertyName];
            properties.splice(0, 0, ...otherPropertiese);
        }
        const v = <any>this.section();
        const result = properties.find(x => !!v[x])!;
        return cutString(stripHtmlTags(v[result]));
    }

    raiseOnAdd(event: MouseEvent) {
        event.stopPropagation();
        this.onAdd.emit();
    }

}
