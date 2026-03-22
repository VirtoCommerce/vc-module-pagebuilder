import { ChangeDetectionStrategy, Component } from '@angular/core';
import { OverlayModule } from '@angular/cdk/overlay';

@Component({
    selector: 'app-icon-with-preview',
    templateUrl: './icon-with-preview.component.html',
    styleUrls: ['./icon-with-preview.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [OverlayModule]
})
export class IconWithPreviewComponent {

    isOpen = false;

}
