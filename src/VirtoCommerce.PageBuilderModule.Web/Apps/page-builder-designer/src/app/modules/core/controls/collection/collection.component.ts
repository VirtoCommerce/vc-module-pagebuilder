import { ModalService } from '@core/services';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { DragDropModule } from '@angular/cdk/drag-drop';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { UntypedFormArray, UntypedFormGroup, AbstractControl } from '@angular/forms';
import { Subject, takeUntil } from 'rxjs';

import { BaseControlDirective } from '@core/controls/base-control.directive';
import { CollectionDescriptor, ControlDescriptor } from '@models/controls';

import { ContextMenuActionType, ControlContext } from '@core/models';
import { coreHelpers, formsHelpers } from '@core/helpers';
import { CdkDragDrop } from '@angular/cdk/drag-drop';
import { appHelpers } from '@integration/helpers';

import { ChevronComponent } from '@core/components/chevron/chevron.component';
import { ContextMenuComponent } from '@core/components/context-menu/context-menu.component';
import { DragHandleComponent } from '@core/components/drag-handle/drag-handle.component';
import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';
import { ControlsListComponent } from '@core/dynamics/controls-list/controls-list.component';

@Component({
    selector: 'app-collection',
    templateUrl: './collection.component.html',
    styleUrls: ['./collection.component.scss'],
    imports: [NgClass, ReactiveFormsModule, DragDropModule, ChevronComponent, ContextMenuComponent, DragHandleComponent, IconButtonComponent, ControlsListComponent]
})
export class CollectionComponent extends BaseControlDirective<CollectionDescriptor> {

    private readonly destroyRef = inject(DestroyRef);
    private readonly modals = inject(ModalService);
    private readonly formReset$ = new Subject<void>();
    readonly titles = signal<string[]>([]);

    hoverItem: any | null = null;
    openedItem: any | null = null;

    itemActions = [{
        action: 'duplicate',
        title: 'Duplicate',
        icon: 'file_copy',
        selected: false,
        inactive: () => !this.canAddItem()
    }, {
        action: 'delete',
        title: 'Delete',
        icon: 'delete_outline'
    }];


    form!: UntypedFormGroup;
    collectionFormArray!: UntypedFormArray;

    toggle(item: any) {
        if (this.openedItem === item) {
            this.openedItem = null;
        } else {
            this.openedItem = item;
        }
    }

    getContext(item: UntypedFormGroup, index: number): ControlContext {
        return { ...this.context, item: this.controlValue(), index, element: item.value, parent: this.context };
    }

    override setControlValue(value: any): void {
        if (value !== this.controlValue() || !this.form) {
            if (!value) {
                value = [];
            }
            if (!Array.isArray(value)) {
                value = [value];
            }
            super.setControlValue(value);
            const descriptors = this.getDescriptors();
            this.collectionFormArray = formsHelpers.generateFormArray(value, descriptors);
            this.form = new UntypedFormGroup({ list: this.collectionFormArray });
            this.formReset$.next();
            this.updateTitles(value);
            this.form.valueChanges.pipe(
                takeUntil(this.formReset$),
                takeUntilDestroyed(this.destroyRef)
            ).subscribe(x => {
                this.onValueChanged(x.list);
                this.updateTitles(x.list);
            });
        }
    }

    private updateTitles(values: any[]): void {
        const field = this.descriptor?.displayField;
        this.titles.set(values.map((v, i) => {
            const fromField = field ? appHelpers.getValueByPath(v, field) : undefined;
            return (fromField && String(fromField)) || `Item ${i + 1}`;
        }));
    }

    getDescriptors(): ControlDescriptor[] {
        return this.descriptor?.element || [];
    }

    canAddItem(): boolean {
        return !this.descriptor?.maxCount || this.collectionFormArray.length < this.descriptor.maxCount;
    }

    addItem() {
        const descriptors = this.getDescriptors();
        const item = formsHelpers.generateForm(
            coreHelpers.createDefaultObject(descriptors),
            descriptors
        );
        this.collectionFormArray.push(item);
        this.openedItem = item;
    }

    onActionClick(event: ContextMenuActionType, item: AbstractControl, index: number) {
        if (event.action === 'duplicate') {
            if (!this.canAddItem()) {
                return;
            }
            const descriptors = this.getDescriptors();
            const newItem = formsHelpers.generateForm(item.value, descriptors);
            this.collectionFormArray.insert(index + 1, newItem);
            this.openedItem = newItem;
        } else if (event.action === 'delete') {
            if (!this.descriptor?.skipRemoveConfirmation) {
                this.modals.confirm(this.descriptor?.removeMessage || 'Do you want to delete this item?').subscribe((data: any) => {
                    if (data) {
                        this.collectionFormArray.removeAt(index);
                    }
                });
            } else {
                this.collectionFormArray.removeAt(index);
            }
        }
    }

    reorderItems(event: CdkDragDrop<any>) {
        const item = this.collectionFormArray.at(event.previousIndex);
        this.collectionFormArray.removeAt(event.previousIndex);
        this.collectionFormArray.insert(event.currentIndex, item);
    }

}
