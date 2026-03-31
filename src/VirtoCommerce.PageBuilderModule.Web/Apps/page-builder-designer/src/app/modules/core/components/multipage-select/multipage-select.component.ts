import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

import { OverlayModule } from '@angular/cdk/overlay';
import { NgScrollbar } from 'ngx-scrollbar';
import { MultipageSelectDescriptor } from '@core/models';
import { ChevronComponent } from '../chevron/chevron.component';
import { SeparatorComponent } from '../separator/separator.component';
import { IconComponent } from '../icon/icon.component';

@Component({
    selector: 'app-multipage-select',
    templateUrl: './multipage-select.component.html',
    styleUrls: ['./multipage-select.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [OverlayModule, NgScrollbar, ChevronComponent, SeparatorComponent, IconComponent],
})
export class MultipageSelectComponent {

    readonly panelClass = input('');

    readonly titleText = input<string | null>(null);
    readonly filter = input<string | null>(null);
    readonly filterPlaceholder = input('');
    readonly default = input<MultipageSelectDescriptor>();
    readonly current = input<MultipageSelectDescriptor | null>(null);
    readonly parentItems = input<MultipageSelectDescriptor[] | null>([]);
    readonly childrenItems = input<MultipageSelectDescriptor[] | null>(null);

    readonly itemSelected = output<MultipageSelectDescriptor>();
    readonly filterChanged = output<string>();
    readonly backClick = output();

    isOpen = false;

    get currentLabel(): string {
        return this.current()?.title || this.default()?.title || '';
    }

    close() {
        this.isOpen = false;
    }

    outsideClick(event: MouseEvent) {
        event.stopPropagation();
        this.close();
    }

    togglePopover() {
        this.isOpen = !this.isOpen;
    }

    updateFilter(event: Event) {
        const target = <HTMLInputElement>event.target;
        this.filterChanged.emit(target.value);
    }

    getItemName(item: MultipageSelectDescriptor): string {
        return item.title;
    }

    selectItem(item: MultipageSelectDescriptor) {
        this.itemSelected.emit(item);
        if (!item.hasChildren) {
            this.isOpen = false;
        }
    }

    back() {
        this.backClick.emit();
    }
}
