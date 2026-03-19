import { ChangeDetectionStrategy, Component, input, output, inject } from '@angular/core';
import { NgClass } from '@angular/common';
import { IconComponent } from '@core/components/icon/icon.component';
import { CheckboxComponent } from '@core/controls/checkbox/checkbox.component';
import { ContextMenuComponent } from '@core/components/context-menu/context-menu.component';
// import { trigger, state, style, animate, transition } from '@angular/animations';

import { ContextMenuAction } from '@core/models';
import { SectionModel, SectionSchema } from '@models/document';
import { ContextMenuHelper, helpers } from '@editor/helpers';

@Component({
    selector: 'app-section-item',
    templateUrl: './section-item.component.html',
    styleUrls: ['./section-item.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgClass, IconComponent, CheckboxComponent, ContextMenuComponent]
})
export class SectionItemComponent {

    private readonly helper = inject(ContextMenuHelper);

    isHover: boolean = false;
    isIconHover: boolean = false;

    readonly section = input.required<SectionModel>();
    readonly sectionSchema = input.required<SectionSchema>();
    readonly hasContextMenu = input(false);
    readonly selectable = input(true);
    readonly selected = input(false);

    readonly actionClick = output<string>();
    readonly itemClick = output();
    readonly itemHover = output();
    readonly itemSelectChanged = output<boolean>();

    get displayCheckbox(): boolean {
        return (this.isIconHover && this.selectable()) || this.selected();
    }

    onItemClick(event: MouseEvent) {
        if (!!this.sectionSchema()) {
            this.itemClick.emit();
        }
    }

    onCheckboxClick(event: MouseEvent) {
        event.stopPropagation();
    }

    onCheckboxValueChanged(value: boolean) {
        this.itemSelectChanged.emit(value);
    }

    onActionClick(event: ContextMenuAction) {
        if (event !== '|') {
            this.actionClick.emit(event.action);
        }
    }

    onItemHover() {
        this.itemHover.emit();
    }

    getSectionIcon(): string | null {
        if (!this.sectionSchema()) {
            return null; // todo: unknown schema icon
        }
        return this.sectionSchema().icon || 'blur_on'; // todo: schema hasn't icon
    }

    getSectionName(): string {
        return helpers.getSectionName(this.section(), this.sectionSchema());
        // if (this.sectionSchema?.displayField) {
        //     return <string>this.section[this.sectionSchema.displayField] || <string>this.section['name'] || this.section.type;
        // }
        // return <string>this.section['name'] || this.section.type;
    }

    getItemActions: () => Promise<ContextMenuAction[]> = () => {
        const result = this.helper.getSectionsActions(this.section(), !!this.sectionSchema()?.blocks?.length);
        return result;
    };
}
