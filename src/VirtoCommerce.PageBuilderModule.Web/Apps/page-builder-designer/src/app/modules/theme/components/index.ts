import { ThemeEditorHostComponent } from './theme-editor-host/theme-editor-host.component';
import { ThemeEditorComponent } from './theme-editor/theme-editor.component';
import { PresetsPanelComponent } from './presets-panel/presets-panel.component';
import { ToolbarHostComponent } from './toolbar-host/toolbar-host.component';

export * from './theme-editor-host/theme-editor-host.component';
export * from './toolbar-host/toolbar-host.component';
export * from './presets-panel/presets-panel.component';

export const COMPONENTS = [
    ThemeEditorHostComponent,
    ThemeEditorComponent,
    PresetsPanelComponent,
    ToolbarHostComponent
];
