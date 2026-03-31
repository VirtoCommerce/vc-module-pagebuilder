import { Component } from '@angular/core';
import { LogoComponent } from '@core/components/logo/logo.component';
import { ToolbarPlaceholderDirective } from './toolbar-placeholder.directive';

@Component({
    selector: 'app-toolbar',
    templateUrl: './toolbar.component.html',
    styleUrls: ['./toolbar.component.scss'],
    imports: [LogoComponent, ToolbarPlaceholderDirective]
})
export class ToolbarComponent {

}
