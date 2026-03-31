import { ChangeDetectionStrategy, Component, inject, input, output } from '@angular/core';

import { ActionButtonDescriptor } from '@core/models';

import { AppConfig } from '@integration/services';

import { PreviewModeComponent } from '@shared/components/preview-mode/preview-mode.component';
import { TemplateSelectorComponent } from '@shared/components/template-selector/template-selector.component';
import { ActionsPanelComponent } from '@shared/components/actions-panel/actions-panel.component';

@Component({
    selector: 'app-default-toolbar',
    templateUrl: './default-toolbar.component.html',
    styleUrls: ['./default-toolbar.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [PreviewModeComponent, TemplateSelectorComponent, ActionsPanelComponent]
})
export class DefaultToolbarComponent {

    private readonly appConfig = inject(AppConfig);

    readonly panels = input<ActionButtonDescriptor[][] | null>(null);
    readonly actionExecuted = output<string>();

    displayTemplateSelector: boolean = !this.appConfig.getValue('skipTemplates');

    onActionExecuted(item: string) {
        this.actionExecuted.emit(item);
    }
}
