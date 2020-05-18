import { BaseControlDescriptor, ControlDescriptor } from '.';
import { MatDialogConfig } from '@angular/material/dialog';

export interface PopupListControlDescriptor extends BaseControlDescriptor {
    addText?: string;
    removeText?: string;
    element: ControlDescriptor[];
    options?: MatDialogConfig
}
