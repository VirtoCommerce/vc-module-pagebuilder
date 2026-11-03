import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeEditorComponent } from '@theme/components/theme-editor/theme-editor.component';

@Component({
    selector: 'app-theme-editor-host',
    templateUrl: './theme-editor-host.component.html',
    styleUrls: ['./theme-editor-host.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ThemeEditorComponent, RouterOutlet]
})
export class ThemeEditorHostComponent {
}
