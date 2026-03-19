import { AddSectionComponent } from './add-section/add-section.component';
import { EditSectionComponent } from './edit-section/edit-section.component';
import { TemplateEditorComponent } from './template-editor/template-editor.component';
import { TemplateEditorHostComponent } from './template-editor-host/template-editor-host.component';
import { ToolbarHostComponent } from './toolbar-host/toolbar-host.component';

export * from './template-editor-host/template-editor-host.component';
export * from './toolbar-host/toolbar-host.component';
export * from './add-section/add-section.component';
export * from './edit-section/edit-section.component';

export const COMPONENTS = [
    AddSectionComponent,
    EditSectionComponent,
    TemplateEditorComponent,
    TemplateEditorHostComponent,
    ToolbarHostComponent
];
