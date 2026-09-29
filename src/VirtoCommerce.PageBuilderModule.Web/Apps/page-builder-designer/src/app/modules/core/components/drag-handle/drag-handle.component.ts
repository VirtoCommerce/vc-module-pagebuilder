import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { IconComponent } from '../icon/icon.component';

@Component({
    selector: 'app-drag-handle',
    templateUrl: './drag-handle.component.html',
    styleUrls: ['./drag-handle.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [IconComponent],
    host: {
        '[class.visible]': 'visible()',
    },
})
export class DragHandleComponent {

    readonly visible = input(false);
    readonly info = input('');
    readonly disabled = input(false);
    readonly keyboardMove = input(false);
    readonly move = output<number>();

    onKeydown(event: KeyboardEvent) {
        if (this.keyboardMove() && !this.disabled() && !event.repeat && (event.key === 'ArrowUp' || event.key === 'ArrowDown')) {
            event.preventDefault();
            event.stopPropagation();
            this.move.emit(event.key === 'ArrowUp' ? -1 : 1);
        }
    }

    onClick(event: MouseEvent) {
        event.stopPropagation();
    }
}
