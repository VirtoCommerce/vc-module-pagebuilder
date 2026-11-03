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

    // Bundled, not linked from the OZ deployment: its asset names carry a content hash, so every OZ
    // redeploy renames the file and would leave this button with a broken image.
    readonly iconUrl = 'assets/icons/oz-chat.png';

    toggle() {
        this.ui.toggle();
    }
}
