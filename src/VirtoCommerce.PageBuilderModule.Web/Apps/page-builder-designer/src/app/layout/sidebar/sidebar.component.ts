import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterOutlet } from '@angular/router';
import { Store } from '@ngrx/store';
import { BuilderState } from '@shared/routing';
import * as fromRoute from '@shared/routing';

@Component({
    selector: 'app-sidebar',
    templateUrl: './sidebar.component.html',
    styleUrls: ['./sidebar.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [RouterOutlet],
    host: {
        '[class.hidden]': 'isHidden()',
        '[class.desktop-50]': 'desktop50()',
    },
})
export class SidebarComponent {

    private readonly store = inject(Store<BuilderState>);

    readonly isHidden = toSignal(this.store.select(fromRoute.isFullscreenPreviewMode), { initialValue: false });
    readonly desktop50 = toSignal(this.store.select(fromRoute.isDesktop50), { initialValue: false });
}
