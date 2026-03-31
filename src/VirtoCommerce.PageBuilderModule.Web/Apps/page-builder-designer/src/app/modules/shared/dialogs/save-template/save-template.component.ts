import { ChangeDetectionStrategy, Component, ElementRef, viewChild, inject } from '@angular/core';

import { ReactiveFormsModule, FormRecord, FormControl } from '@angular/forms';
import { MatDialogContent, MatDialogActions, MatDialogRef, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { TemplateEntryInfo } from '@shared/models';
import { IconComponent } from '@core/components/icon/icon.component';
import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';

@Component({
    selector: 'app-save-template',
    templateUrl: './save-template.component.html',
    styleUrls: ['./save-template.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [ReactiveFormsModule, MatDialogContent, MatDialogActions, IconComponent, IconButtonComponent]
})
export class SaveTemplateComponent {

    readonly selectAllInput = viewChild.required<ElementRef<HTMLInputElement>>('selectAllInput');

    private readonly dialogRef = inject(MatDialogRef<SaveTemplateComponent>);
    private readonly data = inject<{ entries: TemplateEntryInfo[] }>(MAT_DIALOG_DATA);

    readonly entries = this.data.entries;
    readonly form = new FormRecord(
        Object.fromEntries(this.entries.map(e => [e.key, new FormControl(true, { nonNullable: true })]))
    );

    selectAll(event: Event) {
        const value = (event.target as HTMLInputElement).checked;
        this.entries.forEach(e => this.form.get(e.key)?.setValue(value));
    }

    setSelectAll() {
        const value = this.form.value;
        const checked = Object.keys(value).every(key => value[key]);
        const indeterminate = !checked && Object.keys(value).some(key => value[key]);
        this.selectAllInput().nativeElement.indeterminate = indeterminate;
        this.selectAllInput().nativeElement.checked = checked || indeterminate;
    }

    confirm() {
        const value = this.form.value;
        this.dialogRef.close({ entries: Object.keys(value).filter(key => value[key]), accept: true });
    }

    decline() {
        this.dialogRef.close({ accept: false });
    }
}
