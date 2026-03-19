import { Store } from '@ngrx/store';
import { ChangeDetectionStrategy, Component, HostListener, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterOutlet } from '@angular/router';

import * as sharedSelectors from '@shared/store/selectors';
import * as editorSelectors from '@editor/store/selectors';
import * as themeSelectors from '@theme/store/selectors';
import { BuilderState as SharedState } from '@shared/store';
import { BuilderState as EditorState } from '@editor/store';
import { BuilderState as ThemeState } from '@theme/store';

import { ToolbarComponent } from './layout/toolbar/toolbar.component';
import { PreviewAreaComponent } from './layout/preview-area/preview-area.component';
import { FullscreenLoaderComponent } from './layout/fullscreen-loader/fullscreen-loader.component';

@Component({
    selector: 'app-root',
    templateUrl: './app.component.html',
    styleUrls: ['./app.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [RouterOutlet, ToolbarComponent, PreviewAreaComponent, FullscreenLoaderComponent]
})
export class AppComponent {

    private store$ = inject(Store<SharedState & EditorState & ThemeState>);

    readonly isHttpLoading = toSignal(this.store$.select(sharedSelectors.isHttpLoading));
    readonly isEditorLoading = toSignal(this.store$.select(editorSelectors.isLoading), { initialValue: false });
    readonly isThemeLoading = toSignal(this.store$.select(themeSelectors.isLoading), { initialValue: false });

    @HostListener('window:keyup', ['$event'])
    keyEvent(event: KeyboardEvent) {
        if (event.key === 'Escape') {
            // todo: useful feature, must be implemented
            // this.store$.dispatch(actions.closeAllPanels());
        }
    }
}
