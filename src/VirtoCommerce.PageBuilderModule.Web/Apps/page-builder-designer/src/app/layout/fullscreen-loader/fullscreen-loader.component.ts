import { ChangeDetectionStrategy, Component } from '@angular/core';
import { SpinnerComponent } from '@core/components/spinner/spinner.component';

@Component({
    selector: 'app-fullscreen-loader',
    templateUrl: './fullscreen-loader.component.html',
    styleUrls: ['./fullscreen-loader.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [SpinnerComponent]
})
export class FullscreenLoaderComponent {
}
