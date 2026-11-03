import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { NgClass } from '@angular/common';
import { IconComponent } from '../icon/icon.component';

@Component({
    selector: 'app-chevron',
    templateUrl: './chevron.component.html',
    styleUrls: ['./chevron.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgClass, IconComponent]
})
export class ChevronComponent {
    readonly opened = input(false);
    readonly vertical = input(false);
    readonly hoverable = input(true);
}
