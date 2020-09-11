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
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatMomentDateModule, MAT_MOMENT_DATE_ADAPTER_OPTIONS } from '@angular/material-moment-adapter';

import { COMPONENTS, POPUPS } from './components';
import { CONTROLS } from './controls';

import { LAYOUT_COMPONENTS } from './layouts';

@NgModule({
    declarations: [
        ...COMPONENTS,
        ...CONTROLS,
        ...POPUPS,
        ...LAYOUT_COMPONENTS
    ],
    entryComponents: [...CONTROLS, ...POPUPS],
    imports: [
        BrowserModule,
        BrowserAnimationsModule,
        ReactiveFormsModule,
        DragDropModule,
        OverlayModule,
        ColorPickerModule,
        CKEditorModule,
        MatDialogModule,
        MatDatepickerModule,
        MatMomentDateModule,
        ToastrModule.forRoot()
    ],
    providers: [
        { provide: MAT_MOMENT_DATE_ADAPTER_OPTIONS, useValue: { useUtc: true } }
    ],
    exports: [...COMPONENTS, ...LAYOUT_COMPONENTS, OverlayModule, MatDialogModule, MatDatepickerModule, MatMomentDateModule]
})
export class SharedModule { }
