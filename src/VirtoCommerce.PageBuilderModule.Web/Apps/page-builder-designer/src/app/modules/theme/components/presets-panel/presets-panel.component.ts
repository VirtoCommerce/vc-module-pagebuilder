import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NgClass, KeyValuePipe } from '@angular/common';
import { Store } from '@ngrx/store';
import { OverlapPanelComponent } from '@core/components/overlap-panel/overlap-panel.component';
import { PanelComponent } from '@core/components/panel/panel.component';
import { IconComponent } from '@core/components/icon/icon.component';
import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';
import { PresetsIconComponent } from '@theme/controls/presets-icon/presets-icon.component';

import { BuilderState } from '@theme/store/state';

import * as actions from '@theme/store/actions';
import * as fromTheme from '@theme/store/selectors';
import * as fromRoute from '@shared/routing/selectors';

@Component({
    selector: 'app-presets-panel',
    templateUrl: './presets-panel.component.html',
    styleUrls: ['./presets-panel.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgClass, KeyValuePipe, OverlapPanelComponent, PanelComponent, IconComponent, IconButtonComponent, PresetsIconComponent]
})
export class PresetsPanelComponent {

    private readonly store$ = inject(Store<BuilderState>);

    readonly filter = toSignal(this.store$.select(fromTheme.selectPresetsFilter), { initialValue: '' });
    readonly viewModel = toSignal(this.store$.select(fromTheme.selectPresetsContext));
    readonly isHalfScreen = toSignal(this.store$.select(fromRoute.isDesktop50), { initialValue: false });

    onBackClick() {
        this.store$.dispatch(actions.exitPresets());
    }

    usePreset(preset: string) {
        this.store$.dispatch(actions.applyPreset({ preset }));
    }

    previewPreset(preset: string) {
        this.store$.dispatch(actions.previewPreset({ preset }));
    }

    applyPresetsFilter(event: Event) {
        const value = (event.target as HTMLInputElement).value;
        this.store$.dispatch(actions.applyPresetsFilter({ filter: value }));
    }
}
