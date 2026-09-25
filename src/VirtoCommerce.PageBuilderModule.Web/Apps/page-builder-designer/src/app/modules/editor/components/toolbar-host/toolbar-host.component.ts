import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Store } from '@ngrx/store';

import { AppConfig } from '@integration/services';

import { BuilderState } from '@editor/store/state';
import { canEditSharedComponentOriginal } from '@editor/helpers';
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
            // The descriptor is what says this store can take a page down at all — on the git flow it
            // points at the endpoint that deletes the page from the production branch.
            useUnpublish: !!this.appConfig.getValue('publish')?.unpublish,
            // Promotion to production, offered only on the git flow — and, like unpublish, the
            // descriptor's presence is what says the store has it.
            usePromote: !!this.appConfig.getValue('publish')?.promote,
            useExternalPreview: !!this.appConfig.getValue('externalPreview'),
            // pages kept in git have versions; a store on blob storage has none, and the server withholds
            // the descriptor for it
            useHistory: !!this.appConfig.getValue('history'),
            canEditSharedComponents: canEditSharedComponentOriginal(this.appConfig),
        }
    )), { initialValue: null });
    readonly sharedComponentId = toSignal(
        this.store$.select(routingSelectors.selectSharedComponentIdParameter),
        { initialValue: '' },
    );

    onActionExecuted(action: string) {
        if (action === 'save'
            && this.sharedComponentId()
            && !canEditSharedComponentOriginal(this.appConfig)) {
            return;
        }
        this.store$.dispatch(actions.executeToolbarAction({ action }));
    }

}
