import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Store } from '@ngrx/store';

import { AppConfig } from '@integration/services';

import { BuilderState } from '@editor/store/state';
import { canEditLinkedComponentOriginal } from '@editor/helpers';
import * as actions from '@editor/store/actions';
import * as selectors from '@editor/store/selectors';
import { DefaultToolbarComponent } from '@shared/components/default-toolbar/default-toolbar.component';
import * as routingSelectors from '@shared/routing/selectors';

@Component({
    selector: 'app-toolbar-host',
    templateUrl: './toolbar-host.component.html',
    styleUrls: ['./toolbar-host.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [DefaultToolbarComponent]
})
export class ToolbarHostComponent {

    private readonly store$ = inject(Store<BuilderState>);
    private readonly appConfig = inject(AppConfig);

    readonly panels = toSignal(this.store$.select(selectors.selectToolbarButtonsState(
        {
            useTheme: !this.appConfig.getValue('skipTheme'),
            useDrafts: !!this.appConfig.getValue('publish'),
            useExternalPreview: !!this.appConfig.getValue('externalPreview'),
            canEditLinkedComponents: canEditLinkedComponentOriginal(this.appConfig),
        }
    )), { initialValue: null });
    readonly linkedComponentId = toSignal(
        this.store$.select(routingSelectors.selectLinkedComponentIdParameter),
        { initialValue: '' },
    );

    onActionExecuted(action: string) {
        if (action === 'save'
            && this.linkedComponentId()
            && !canEditLinkedComponentOriginal(this.appConfig)) {
            return;
        }
        this.store$.dispatch(actions.executeToolbarAction({ action }));
    }

}
