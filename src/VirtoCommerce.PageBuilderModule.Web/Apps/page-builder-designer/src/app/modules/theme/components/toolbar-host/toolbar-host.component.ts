import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Store } from '@ngrx/store';

import { BuilderState } from '@theme/store/state';
import { DefaultToolbarComponent } from '@shared/components/default-toolbar/default-toolbar.component';
import * as actions from '@theme/store/actions';
import * as selectors from '@theme/store/selectors';

@Component({
    selector: 'app-toolbar-host',
    templateUrl: './toolbar-host.component.html',
    styleUrls: ['./toolbar-host.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [DefaultToolbarComponent]
})
export class ToolbarHostComponent {

    private readonly store$ = inject(Store<BuilderState>);

    readonly panels = toSignal(this.store$.select(selectors.selectToolbarButtonsState), { initialValue: null });

    onActionExecuted(action: string) {
        this.store$.dispatch(actions.executeAction({ action }));
    }

}
