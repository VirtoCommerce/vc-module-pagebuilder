import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';
import { IconComponent } from '@core/components/icon/icon.component';
import { OzAgentIframeComponent } from '../oz-agent-iframe/oz-agent-iframe.component';
import { OzAgentUiService } from '../../services/oz-agent-ui.service';

@Component({
    selector: 'app-oz-agent-panel',
    templateUrl: './oz-agent-panel.component.html',
    styleUrls: ['./oz-agent-panel.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [IconButtonComponent, IconComponent, OzAgentIframeComponent],
    host: {
        '[class.is-open]': 'ui.isOpen()',
        '[class.is-pinned]': 'ui.isPinned()',
    },
})
export class OzAgentPanelComponent {

    protected readonly ui = inject(OzAgentUiService);

    close() {
        this.ui.close();
    }

    togglePin() {
        this.ui.togglePin();
    }
}
