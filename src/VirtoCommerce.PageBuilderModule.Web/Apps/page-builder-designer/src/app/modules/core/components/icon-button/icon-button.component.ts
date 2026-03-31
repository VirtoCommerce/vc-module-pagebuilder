import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { NgClass } from '@angular/common';
import { IconComponent } from '../icon/icon.component';

@Component({
    selector: 'app-icon-button',
    templateUrl: './icon-button.component.html',
    styleUrls: ['./icon-button.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgClass, IconComponent]
})
export class IconButtonComponent {

    readonly icon = input<string>();
    readonly text = input<string>();
    readonly skin = input<string>();
    readonly disabled = input(false);

    readonly onClick = output<MouseEvent>();

    raiseOnClick(event: MouseEvent) {
        this.onClick.emit(event);
    }
}
