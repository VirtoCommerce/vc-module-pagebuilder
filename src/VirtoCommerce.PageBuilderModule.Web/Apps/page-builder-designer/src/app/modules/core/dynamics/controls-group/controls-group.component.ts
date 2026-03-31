import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { NgClass } from '@angular/common';
import { MatRipple } from '@angular/material/core';
import { ChevronComponent } from '@core/components/chevron/chevron.component';

@Component({
    selector: 'app-controls-group',
    templateUrl: './controls-group.component.html',
    styleUrls: ['./controls-group.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgClass, MatRipple, ChevronComponent]
})
export class ControlsGroupComponent {

    readonly label = input<string | null>(null);
    readonly opened = input(false);

    readonly openedChanged = output<boolean>();

    onOpenedChanged() {
        this.openedChanged.emit(!this.opened());
    }
}
