import { ChangeDetectionStrategy, Component, HostBinding, input } from '@angular/core';
import { MatIcon } from '@angular/material/icon';

@Component({
    selector: 'app-icon',
    templateUrl: './icon.component.html',
    styleUrls: ['./icon.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [MatIcon]
})
export class IconComponent {
    readonly inline = input<boolean>(false);
    readonly hoverable = input<boolean>(false);
    readonly smallSize = input<boolean>(false);

    @HostBinding('class.inline') get inlineClass() { return this.inline(); }
    @HostBinding('class.hoverable') get hoverableClass() { return this.hoverable(); }
    @HostBinding('class.small-size') get smallSizeClass() { return this.smallSize(); }
}
