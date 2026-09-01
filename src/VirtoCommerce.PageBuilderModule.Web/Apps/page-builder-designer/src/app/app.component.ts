import { Store } from '@ngrx/store';
import { ChangeDetectionStrategy, Component, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterOutlet } from '@angular/router';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';

import * as sharedSelectors from '@shared/store/selectors';
import * as editorSelectors from '@editor/store/selectors';
import * as themeSelectors from '@theme/store/selectors';
import { BuilderState as SharedState } from '@shared/store';
import { BuilderState as EditorState } from '@editor/store';
import { BuilderState as ThemeState } from '@theme/store';
import { LoginComponent } from '@shared/dialogs';
import { SessionService } from '@integration/services';

import { ToolbarComponent } from './layout/toolbar/toolbar.component';
import { PreviewAreaComponent } from './layout/preview-area/preview-area.component';
import { FullscreenLoaderComponent } from './layout/fullscreen-loader/fullscreen-loader.component';

@Component({
    selector: 'app-root',
    templateUrl: './app.component.html',
    styleUrls: ['./app.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [RouterOutlet, ToolbarComponent, PreviewAreaComponent, FullscreenLoaderComponent],
    host: {
        '(window:keyup)': 'keyEvent($event)',
    },
})
export class AppComponent {

    private store$ = inject(Store<SharedState & EditorState & ThemeState>);
    private readonly session = inject(SessionService);
    private readonly dialogs = inject(MatDialog);

    private loginDialog?: MatDialogRef<LoginComponent>;

    readonly isHttpLoading = toSignal(this.store$.select(sharedSelectors.isHttpLoading));
    readonly isEditorLoading = toSignal(this.store$.select(editorSelectors.isLoading), { initialValue: false });
    readonly isThemeLoading = toSignal(this.store$.select(themeSelectors.isLoading), { initialValue: false });

    constructor() {
        effect(() => {
            if (this.session.expired()) {
                this.promptForLogin();
            }
        });
    }

    // Nothing in the designer works without a session, so the prompt is modal and cannot be
    // dismissed - the alternative used to be a silently broken designer (VCST-5847).
    private promptForLogin() {
        if (this.loginDialog) {
            return;
        }
        this.loginDialog = this.dialogs.open(LoginComponent, { disableClose: true, panelClass: 'login-dialog' });
        this.loginDialog.afterClosed().subscribe(() => {
            this.loginDialog = undefined;
            // the session may have expired again while the designer was catching up after the sign in
            if (this.session.expired()) {
                this.promptForLogin();
            }
        });
    }

    keyEvent(event: KeyboardEvent) {
        if (event.key === 'Escape') {
            // todo: useful feature, must be implemented
            // this.store$.dispatch(actions.closeAllPanels());
        }
    }
}
