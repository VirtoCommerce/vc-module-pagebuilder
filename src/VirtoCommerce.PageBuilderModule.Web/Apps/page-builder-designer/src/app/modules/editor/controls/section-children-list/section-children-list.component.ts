import { CdkDragSortEvent, CdkDragStart, DragDropModule } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { BlockStatesList, SectionsSchemasList } from '@editor/models';
import { ReorderItemsModel } from '@core/models';
import { SectionModel } from '@models/document';
import { domHelpers } from '@core/helpers';
import { SectionItemComponent } from '@editor/controls/section-item/section-item.component';
import { DragHandleComponent } from '@core/components/drag-handle/drag-handle.component';
import { IconButtonComponent } from '@core/components/icon-button/icon-button.component';

@Component({
    selector: 'app-section-children-list',
    templateUrl: './section-children-list.component.html',
    styleUrls: ['./section-children-list.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgClass, DragDropModule, SectionItemComponent, DragHandleComponent, IconButtonComponent]
})
export class SectionChildrenListComponent {

    private _fakeElement: HTMLElement | null = null;

    readonly currentHoverId = signal<string | null>(null);

    readonly section = input.required<SectionModel>();
    readonly blocksSchemas = input.required<SectionsSchemasList>();
    readonly states = input.required<BlockStatesList>();
    readonly selectedBlocksCount = computed(() =>
        Object.values(this.states() || {}).filter(x => x.selected).length
    );
    readonly selectMode = input(false);
    readonly readOnly = input(false);

    readonly itemClick = output<SectionModel>();
    readonly checkChanged = output<{ blockId: string, selected: boolean }>();
    readonly addBlockClick = output();
    readonly reorderBlocks = output<ReorderItemsModel>();
    readonly executeAction = output<{ action: string, block: SectionModel }>();

    onReorderBlocks(event: CdkDragSortEvent<SectionModel>) {
        if (this.readOnly()) {
            return;
        }
        this.reorderBlocks.emit({ item: event.item.data, currentIndex: event.currentIndex, previousIndex: event.previousIndex, parent: this.section() });
    }

    onItemClick(block: SectionModel) {
        if (this.readOnly()) {
            return;
        }
        this.itemClick.emit(block);
    }

    onAddBlockClick() {
        if (this.readOnly()) {
            return;
        }
        this.addBlockClick.emit();
    }

    onActionExecuted(action: string, block: SectionModel) {
        if (this.readOnly()) {
            return;
        }
        this.executeAction.emit({ action, block });
    }

    onItemSelectChanged(selected: boolean, blockId: string) {
        if (this.readOnly()) {
            return;
        }
        this.checkChanged.emit({ blockId, selected });
    }

    blockDragStarted(event: CdkDragStart) {
        if (this.readOnly()) {
            return;
        }
        const rootElement = event.source.getRootElement();
        this._fakeElement = domHelpers.deepCloneNode(rootElement);
        domHelpers.toggleVisibility(this._fakeElement, true, new Set(['position']));
        this._fakeElement.classList.add('dragging');
        event.source.dropContainer.element.nativeElement.insertBefore(this._fakeElement, event.source.getPlaceholderElement());
    }

    blockDragCompleted() {
        this._fakeElement?.remove();
        this._fakeElement = null;
    }
}
