import { Component, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { Router, RouterOutlet } from '@angular/router';
import { LogoComponent } from '@core/components/logo/logo.component';
import { ToolbarPlaceholderDirective } from './toolbar-placeholder.directive';

@Component({
    selector: 'app-toolbar',
    templateUrl: './toolbar.component.html',
    styleUrls: ['./toolbar.component.scss'],
    imports: [LogoComponent, ToolbarPlaceholderDirective]
})
export class ToolbarComponent {

    private readonly router = inject(Router);
    private readonly store = inject(Store);

    routerOutletActivated(ro: RouterOutlet) {
    }

}
