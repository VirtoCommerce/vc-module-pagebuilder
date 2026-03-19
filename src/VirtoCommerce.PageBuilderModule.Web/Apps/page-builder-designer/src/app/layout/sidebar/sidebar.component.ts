import { ChangeDetectionStrategy, Component, HostBinding, inject } from '@angular/core';
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
    imports: [RouterOutlet]
})
export class SidebarComponent {

    private readonly store = inject(Store<BuilderState>);

    private readonly isHidden$ = toSignal(this.store.select(fromRoute.isFullscreenPreviewMode), { initialValue: false });
    private readonly desktop50$ = toSignal(this.store.select(fromRoute.isDesktop50), { initialValue: false });

    @HostBinding('class.hidden') get isHidden() { return this.isHidden$(); }
    @HostBinding('class.desktop-50') get desktop50() { return this.desktop50$(); }
}
