import { ChangeDetectionStrategy, Component } from '@angular/core';
import { JsonPipe } from '@angular/common';

import { BaseControlDirective } from '@core/controls/base-control.directive';
import { BaseControlDescriptor } from '@models/controls';

@Component({
    selector: 'app-unknown-editor',
    templateUrl: './unknown-editor.component.html',
    styleUrls: ['./unknown-editor.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [JsonPipe]
})
export class UnknownEditorComponent extends BaseControlDirective<BaseControlDescriptor> {
}
