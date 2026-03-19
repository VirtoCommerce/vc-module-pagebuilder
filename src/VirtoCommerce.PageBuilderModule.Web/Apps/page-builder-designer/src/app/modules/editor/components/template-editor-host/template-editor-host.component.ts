import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TemplateEditorComponent } from '@editor/components/template-editor/template-editor.component';

@Component({
    selector: 'app-template-editor-host',
    templateUrl: './template-editor-host.component.html',
    styleUrls: ['./template-editor-host.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [RouterOutlet, TemplateEditorComponent]
})
export class TemplateEditorHostComponent {
}
