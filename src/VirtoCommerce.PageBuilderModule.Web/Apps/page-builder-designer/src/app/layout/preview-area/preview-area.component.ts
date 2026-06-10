import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Store } from '@ngrx/store';
import { BuilderState } from '@shared/routing';
import * as fromRoute from '@shared/routing';
import { LivePreviewComponent } from '@shared/components/live-preview/live-preview.component';
import { OzAgentPanelComponent } from '@ai-agent/components/oz-agent-panel/oz-agent-panel.component';

@Component({
    selector: 'app-preview-area',
    templateUrl: './preview-area.component.html',
    styleUrls: ['./preview-area.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [LivePreviewComponent, OzAgentPanelComponent],
    host: {
        '[class.desktop-50]': 'desktop50()',
    },
})
export class PreviewAreaComponent {

    private readonly store = inject(Store<BuilderState>);

    readonly desktop50 = toSignal(this.store.select(fromRoute.isDesktop50), { initialValue: false });
}
