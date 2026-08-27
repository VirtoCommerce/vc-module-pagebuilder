import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Store } from '@ngrx/store';

import { AppConfig } from '@integration/services';

import { BuilderState } from '@editor/store/state';
import * as actions from '@editor/store/actions';
import * as selectors from '@editor/store/selectors';
import { DefaultToolbarComponent } from '@shared/components/default-toolbar/default-toolbar.component';

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
            useExternalPreview: !!this.appConfig.getValue('externalPreview'),
            // pages kept in git have versions; a store on blob storage has none, and the server withholds
            // the descriptor for it
            useHistory: !!this.appConfig.getValue('history')
        }
    )), { initialValue: null });

    onActionExecuted(action: string) {
        this.store$.dispatch(actions.executeToolbarAction({ action }));
    }

}
