import { OnInit, Component, Inject } from '@angular/core';
import { MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { ComponentContext, PasteResultModel, PopupListControlDescriptor } from '@shared/models';
import { FormGroup, FormBuilder, FormArray } from '@angular/forms';
import { FormHelper } from '../../services';
import { CdkDragSortEvent } from '@angular/cdk/drag-drop';

@Component({
    selector: 'app-popup-content',
    styleUrls: ['./popup-content.component.scss'],
    templateUrl: './popup-content.component.html'
})
export class PopupContentComponent implements OnInit {

    public form: FormGroup;
    public items: FormArray;

    constructor(fb: FormBuilder,
        private formHelper: FormHelper,
        private dialogRef: MatDialogRef<PopupContentComponent>,
        @Inject(MAT_DIALOG_DATA) public data: { model: any[], descriptor: PopupListControlDescriptor, context: ComponentContext }) {
        const model = [...data.model||[]];
        this.items = fb.array(model.map(item => formHelper.generateForm(item, data.descriptor.element)));
        this.form = fb.group({ items: this.items });
    }

    ngOnInit() { }

    addElement() {
        this.items.push(this.formHelper.generateForm({}, this.data.descriptor.element));
    }

    removeElement(index) {
        this.items.removeAt(index);
    }

    sortItems(event: CdkDragSortEvent<any>) {
        const current = this.items.controls.splice(event.previousIndex, 1);
        this.items.controls.splice(event.currentIndex, 0, ...current);
        this.items.updateValueAndValidity();
    }

    onCancelClick() {
        this.dialogRef.close({
            success: false,
            error: null,
            data: null
        });
    }

    onClose() {
        this.dialogRef.close({
            success: true,
            error: null,
            data: this.form.value.items
        });
    }
}
