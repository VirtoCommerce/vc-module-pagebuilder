import { ItemsGroup } from '@core/models';
import { SectionSchema } from '@models/document';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

import { IconComponent } from '@core/components/icon/icon.component';
import { ChevronComponent } from '@core/components/chevron/chevron.component';
import { AddSectionItemComponent } from '@editor/controls/add-section-item/add-section-item.component';

@Component({
    selector: 'app-add-section-group',
    templateUrl: './add-section-group.component.html',
    styleUrls: ['./add-section-group.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [IconComponent, ChevronComponent, AddSectionItemComponent]
})
export class AddSectionGroupComponent {

    readonly group = input.required<ItemsGroup<SectionSchema>>();
    readonly opened = input(false);
    readonly underPreviewType = input<string | null>(null);

    readonly onAdd = output<SectionSchema>();
    readonly onPreview = output<SectionSchema>();
    readonly onOpened = output();

    raiseOnAdd(section: SectionSchema) {
        this.onAdd.emit(section);
    }

    raiseOnPreview(section: SectionSchema) {
        this.onPreview.emit(section);
    }

    raiseOnOpened() {
        this.onOpened.emit();
    }

}
