import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIcon } from '@angular/material/icon';

@Component({
    selector: 'app-icon',
    templateUrl: './icon.component.html',
    styleUrls: ['./icon.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [MatIcon],
    host: {
        '[class.inline]': 'inline()',
        '[class.hoverable]': 'hoverable()',
        '[class.small-size]': 'smallSize()',
    },
})
export class IconComponent {
    readonly inline = input<boolean>(false);
    readonly hoverable = input<boolean>(false);
    readonly smallSize = input<boolean>(false);
}
