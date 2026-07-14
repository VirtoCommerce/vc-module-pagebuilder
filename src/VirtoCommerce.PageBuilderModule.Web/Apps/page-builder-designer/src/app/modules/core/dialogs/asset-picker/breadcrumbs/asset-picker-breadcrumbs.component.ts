import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

import { AssetPickerBreadcrumb } from '../asset-picker.models';

@Component({
    selector: 'app-asset-picker-breadcrumbs',
    templateUrl: './asset-picker-breadcrumbs.component.html',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class AssetPickerBreadcrumbsComponent {
    readonly breadcrumbs = input<AssetPickerBreadcrumb[]>([]);
    readonly navigate = output<string>();
}
