import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { NgModule } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { DragDropModule } from '@angular/cdk/drag-drop';
import { OverlayModule } from '@angular/cdk/overlay';
import { ColorPickerModule } from 'ngx-color-picker';
import { CKEditorModule } from 'ckeditor4-angular';
import { ToastrModule } from 'ngx-toastr';
import { MatDialogModule } from '@angular/material/dialog';

import { COMPONENTS } from './components';
import { CONTROLS } from './controls';

import { LAYOUT_COMPONENTS } from './layouts';
import { PastePopupComponent } from './components/paste-popup/paste-popup.component';

@NgModule({
    declarations: [
        ...COMPONENTS,
        ...CONTROLS,

        ...LAYOUT_COMPONENTS
    ],
    entryComponents: [...CONTROLS, PastePopupComponent],
    imports: [
        BrowserModule,
        BrowserAnimationsModule,
        ReactiveFormsModule,
        DragDropModule,
        OverlayModule,
        ColorPickerModule,
        CKEditorModule,
        MatDialogModule,
        ToastrModule.forRoot()
    ],
    exports: [...COMPONENTS, ...LAYOUT_COMPONENTS, OverlayModule, MatDialogModule]
})
export class SharedModule { }
