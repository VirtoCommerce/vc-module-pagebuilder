import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';

import { AssetPickerBreadcrumb } from './asset-picker.models';

@Component({
    selector: 'app-asset-picker-breadcrumbs',
    template: `
        <div class="breadcrumbs">
            @for (crumb of breadcrumbs; track crumb.url; let last = $last) {
                <button type="button" [disabled]="last" (click)="navigate.emit(crumb.url)">
                    {{ crumb.label }}
                </button>
                @if (!last) {
                    <span>/</span>
                }
            }
        </div>
    `,
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class AssetPickerBreadcrumbsComponent {
    @Input() breadcrumbs: AssetPickerBreadcrumb[] = [];
    @Output() navigate = new EventEmitter<string>();
}
