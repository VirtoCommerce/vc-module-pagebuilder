import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { NgStyle } from '@angular/common';
import { IconComponent } from '../icon/icon.component';

@Component({
    selector: 'app-drag-handle',
    templateUrl: './drag-handle.component.html',
    styleUrls: ['./drag-handle.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgStyle, IconComponent],
    host: {
        '[class.visible]': 'visible()',
    },
})
export class DragHandleComponent {

    readonly visible = input(false);
    readonly info = input('');

    onClick(event: MouseEvent) {
        event.stopPropagation();
    }
}
