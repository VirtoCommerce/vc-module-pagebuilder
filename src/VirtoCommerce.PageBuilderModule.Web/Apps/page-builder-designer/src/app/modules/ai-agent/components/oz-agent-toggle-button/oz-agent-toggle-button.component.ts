import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { OzAgentUiService } from '../../services/oz-agent-ui.service';

@Component({
    selector: 'app-oz-agent-toggle-button',
    templateUrl: './oz-agent-toggle-button.component.html',
    styleUrls: ['./oz-agent-toggle-button.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    host: {
        '[class.is-hidden]': 'ui.isOpen()',
    },
})
export class OzAgentToggleButtonComponent {

    protected readonly ui = inject(OzAgentUiService);

    readonly iconUrl = 'https://virtooz.virtocommerce.org/public/assets/chat-DafIu_-0.png';

    toggle() {
        this.ui.toggle();
    }
}
