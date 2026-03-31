import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { NgClass, NgStyle } from '@angular/common';
import { ChevronComponent } from '../chevron/chevron.component';

@Component({
    selector: 'app-collapsible-list-item',
    templateUrl: './collapsible-list-item.component.html',
    styleUrls: ['./collapsible-list-item.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgClass, NgStyle, ChevronComponent]
})
export class CollapsibleListItemComponent {

    readonly opened = input(false);
    readonly expandable = input(false);
    readonly hovered = input(false);
    readonly highlight = input(false);

    readonly openChanged = output<boolean>();

    onChevronClick() {
        this.openChanged.emit(!this.opened());
    }
}
