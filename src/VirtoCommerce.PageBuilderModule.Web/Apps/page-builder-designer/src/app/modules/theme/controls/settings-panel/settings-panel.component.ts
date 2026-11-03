import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ModelChangedEventArgs, ControlContext } from '@core/models';
import { OverlapPanelComponent } from '@core/components/overlap-panel/overlap-panel.component';
import { PanelComponent } from '@core/components/panel/panel.component';
import { DynamicFormComponent } from '@core/dynamics/dynamic-form/dynamic-form.component';
import { IconComponent } from '@core/components/icon/icon.component';

@Component({
    selector: 'app-settings-panel',
    templateUrl: './settings-panel.component.html',
    styleUrls: ['./settings-panel.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [OverlapPanelComponent, PanelComponent, DynamicFormComponent, IconComponent]
})
export class SettingsPanelComponent {

    readonly settings = input.required<any>();
    readonly group = input.required<any>();
    readonly context = input.required<ControlContext>();

    readonly backClick = output();
    readonly settingsChanged = output<ModelChangedEventArgs>();

    onBackClick() {
        this.backClick.emit();
    }

    onSettingsChanged(args: ModelChangedEventArgs) {
        this.settingsChanged.emit(args);
    }
}
