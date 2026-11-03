import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Store } from '@ngrx/store';

import { ModelChangedEventArgs } from '@core/models';
import { PanelComponent } from '@core/components/panel/panel.component';
import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';
import { ChevronComponent } from '@core/components/chevron/chevron.component';
import { DynamicFormComponent } from '@core/dynamics/dynamic-form/dynamic-form.component';
import { SettingsPanelComponent } from '@theme/controls/settings-panel/settings-panel.component';

import * as fromTheme from '@theme/store/selectors';
import * as actions from '@theme/store/actions';

@Component({
    selector: 'app-theme-editor',
    templateUrl: './theme-editor.component.html',
    styleUrls: ['./theme-editor.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [PanelComponent, IconButtonComponent, ChevronComponent, DynamicFormComponent, SettingsPanelComponent]
})
export class ThemeEditorComponent {

    private readonly store$ = inject(Store<any>);

    readonly editableGroup = toSignal(this.store$.select(fromTheme.selectEditableGroup), { initialValue: null });
    readonly settings = toSignal(this.store$.select(fromTheme.selectCurrentSettings), { initialValue: null });
    readonly schema = toSignal(this.store$.select(fromTheme.selectSettingsSchema), { initialValue: null });
    readonly uiState = toSignal(this.store$.select(fromTheme.selectGroupsState), { initialValue: null });

    context = <any>{}; // todo: select from state



    toggleGroup(group: any) {
        this.store$.dispatch(actions.toggleGroup({ group }));
    }

    onBackClick(group: any) {
        this.store$.dispatch(actions.toggleGroup({ group }));
    }

    onPresetsClick() {
        this.store$.dispatch(actions.gotoPresets());
    }

    onSettingsChanged(args: ModelChangedEventArgs) {
        this.store$.dispatch(actions.updateSettings(args));
    }
}
