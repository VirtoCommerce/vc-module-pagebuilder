import { CdkDragRelease, CdkDragSortEvent, CdkDragStart, DragDropModule } from '@angular/cdk/drag-drop';
import { ChangeDetectionStrategy, Component, Input, input, output } from '@angular/core';
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

    currentHoverId: string | null = null;
    selectedBlocksCount = 0;

    readonly section = input.required<SectionModel>();
    readonly blocksSchemas = input.required<SectionsSchemasList>();
    private _states!: BlockStatesList;
    public get states(): BlockStatesList {
        return this._states;
    }
    @Input({ required: true })
    public set states(value: BlockStatesList) {
        this.selectedBlocksCount = Object.values(value || {}).filter(x => x.selected).length;
        this._states = value;
    }
    readonly selectMode = input(false);

    readonly itemClick = output<SectionModel>();
    readonly checkChanged = output<{ blockId: string, selected: boolean }>();
    readonly addBlockClick = output();
    readonly reorderBlocks = output<ReorderItemsModel>();
    readonly executeAction = output<{ action: string, block: SectionModel }>();

    onReorderBlocks(event: CdkDragSortEvent<SectionModel>) {
        this.reorderBlocks.emit({ item: event.item.data, currentIndex: event.currentIndex, previousIndex: event.previousIndex, parent: this.section() });
    }

    onItemClick(block: SectionModel) {
        this.itemClick.emit(block);
    }

    onAddBlockClick() {
        this.addBlockClick.emit();
    }

    onActionExecuted(action: string, block: SectionModel) {
        this.executeAction.emit({ action, block });
    }

    onItemSelectChanged(selected: boolean, blockId: string) {
        this.checkChanged.emit({ blockId, selected });
    }

    blockDragStarted(event: CdkDragStart) {
        const rootElement = event.source.getRootElement();
        this._fakeElement = domHelpers.deepCloneNode(rootElement);
        domHelpers.toggleVisibility(this._fakeElement, true, new Set('position'));
        this._fakeElement.classList.add('dragging');
        event.source.dropContainer.element.nativeElement.insertBefore(this._fakeElement, event.source.getPlaceholderElement());
    }

    blockDragCompleted() {
        this._fakeElement?.remove();
        this._fakeElement = null;
    }
}
